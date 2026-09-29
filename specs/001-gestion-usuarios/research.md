# Research: Interfaz de Gestión de Usuarios

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-29

El stack del producto lo decidió el responsable del proyecto (2026-09-29): Blazor (C#) en la
interfaz, ASP.NET Core Web API en el servidor, SQL Server con Entity Framework Core y Microsoft
Azure como nube. Este documento resuelve las decisiones técnicas derivadas. No queda ningún
`NEEDS CLARIFICATION` abierto.

---

## R1. Versión de .NET

- **Decision**: .NET 10 (LTS) y C# 14 para todos los proyectos.
- **Rationale**: es la versión con soporte a largo plazo vigente, y la usan tanto ASP.NET Core, como Blazor WebAssembly y EF Core 10. Un ERP necesita una ventana de soporte larga.
- **Alternatives considered**: .NET 9 (STS, su soporte termina antes); la preview de .NET 11 (no apta para producción).

## R2. Modelo de hospedaje de Blazor

- **Decision**: una aplicación **Blazor WebAssembly** independiente que consume la Web API por HTTP y se publica en Azure Static Web Apps.
- **Rationale**: el responsable eligió una Web API separada. Con WASM, la interfaz no tiene acceso a la base de datos ni a la lógica de autorización: toda decisión de permisos y empresa ocurre en la API, que es lo que exige el Principio II. Además, la frontera cliente/servidor queda explícita y se puede probar con contratos.
- **Alternatives considered**: Blazor Server o Blazor Web App con renderizado interactivo en el servidor. Se descartó porque mezcla la interfaz con el proceso del servidor, lo que invita a saltarse la API y a validar permisos en componentes. Además, necesita un circuito SignalR por usuario.

## R3. Contexto de solicitud y aislamiento multiempresa

- **Decision**:
  1. `IContextoSolicitud` (usuario, empresa, permisos) se construye **solo** a partir de los claims del token verificado.
  2. EF Core aplica un **filtro de consulta global** por `EmpresaId` a toda entidad de empresa.
  3. Un `DbConnectionInterceptor` fija `SESSION_CONTEXT('EmpresaId')` (de solo lectura) al abrir cada conexión.
  4. **Row-Level Security** de SQL Server aplica un predicado de filtro y un predicado de bloqueo sobre `Usuarios` y `AuditoriaUsuarios` que usan ese valor.
  5. Si se busca un recurso de otra empresa, la respuesta es `404 usuario-no-encontrado`, idéntica a la de un recurso inexistente.
- **Rationale**: cumple el Principio I y las restricciones de seguridad de la Constitución: la empresa nunca sale de un parámetro del cliente, hay defensa en profundidad y no se revela la existencia de datos de otra empresa. RLS protege incluso ante SQL escrito a mano o un `IgnoreQueryFilters()` accidental.
- **Alternatives considered**: solo filtro de EF Core (una sola capa, frágil); una base de datos por empresa (costosa de operar en Azure SQL con muchas empresas, y las reglas de negocio no lo exigen); pasar `empresaId` en la URL (lo prohíbe la Constitución).
- **Integración con la autenticación**: el proveedor de identidad y el formato del token los define la futura spec de autenticación. Esta feature solo depende de que el token verificado incluya el identificador del usuario, el de su empresa y sus permisos. En Development y en las pruebas automatizadas se usa el esquema `IdentidadPrueba`, que convierte encabezados `X-Kryon-Prueba-*` en esos mismos claims. Solo existe en los entornos `Development` y `Test` y no define la autenticación real.

## R4. Modelo de permisos consumido

- **Decision**: esta feature **consume** permisos que asigna el catálogo de roles (otra feature) y no los define. Los nombres de esta tabla son **provisionales**: son placeholders de contrato, no un catálogo definitivo. Cada política de autorización de la API (`Usuarios.Acceder`, `Usuarios.Registrar`, `Usuarios.Editar`, `Usuarios.Activar`, `Usuarios.Desactivar`) se asocia por **configuración** al nombre de permiso real. Cuando el catálogo de roles defina sus nombres, solo cambia esa configuración:

  | Permiso (nombre provisional) | Uso |
  |------------------------------|-----|
  | `usuarios.administrar` | Acceso a la gestión de usuarios (FR-005). Un **administrador activo** (FR-036) es un usuario Activo cuyo rol incluye este permiso. |
  | `usuarios.registrar` | Registrar usuarios (FR-020). |
  | `usuarios.editar` | Editar nombre y rol (FR-021). |
  | `usuarios.activar` | Activar (FR-030). |
  | `usuarios.desactivar` | Desactivar (FR-030). |

  Cada acción se protege con una política de autorización de ASP.NET Core. La API calcula `accionesPermitidas` para cada usuario del listado y del detalle combinando los permisos con las reglas de autogestión (FR-034 y FR-035) y el estado actual.
- **Rationale**: la historia 5 exige mostrar y denegar acciones una por una. El catálogo y la definición de permisos están fuera de alcance, así que esta feature solo documenta qué **capacidades** necesita y deja sus nombres como configurables. Si falta la asociación de una política, esa política deniega (Principio VI).
- **Alternatives considered**: un único permiso para todo (no permitiría cumplir la historia 5); comparar con nombres de rol fijos (acopla la feature a un catálogo que no le pertenece).
- **Nota de coordinación (no es regla de esta feature)**: la futura feature de catálogo de roles debe garantizar que la definición de administrador y las capacidades necesarias para administrar usuarios sean coherentes, evitando que una empresa conserve administradores activos pero ninguno tenga las capacidades necesarias para gestionar usuarios. FR-036 no cambia.

## R5. Concurrencia

- **Decision**:
  - **Edición y cambio de estado** (FR-038, FR-039, EC-2, EC-3): la columna `Version` (`rowversion`) funciona como token de concurrencia de EF Core. La API la expone como `ETag` y exige `If-Match` en `PUT` y en las acciones de activar y desactivar. Si no coincide, responde `412 usuario-modificado` con los datos actuales.
  - **Regla del último administrador** (FR-036, EC-6): cualquier operación que pueda reducir el número de administradores activos (desactivar o cambiar un rol) toma un bloqueo exclusivo de aplicación `sp_getapplock` con clave `usuarios-admin:{EmpresaId}` dentro de la transacción. Con el bloqueo tomado, cuenta los administradores activos, valida y guarda.
- **Rationale**: el control optimista cubre las ediciones sobre la misma fila. El bloqueo por empresa cubre el caso de dos administradores que se desactivan mutuamente, que modifican filas distintas. El bloqueo se limita a una empresa y a esas operaciones, así que no afecta a otras empresas.
- **Alternatives considered**: el nivel de aislamiento `SERIALIZABLE` en toda la transacción (bloquea rangos de forma más amplia y es más difícil de razonar); "gana el último" (descartado en clarify); comprobar solo en la aplicación sin bloqueo (tiene una condición de carrera demostrable).

## R6. Listado: búsqueda, filtros y paginación

- **Decision**: paginación por desplazamiento (`pagina`, `tamanoPagina`) con `total` de coincidencias, hecha en el servidor. Tamaño de página: **decisión técnica provisional y configurable** (`Usuarios:Paginacion`), inicialmente 25 por defecto y 100 como máximo. No es un requisito de negocio: la spec dice explícitamente que no lo define. Búsqueda parcial parametrizada (`LIKE` con escape de comodines) sobre `NombreCompleto` e `IdentificadorAcceso`, sin distinguir mayúsculas y minúsculas mediante una intercalación `_CI_AI`. Filtros por `rolId` y `estado`, combinados con AND. Índices `(EmpresaId, NombreCompleto)` y `(EmpresaId, IdentificadorAcceso)`. El orden por defecto es por nombre completo, con el `Id` como criterio de desempate para que el orden sea estable.
- **Rationale**: cumple FR-014 a FR-018. El total y los resultados salen de la misma consulta ya filtrada por empresa (FR-016, SC-008). La paginación por desplazamiento permite indicar el total y tiene controles de anterior y siguiente que se operan bien con teclado.
- **Alternatives considered**: paginación por cursor (no da un total fácilmente y la spec pide indicarlo); búsqueda de texto completo (desproporcionada para dos campos cortos).

## R7. Trazabilidad

- **Decision**: la tabla `AuditoriaUsuarios` es de **solo inserción** y se escribe en la **misma transacción** que la operación. El rol de base de datos de la aplicación tiene `INSERT` y `SELECT` sobre ella, y `UPDATE` y `DELETE` denegados. Los cambios se guardan en JSON (`[{campo, anterior, nuevo}]`) solo en las modificaciones, y únicamente con los campos que cambiaron (FR-044). Toda operación sobre un id que resulta `usuario-no-encontrado` se registra como `UsuarioNoAccesible` (FR-046), con el identificador solicitado, la operación solicitada, sin datos del recurso y bajo la empresa del **actor**. Con RLS no se puede saber si el id pertenece a otra empresa o no existe, y el registro no lo infiere. La hora se guarda como `datetimeoffset` en UTC.
- **Rationale**: cumple el Principio V (actor, momento, empresa, entidad y tipo de operación; inalterable) y FR-043 a FR-046. Escribir en la misma transacción garantiza que no haya operación sin registro ni registro sin operación (SC-002), y las cancelaciones no generan registro (FR-032).
- **Alternatives considered**: Temporal Tables de SQL Server (guardan versiones, pero no el actor ni el tipo de operación sin columnas extra, y la tabla de historial sí se puede purgar); enviar eventos a un servicio externo (añade una dependencia sin necesidad para esta feature).

## R8. Puntos de integración para las dependencias externas (DEP-1 a DEP-5)

- **Decision**: la spec las declara externas, así que el plan **no las resuelve ni define su comportamiento**. Solo define interfaces en `Kryon.Core/Usuarios` que expresan lo que esta feature necesita preguntar:
  - `IPoliticaAltaUsuario` (DEP-2): dado un alta válida, devuelve el estado inicial y se encarga de la credencial inicial. Qué estado y qué mecanismo lo decide la futura spec de autenticación.
  - `IPoliticaIdentificadorAcceso` (DEP-3, DEP-4): `ValidarFormato`, `EstaEnUso` y `PermiteEdicion`. `EstaEnUso` devuelve solo sí o no, así que la firma impide revelar en qué empresa existe un duplicado (FR-027, Principio I). El ámbito de la unicidad, el formato y la editabilidad los decide la futura spec de autenticación.
  - `IFuenteLimiteUsuarios` (DEP-5): informa si existe un límite aplicable a la empresa y si se alcanzó. Esta feature aplica solo la regla de FR-028: si existe y se alcanzó, rechaza el registro. La existencia del límite, su cifra y la forma de contar se deciden fuera de esta feature.
  - DEP-1: sin interfaz. La desactivación publica el evento de dominio `UsuarioDesactivado` y la spec de autenticación decidirá si se suscribe.
- **Consecuencia**: el registro de usuarios y la edición del identificador dependen de estas decisiones externas. Se diseñan (casos de uso, contratos de API y de interfaz) y se prueban con dobles de prueba de cada interfaz, pero su **implementación real y su entrega quedan bloqueadas** hasta que DEP-2 a DEP-5 estén definidas. El plan no elige ningún comportamiento sustituto mientras tanto.
- **Rationale**: respeta la spec (las dependencias no se convierten en reglas nuevas) y el Principio III (las ambigüedades se resuelven en la spec correspondiente, no durante la codificación). Con los puntos de integración, conectar las decisiones externas no obliga a rehacer esta feature.
- **Alternatives considered**: fijar valores sustitutos, como un estado inicial, un ámbito de unicidad, un identificador de solo lectura o "sin límite" (convertiría dependencias externas en reglas de negocio no aprobadas); aplazar toda la historia 4 (retrasaría también la edición de nombre y rol, que no depende de la autenticación).

## R9. Errores y mensajes

- **Decision**: todas las respuestas de error usan **ProblemDetails (RFC 9457)**, con un `type` estable (`https://kryon.app/errores/{codigo}`), un `title` en español comprensible y, en validación, `errors` por campo. Nunca incluyen trazas, nombres de tablas ni datos de otra empresa. La interfaz traduce cada `codigo` a un mensaje que dice qué acción no se hizo, por qué y qué puede hacer el administrador (FR-040 y FR-041). La lista completa de códigos está en [contracts/usuarios-api.openapi.yaml](contracts/usuarios-api.openapi.yaml).
- **Rationale**: los códigos estables permiten probar SC-004 y mantener los mensajes en un único lugar.
- **Alternatives considered**: mensajes libres generados en el servidor (difíciles de probar y de mantener coherentes).

## R10. Accesibilidad y pruebas de interfaz

- **Decision**: WCAG 2.2 AA como objetivo. Se usan componentes nativos (`<table>`, `<button>`, `<label>`, `<dialog>`) antes que ARIA. Hay una región `aria-live="polite"` para los resultados y el éxito, y `role="alert"` para los errores. El diálogo de confirmación retiene el foco y lo devuelve al botón que lo abrió. Se prueba en tres niveles: bUnit (nombres accesibles, acciones visibles, foco), Playwright solo con teclado para los flujos completos, y axe-core (`Deque.AxeCore.Playwright`) sin violaciones serias ni críticas. Además hay una revisión manual con lector de pantalla antes de cerrar la feature.
- **Rationale**: son los estándares de accesibilidad de la Constitución y FR-019, FR-029 y FR-047 a FR-050 (SC-003). Las herramientas automáticas no cubren todo, por eso se añade la revisión manual.
- **Alternatives considered**: una biblioteca de componentes de terceros (MudBlazor, Radzen). Se pospone: no es necesaria para esta feature y su accesibilidad varía según el componente. Se reevaluará cuando Kryon tenga más pantallas.

## R11. Estrategia de pruebas

- **Decision**:
  - `Kryon.Core.Tests`: reglas de dominio (autogestión, último administrador, transiciones de estado, validación) y casos de uso con repositorios en memoria.
  - `Kryon.Api.Tests`: `WebApplicationFactory` + Testcontainers con SQL Server real (para que RLS, `rowversion` y `sp_getapplock` se prueben de verdad), el handler de autenticación de prueba y datos semilla de las empresas A y B. Incluye pruebas de casos denegados para cada endpoint y pruebas de concurrencia con solicitudes en paralelo.
  - `Kryon.Web.Tests`: bUnit.
  - `Kryon.E2E.Tests`: Playwright + axe.
- **Rationale**: cumple el Principio IV (casos permitidos y denegados) y la puerta de calidad del aislamiento A/B. SQLite o el proveedor en memoria no soportan RLS ni `sp_getapplock`, por eso la integración usa SQL Server real.
- **Alternatives considered**: el proveedor InMemory de EF Core (no reproduce ni las restricciones ni la concurrencia).

## R12. Despliegue en Azure

- **Decision**:
  - La API va en Azure App Service (Linux) y usa Managed Identity para conectarse a Azure SQL Database, sin contraseñas en la configuración.
  - La interfaz va en Azure Static Web Apps.
  - Los secretos restantes van en Azure Key Vault.
  - Los registros y el monitoreo van a Application Insights, sin datos personales en los mensajes, sin tokens y sin cuerpos de solicitud.
  - Las migraciones de EF Core se aplican como paso explícito del pipeline, con un script idempotente y revisado, nunca al arrancar la aplicación.
- **Rationale**: son los servicios administrados que corresponden a la nube elegida, y cumplen la regla "secretos fuera del repositorio y de los registros".
- **Alternatives considered**: Azure Container Apps (válido, pero todavía no hay necesidad de contenedores en producción); migrar al arrancar (riesgoso con varias instancias).
