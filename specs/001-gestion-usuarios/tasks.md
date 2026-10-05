---

description: "Tareas de implementación de la feature 001-gestion-usuarios"
---

# Tasks: Interfaz de Gestión de Usuarios

**Input**: Design documents from `specs/001-gestion-usuarios/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: SE INCLUYEN. La Constitución exige que cada criterio de aceptación tenga al menos una prueba, que se prueben los casos denegados y que haya una prueba de aislamiento entre empresas (Principio IV y *Puertas de calidad*). Las pruebas de cada historia se escriben primero y deben fallar antes de implementar.

**Organization**: tareas agrupadas por historia de usuario (US1 a US6 de la spec).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos y sin dependencias pendientes).
- **[Story]**: historia de usuario de la spec (US1 a US6).
- **⛔ BLOQUEADA (DEP-n)**: depende de una decisión externa (futura spec de autenticación o decisión comercial). **No se inicia** hasta que esa dependencia esté definida en su spec. El plan exige marcarlas así (plan.md, *Dependencias externas*).

## Path Conventions

Solución .NET única (plan.md, *Project Structure*): `src/Kryon.Api`, `src/Kryon.Core`,
`src/Kryon.Infrastructure`, `src/Kryon.Contracts`, `src/Kryon.Web` y `tests/Kryon.*.Tests`.

## Valores provisionales (recordatorio)

Tamaño de página (25 / máximo 100), longitud máxima del nombre (200), longitud máxima del
identificador (256) y los nombres de permisos (`usuarios.administrar`, `usuarios.registrar`,
`usuarios.editar`, `usuarios.activar`, `usuarios.desactivar`) son **decisiones técnicas
provisionales y configurables**, no requisitos de negocio. Toda tarea que los use debe leerlos de
configuración, nunca escribirlos como constantes en la lógica.

## Feature / release gating (mecanismo de entrega, no regla de negocio)

Mientras DEP-2 a DEP-5 no estén definidas, algunas funciones **no se entregan**. Esto es un
mecanismo técnico de entrega (feature gating) y **no** crea criterios nuevos en la spec: no dice
que el registro "no esté permitido" ni que el identificador "no sea editable". Solo indica que esas
funciones todavía no forman parte del producto desplegado.

| Gate | Se abre cuando | Mientras está cerrado |
|------|----------------|------------------------|
| `RegistroUsuarios` | Hay implementaciones registradas de `IPoliticaAltaUsuario` (DEP-2), `IPoliticaIdentificadorAcceso` (DEP-3) e `IFuenteLimiteUsuarios` (DEP-5). En pruebas, con los dobles de T034; en producción, con T094. | `POST /api/usuarios` no se mapea, `puedeRegistrar = false` y el botón "Nuevo usuario" no aparece. |
| `EdicionIdentificador` | Existe la implementación real de `PermiteEdicion` (T080, DEP-4). | `acciones.editarIdentificador = false` y la edición no acepta `identificadorAcceso`. |

Cuando un gate se abre, el comportamiento pasa a ser el que definan esas specs. Esta feature no
fija ninguna regla sustituta.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: crear la solución y las herramientas base.

- [X] T001 Crear `Kryon.sln` y los proyectos `net10.0`: `src/Kryon.Api` (ASP.NET Core Web API), `src/Kryon.Core` (classlib), `src/Kryon.Infrastructure` (classlib), `src/Kryon.Contracts` (classlib), `src/Kryon.Web` (Blazor WebAssembly standalone), `tests/Kryon.Core.Tests`, `tests/Kryon.Api.Tests`, `tests/Kryon.Web.Tests` y `tests/Kryon.E2E.Tests` (los cuatro con **xUnit v3**). No usar ninguna plantilla que genere xUnit v2 (por ejemplo, la genérica `dotnet new xunit`): usar la plantilla oficial de xUnit v3 o un `.csproj` equivalente. Según la configuración oficial de xUnit v3, los proyectos de prueba pueden ser ejecutables (`OutputType Exe`). Referencias: Api → Core, Infrastructure, Contracts; Infrastructure → Core; Web → Contracts; Core **no** referencia Infrastructure, Api ni Web.
- [X] T002 Crear `Directory.Build.props` en la raíz con `TargetFramework net10.0`, `LangVersion 14`, `Nullable enable`, `TreatWarningsAsErrors true` y los analizadores de .NET habilitados.
- [X] T003 [P] Crear `.editorconfig` en la raíz con las convenciones de C# y verificar que `dotnet format --verify-no-changes` pasa en la solución vacía.
- [X] T004 [P] Añadir paquetes NuGet. En los 4 proyectos de prueba (`tests/Kryon.Core.Tests/Kryon.Core.Tests.csproj`, `tests/Kryon.Api.Tests/Kryon.Api.Tests.csproj`, `tests/Kryon.Web.Tests/Kryon.Web.Tests.csproj`, `tests/Kryon.E2E.Tests/Kryon.E2E.Tests.csproj`): `xunit.v3`, `Microsoft.NET.Test.Sdk` y `xunit.runner.visualstudio` (xUnit v3 en modo VSTest). Además: `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.EntityFrameworkCore.Design` en `src/Kryon.Infrastructure/Kryon.Infrastructure.csproj`, y el manifiesto de herramientas locales `.config/dotnet-tools.json` con `dotnet-ef` fijado a la **misma versión** que esos dos paquetes de EF Core (se instala con `dotnet tool restore`; no se usa ninguna instalación global de `dotnet-ef`); `Microsoft.AspNetCore.Mvc.Testing` y `Testcontainers.MsSql` en `tests/Kryon.Api.Tests/Kryon.Api.Tests.csproj`; `bunit` en `tests/Kryon.Web.Tests/Kryon.Web.Tests.csproj`; `Microsoft.Playwright`, `Deque.AxeCore.Playwright` y `Testcontainers.MsSql` en `tests/Kryon.E2E.Tests/Kryon.E2E.Tests.csproj` (T100 gestiona su propio contenedor). Añadir también las referencias de proyecto: `Kryon.Core.Tests → Kryon.Core`, `Kryon.Api.Tests → Kryon.Api` (Core, Infrastructure y Contracts llegan de forma transitiva) y `Kryon.Web.Tests → Kryon.Web` (Contracts llega de forma transitiva); sin referencias redundantes. `Kryon.E2E.Tests` **no** tiene referencias de proyecto (pruebas de caja negra, research §R11). La plantilla de xUnit v3 en modo VSTest usada en T001 generó el paquete `xunit.v3.mtp-off`: antes de fijar paquetes y versiones, verificar cuál es la combinación oficial compatible con xUnit v3 + VSTest. Antes de fijar versiones, verificar que las versiones de `bunit` y `Microsoft.Playwright` son compatibles con xUnit v3. `Testcontainers.MsSql` y `Deque.AxeCore.Playwright` no cambian.
- [X] T005 [P] Crear el pipeline de CI en `.github/workflows/ci.yml`: `dotnet build`, `dotnet format --verify-no-changes` y `dotnet test` para los 4 proyectos de prueba (xUnit v3 con runner VSTest; sin configuración MTP ni `global.json` para pruebas) en un runner Linux con Docker (lo necesita Testcontainers). **Proveedor CI provisional: GitHub Actions por estar el repositorio en GitHub. Debe confirmarse con el estándar del equipo antes de considerarlo una decisión transversal de Kryon.**

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: contexto de seguridad, aislamiento multiempresa, persistencia, trazabilidad, contratos y bases de prueba que usan todas las historias.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta completar esta fase.

### Seguridad y contexto

- [X] T006 Crear la interfaz `IContextoSolicitud` (`UsuarioId`, `EmpresaId`, `Capacidades`) en `src/Kryon.Core/Seguridad/IContextoSolicitud.cs`. Es la **única** fuente de empresa y actor para toda la feature (FR-001, research §R3).
- [X] T007 Implementar `ContextoSolicitudDesdeClaims` en `src/Kryon.Api/Seguridad/ContextoSolicitud.cs`: construye `IContextoSolicitud` **solo** con los claims del token verificado. Si falta el claim de usuario o de empresa, la solicitud se deniega con 401 `no-autenticado` (Principio VI). Nunca lee la empresa de la ruta, la query ni el cuerpo. T007 aporta la señal de fallo cerrado (`TryCrear` devuelve `false` y no se construye ningún contexto); la respuesta HTTP 401 `no-autenticado` la producen T028 (registro en el pipeline) y T029 (ProblemDetails), y la verifica T036.
- [X] T008 Implementar las políticas de autorización `Usuarios.Acceder`, `Usuarios.Registrar`, `Usuarios.Editar`, `Usuarios.Activar` y `Usuarios.Desactivar` en `src/Kryon.Api/Seguridad/PoliticasUsuarios.cs`. Cada política se asocia a un nombre de permiso leído de la sección de configuración `Seguridad:Permisos` de `src/Kryon.Api/appsettings.json`, con los valores **provisionales** `usuarios.administrar`, `usuarios.registrar`, `usuarios.editar`, `usuarios.activar` y `usuarios.desactivar` (research §R4). Si falta la asociación de una política, esa política **deniega** (403 `sin-permiso`). T008 aporta la decisión de autorización fail-closed; la integración de esa denegación como respuesta HTTP 403 `sin-permiso` corresponde a T028/T029 y se verifica en T036.
- [X] T009 Crear el esquema de autenticación `IdentidadPrueba` **exclusivo para Development y pruebas automatizadas** en `src/Kryon.Api/Seguridad/IdentidadPrueba.cs`. Lee los encabezados `X-Kryon-Prueba-UsuarioId`, `X-Kryon-Prueba-EmpresaId` y `X-Kryon-Prueba-Capacidades` (lista separada por comas) y los convierte en los **mismos claims** que consume `ContextoSolicitudDesdeClaims` (T007), así que alimenta el mismo `IContextoSolicitud` que usará la autenticación real. Su registro condicional en `src/Kryon.Api/Program.cs` lo hace T028: se registra **solo** si el entorno es `Development` o `Test`. En cualquier otro entorno no se registra y los encabezados se ignoran (Principio VI). No define ni sustituye el mecanismo de autenticación real de Kryon, que corresponde a la futura spec de autenticación, y no resuelve ninguna de sus dependencias.
- [X] T010 Añadir en `src/Kryon.Api/Program.cs` una política CORS **solo para Development y Test** que permita al origen local de `Kryon.Web` enviar los encabezados `X-Kryon-Prueba-*`. Fuera de esos entornos, la política no existe.

### Dominio y contratos compartidos

- [X] T011 [P] Crear el enum `EstadoUsuario` (`Activo = 1`, `Inactivo = 2`) y la entidad `Usuario` en `src/Kryon.Core/Usuarios/Usuario.cs`. Campos según data-model.md: `Id` (uniqueidentifier, lo genera el servidor); `EmpresaId` ("Se toma **siempre** de `IContextoSolicitud`… Es inmutable"); `NombreCompleto` ("Se recortan los espacios y no puede quedar vacío"; longitud máxima provisional y configurable); `IdentificadorAcceso` (obligatorio; longitud máxima provisional y configurable; formato y unicidad **no** se validan aquí porque dependen de DEP-3); `RolId`; `Estado`; `Version` (token de concurrencia); `CreadoEn` y `ModificadoEn` (UTC). **No** exponer ningún método de borrado (FR-033).
- [X] T012 [P] Crear el modelo de lectura `RolReferencia` (`Id`, `EmpresaId`, `Nombre`, `Capacidades`) en `src/Kryon.Core/Usuarios/RolReferencia.cs`. Es de solo lectura, porque el catálogo de roles está fuera de alcance.
- [X] T013 [P] Crear la entidad `AuditoriaUsuario` y el enum `TipoOperacion` (`Alta`, `Modificacion`, `Activacion`, `Desactivacion`, `UsuarioNoAccesible`) en `src/Kryon.Core/Usuarios/AuditoriaUsuario.cs`. Campos: `Id` (bigint identity), `EmpresaId` ("La empresa del **actor**"), `ActorUsuarioId`, `OcurridoEn` ("UTC, asignado por el servidor"), `TipoOperacion`, `UsuarioAfectadoId` (en `UsuarioNoAccesible`, el identificador **solicitado**), `OperacionSolicitada` (solo en `UsuarioNoAccesible`: `Consulta`, `Edicion`, `Activacion` o `Desactivacion`) y `CambiosJson` ("Solo en `Modificacion`… únicamente con los campos que cambiaron"). Sin métodos de modificación. `UsuarioNoAccesible` **no** afirma que el recurso pertenezca a otra empresa.
- [X] T014 [P] Crear `CodigoErrorUsuarios` con los 13 códigos funcionales/de dominio de la Gestión de Usuarios (`no-autenticado`, `sin-permiso`, `usuario-no-encontrado`, `validacion`, `rol-no-valido`, `identificador-en-uso`, `identificador-no-editable`, `limite-usuarios-alcanzado`, `no-puede-desactivarse-a-si-mismo`, `no-puede-cambiar-su-propio-rol`, `ultimo-administrador`, `usuario-modificado`, `falta-version`) y la excepción de dominio `ErrorUsuarios` en `src/Kryon.Core/Usuarios/ErrorUsuarios.cs`. `error-interno` **no** pertenece a `CodigoErrorUsuarios`: lo genera exclusivamente la capa API mediante el manejador global de T030. El contrato OpenAPI tiene 14 códigos en total (estos 13 más `error-interno`).
- [X] T015 [P] Crear los DTOs del contrato en `src/Kryon.Contracts/Usuarios/`: `UsuarioResumen.cs`, `DetalleUsuario.cs`, `PaginaUsuarios.cs`, `AccionesPermitidas.cs`, `RolResumen.cs`, `RegistrarUsuarioSolicitud.cs`, `EditarUsuarioSolicitud.cs`, `Problema.cs` y `CodigosError.cs`, exactamente como los esquemas de `contracts/usuarios-api.openapi.yaml`. **Ningún** DTO de entrada tiene `empresaId`.
- [X] T016 [P] Crear las interfaces de los puntos de integración externos en `src/Kryon.Core/Usuarios/Integraciones/`: `IPoliticaAltaUsuario.cs` (DEP-2: devuelve el estado inicial y gestiona la credencial inicial), `IPoliticaIdentificadorAcceso.cs` (DEP-3/DEP-4: `ValidarFormato`, `EstaEnUso` que **solo devuelve bool**, y `PermiteEdicion`) e `IFuenteLimiteUsuarios.cs` (DEP-5: informa si existe un límite aplicable y si se alcanzó). Además, en `src/Kryon.Core/Usuarios/`, las interfaces que consumen los casos de uso: `IConsultaUsuarios.cs`, `IRepositorioUsuarios.cs`, `IBloqueoAdministradoresEmpresa.cs`, `IRegistroAuditoria.cs` e `IUnidadDeTrabajo.cs`. Sus firmas usan solo tipos de `Kryon.Core`: nunca clases de `Kryon.Infrastructure` ni DTOs de `Kryon.Contracts`. Solo interfaces y documentación XML, **sin implementaciones** (research §R8 y §R11).
- [X] T017 [P] Crear el evento de dominio `UsuarioDesactivado` (UsuarioId, EmpresaId, OcurridoEn) y la interfaz `IPublicadorEventos` en `src/Kryon.Core/Usuarios/Eventos/UsuarioDesactivado.cs`. Es el punto de integración de DEP-1: esta feature no se suscribe al evento.
- [X] T018 Crear `OpcionesUsuarios` (sección `Usuarios`: `Paginacion:TamanoPorDefecto = 25`, `Paginacion:TamanoMaximo = 100`, `Validacion:LongitudMaximaNombre = 200`, `Validacion:LongitudMaximaIdentificador = 256`, todos **provisionales**) en `src/Kryon.Core/Usuarios/OpcionesUsuarios.cs`, con valores en `src/Kryon.Api/appsettings.json` y validación al arrancar.
- [X] T019 Implementar `CalculadoraAcciones` en `src/Kryon.Core/Usuarios/CalculadoraAcciones.cs` con la tabla de data-model.md: `verDetalle` si hay acceso; `editar` con la capacidad de editar; `cambiarRol` = `editar` y el usuario no es el actor; `activar` con la capacidad de activar y el usuario Inactivo; `desactivar` con la capacidad de desactivar, el usuario Activo y que no sea el actor. La regla del último administrador **no** oculta la acción. `editarIdentificador` se resuelve con el gate `EdicionIdentificador` (ver *Feature / release gating*): mientras el gate esté cerrado, vale `false` porque la función no está entregada. Calcula también `esCuentaPropia`.

### Persistencia, aislamiento y trazabilidad

- [X] T020 Crear `KryonDbContext` y la configuración de `Usuario` en `src/Kryon.Infrastructure/Persistencia/KryonDbContext.cs` y `src/Kryon.Infrastructure/Persistencia/Configuraciones/UsuarioConfiguracion.cs`: `NombreCompleto` "nvarchar(200)" e `IdentificadorAcceso` "nvarchar(256)" (tamaños provisionales), intercalación `_CI_AI` en ambos, `Version` como `rowversion` / `IsRowVersion()`, índices `IX (EmpresaId, NombreCompleto, Id)`, `IX (EmpresaId, IdentificadorAcceso)` e `IX (EmpresaId, Estado, RolId)`, **sin** índice único del identificador (DEP-3). Implementar también `UnidadDeTrabajo` (implementa `IUnidadDeTrabajo` de T016: inicia, confirma y revierte la transacción de `KryonDbContext`) en `src/Kryon.Infrastructure/Persistencia/UnidadDeTrabajo.cs`.
- [X] T021 [P] Crear las configuraciones de `RolReferencia` (solo lectura) y de `AuditoriaUsuario` (`CambiosJson` "nvarchar(max), null", `OperacionSolicitada` null) en `src/Kryon.Infrastructure/Persistencia/Configuraciones/RolReferenciaConfiguracion.cs` y `src/Kryon.Infrastructure/Persistencia/Configuraciones/AuditoriaUsuarioConfiguracion.cs`.
- [X] T022 Añadir el filtro de consulta global por `EmpresaId` (tomado de `IContextoSolicitud`) a `Usuario`, `RolReferencia` y `AuditoriaUsuario` en `src/Kryon.Infrastructure/Persistencia/KryonDbContext.cs` (FR-001, research §R3, primera capa).
- [X] T023 Implementar `ContextoEmpresaInterceptor` (`DbConnectionInterceptor`) en `src/Kryon.Infrastructure/Persistencia/ContextoEmpresaInterceptor.cs`: al abrir cada conexión ejecuta `sp_set_session_context @key=N'EmpresaId', @value=…, @read_only=1` con la empresa de `IContextoSolicitud` (research §R3, segunda capa).
- [X] T024 Primera tarea que usa `dotnet ef`, siempre como herramienta local (T004). (A) Crear `KryonDbContextFactoriaDiseno` (`IDesignTimeDbContextFactory<KryonDbContext>`) en `src/Kryon.Infrastructure/Persistencia/KryonDbContextFactoriaDiseno.cs`, **solo para el tooling de EF Core**: no se registra en DI, no depende de `Kryon.Api`, no ejecuta operaciones de tenant y no es una vía para saltarse el aislamiento; si para construir el modelo necesita un `IContextoSolicitud`, usa una implementación de diseño aislada dentro de esa factoría, inaccesible en ejecución. (B) Crear la migración inicial de esquema (tablas `Usuarios`, `Roles` de referencia y `AuditoriaUsuarios`, con los índices y columnas de T020 y T021) en `src/Kryon.Infrastructure/Persistencia/Migraciones/` con: `dotnet tool restore` y `dotnet ef migrations add Inicial --project src/Kryon.Infrastructure --startup-project src/Kryon.Infrastructure --output-dir Persistencia/Migraciones`. (C) Crear el **generador** `scripts/generar-migracion.ps1`: ejecuta `dotnet tool restore` y `dotnet ef migrations script --idempotent --project src/Kryon.Infrastructure --startup-project src/Kryon.Infrastructure` hacia `artifacts/sql/kryon-migraciones-idempotente.sql` (carpeta ignorada por git), termina con error si la generación falla, no aplica el SQL a ninguna base de datos, no arranca la API y no contiene secretos ni cadenas de conexión reales. El SQL generado no se versiona: el generador es la fuente reproducible.
- [X] T025 Crear la migración `SeguridadFilas` en `src/Kryon.Infrastructure/Persistencia/Migraciones/` con la herramienta local y el mismo comando de T024 (`dotnet ef migrations add SeguridadFilas …`), sin `dotnet-ef` global, con SQL explícito: una función de predicado y una `SECURITY POLICY` de RLS con predicados de **filtro** y de **bloqueo** sobre `Usuarios` y `AuditoriaUsuarios` basados en `SESSION_CONTEXT('EmpresaId')` (research §R3).
- [X] T026 Crear la migración `PermisosAuditoria` en `src/Kryon.Infrastructure/Persistencia/Migraciones/` con la herramienta local y el mismo comando de T024, sin `dotnet-ef` global, con SQL explícito: un rol de base de datos de la aplicación con `INSERT` y `SELECT` sobre `AuditoriaUsuarios` y **`DENY UPDATE, DELETE`** sobre ella (FR-045, research §R7). Al terminar, verificar que `scripts/generar-migracion.ps1` genera sin errores un script idempotente que incluye `Inicial`, `SeguridadFilas` y `PermisosAuditoria`.
- [X] T027 Implementar `RegistroAuditoria` (implementa `IRegistroAuditoria` de T016) en `src/Kryon.Infrastructure/Usuarios/RegistroAuditoria.cs`: añade el registro de auditoría en la **misma transacción** que la operación. `CambiosJson` sigue el formato `[{"campo":…,"anterior":…,"nuevo":…}]` solo con los campos que cambiaron y solo en `Modificacion` (FR-043, FR-044). Nunca incluye credenciales ni tokens.
- [X] T028 Registrar Core, Infrastructure, seguridad, opciones e interceptor en `src/Kryon.Api/Program.cs`, con la cadena de conexión desde configuración (User Secrets en desarrollo, Key Vault en Azure): `IContextoSolicitud` (desde los claims verificados, T007), `ContextoEmpresaInterceptor` y `KryonDbContext` por solicitud, las implementaciones de Infrastructure de las interfaces de Core de T016 que ya existen en esta fase (`UnidadDeTrabajo` y `RegistroAuditoria`), `OpcionesUsuarios` (T018), las políticas de autorización (T008), `IdentidadPrueba` **solo** en Development y Test (T009) y el CORS de T010. **Sin** registrar implementaciones de los puntos de integración DEP-2, DEP-3, DEP-4 y DEP-5, y sin aplicar migraciones al arrancar (research §R12). Las implementaciones de Infrastructure que aún no existen se registran en la tarea que las crea: `ConsultaUsuarios` en T048, `BloqueoAdministradoresEmpresa` en T063 y `RepositorioUsuarios` en T064.

### Errores de la API

- [X] T029 Implementar el mapeo de `ErrorUsuarios` a ProblemDetails (RFC 9457) en `src/Kryon.Api/Usuarios/MapeoErrores.cs`: `type` = `https://kryon.app/errores/{codigo}`, `title` en español, `codigo`, `errors` por campo en validación y `actual` en `usuario-modificado`. Los estados HTTP son los del contrato (400, 401, 403, 404, 409, 412, 428). Nunca incluye trazas, nombres de tablas ni datos de otra empresa (FR-041).
- [X] T030 Implementar el manejador global de excepciones inesperadas `ManejadorExcepcionesInesperadas` (`IExceptionHandler`) en `src/Kryon.Api/Errores/ManejadorExcepcionesInesperadas.cs` y registrarlo con `UseExceptionHandler` en `src/Kryon.Api/Program.cs`. Ante cualquier excepción no controlada responde `500` con la respuesta común `ErrorInterno` del contrato: ProblemDetails **genérico** con `type` = `https://kryon.app/errores/error-interno`, `title` fijo en español, `codigo` = `error-interno` y sin `detail` técnico. El cuerpo **no** incluye stack traces, nombres de clases o tipos internos, consultas SQL, rutas del servidor ni ningún otro detalle técnico, en ningún entorno. El detalle se envía solo al log del servidor, sin secretos. Desactivar `DeveloperExceptionPage` para la API (FR-041, SC-004).
- [X] T031 Implementar el filtro de endpoint `RegistroUsuarioNoAccesible` en `src/Kryon.Api/Usuarios/RegistroUsuarioNoAccesible.cs`: cuando un endpoint con `{id}` termina en `usuario-no-encontrado`, inserta con `RegistroAuditoria` un registro `UsuarioNoAccesible` con el actor, la empresa **actual** (la del actor), el id solicitado, la `OperacionSolicitada` y el momento (FR-046). No consulta ni infiere la empresa propietaria del recurso: con RLS, un usuario de otra empresa y uno inexistente son indistinguibles (Principio I). La respuesta 404 no cambia.

### Bases de prueba

- [X] T032 Crear `KryonApiFactory` (`WebApplicationFactory<Program>` con el entorno `Test` y un contenedor `MsSqlContainer` de Testcontainers, que aplica las migraciones con `Database.Migrate` **solo dentro de este arnés de pruebas**: no forma parte del arranque normal de `Kryon.Api`, no cambia la regla de producción de research §R12 y no requiere `dotnet-ef`) en `tests/Kryon.Api.Tests/Infraestructura/KryonApiFactory.cs`. Añadir el ayudante `ClienteComo(usuarioSemilla, capacidades?)` en `tests/Kryon.Api.Tests/Infraestructura/IdentidadPruebaCliente.cs`: devuelve un `HttpClient` con los encabezados `X-Kryon-Prueba-UsuarioId`, `X-Kryon-Prueba-EmpresaId` y `X-Kryon-Prueba-Capacidades` del esquema `IdentidadPrueba` (T009). **Todas** las pruebas de API se autentican solo mediante este ayudante.
- [X] T033 Crear los datos semilla de quickstart.md en `tests/Kryon.Api.Tests/Infraestructura/SemillaDatos.cs`. **Empresa A**: `ana` (administradora con todas las capacidades), `beto` (administrador), `carla` (sin capacidades de gestión) y `dario` (Inactivo). **Empresa B**: `berta` (administradora) y "Ana Beta". Un rol de A sin la capacidad de desactivar.
- [X] T034 [P] Crear los dobles de prueba configurables de `IPoliticaAltaUsuario`, `IPoliticaIdentificadorAcceso` e `IFuenteLimiteUsuarios` en `tests/Kryon.Api.Tests/Infraestructura/DoblesIntegraciones.cs`. Documentar en el archivo que **no** representan reglas reales (quickstart, prerrequisitos).
- [X] T035 [P] Escribir la prueba de RLS directa (quickstart V2) en `tests/Kryon.Api.Tests/Aislamiento/RlsTests.cs`: con `SESSION_CONTEXT` de A y sin filtro de EF Core, una consulta SQL solo devuelve filas de A, y un INSERT con `EmpresaId` de B es bloqueado. También verifica que el rol de la aplicación no puede hacer `UPDATE` ni `DELETE` sobre `AuditoriaUsuarios`.
- [X] T036 [P] Escribir las pruebas del contexto y de las políticas en `tests/Kryon.Api.Tests/Seguridad/ContextoYPoliticasTests.cs`: identidad de prueba sin `EmpresaId` → 401; política sin asociación configurada → 403; un `empresaId` enviado en query o cuerpo se ignora. Con una factoría en entorno `Production`, los encabezados `X-Kryon-Prueba-*` se ignoran y la solicitud responde 401 (el esquema `IdentidadPrueba` no existe fuera de Development y Test).
- [X] T037 [P] Escribir la prueba de excepción inesperada en `tests/Kryon.Api.Tests/Errores/ExcepcionInesperadaTests.cs`. Mediante un `IStartupFilter` registrado **solo en el ensamblado de pruebas**, mapear un endpoint de prueba que lanza una excepción cuyo mensaje contiene una consulta SQL, una ruta de archivo del servidor y el nombre de un tipo interno. Verificar: `500`; cuerpo ProblemDetails genérico de T030; el cuerpo no contiene `at ` ni ningún marco de stack trace, ni la consulta SQL, ni la ruta, ni el nombre del tipo, ni el mensaje original (FR-041, SC-004).

### Base de la interfaz Blazor

- [X] T038 Configurar `src/Kryon.Web/Program.cs` (HttpClient con la URL base de la API desde configuración) y crear el cliente tipado **público** `ClienteUsuarios`, que recibe `HttpClient` por constructor, en `src/Kryon.Web/Usuarios/ClienteUsuarios.cs` (sin una interfaz creada solo para las pruebas). Todas las operaciones del contrato, con `If-Match` en PUT, desactivación y activación, y lectura de `ETag` y ProblemDetails. `ClienteUsuarios` no conoce ningún mecanismo de identidad.
- [X] T039 Crear `IdentidadPruebaHandler` (`DelegatingHandler`) en `src/Kryon.Web/Desarrollo/IdentidadPruebaHandler.cs`: añade los encabezados `X-Kryon-Prueba-*` con el usuario, la empresa y las capacidades de `wwwroot/appsettings.Development.json`. Se registra en `src/Kryon.Web/Program.cs` **solo** si `IWebAssemblyHostEnvironment.IsDevelopment()`; en otros entornos no existe. No representa la autenticación real de Kryon.
- [X] T040 [P] Crear el componente de mensajes con una región `aria-live="polite"` para éxito y resultados y `role="alert"` para errores, siempre con texto (FR-050), en `src/Kryon.Web/Compartido/Mensajes.razor`.
- [X] T041 [P] Crear `TextosErrores` con los textos exactos de la tabla *Mensajes* de `contracts/ui-usuarios.md`, uno por código de error, incluido `error-interno` ("No se pudo completar la acción por un error interno. Vuelve a intentarlo.") y el caso sin respuesta o error de red, en `src/Kryon.Web/Usuarios/TextosErrores.cs`. Un código desconocido usa el texto de `error-interno`.
- [X] T042 [P] Crear el contexto de bUnit en `tests/Kryon.Web.Tests/Infraestructura/ContextoPruebaWeb.cs`: registra el `ClienteUsuarios` **real** construido sobre un `HttpClient` con un `HttpMessageHandler` falso que devuelve las respuestas preparadas por cada prueba. Sin interfaces ni métodos virtuales en producción.

**Checkpoint**: aislamiento en dos capas probado, seguridad por defecto activa y trazabilidad lista. Ya pueden empezar las historias.

---

## Phase 3: User Story 1 - Ver los usuarios de mi empresa (Priority: P1) 🎯 MVP

**Goal**: listado paginado de los usuarios de la empresa actual, con búsqueda, filtros por rol y estado, marca "Tú", estado vacío y "sin resultados".

**Independent Test**: como `ana` (A), el listado muestra exactamente los usuarios de A con nombre, identificador, rol y estado. Buscar "Ana" no devuelve "Ana Beta" (B) y el total no la cuenta. `carla` recibe acceso denegado (quickstart V1, SC-001, SC-008).

### Tests for User Story 1 ⚠️ (escribir primero, deben fallar)

- [X] T043 [P] [US1] Pruebas de API de aislamiento y contenido de `GET /api/usuarios` en `tests/Kryon.Api.Tests/Usuarios/ListarUsuariosAislamientoTests.cs`: solo usuarios de A y `total` sin contar B (US1-1, SC-001); campos nombre, identificador, rol y estado (US1-2); la fila de `ana` con `esCuentaPropia = true` y `acciones.desactivar = false` (US1-5); la búsqueda "Ana" no incluye "Ana Beta" ni la cuenta (US1-9, SC-008); `carla` → 403 `sin-permiso` sin datos (US1-4).
- [ ] T044 [P] [US1] Pruebas de API de búsqueda, filtros y paginación de `GET /api/usuarios` en `tests/Kryon.Api.Tests/Usuarios/ListarUsuariosBusquedaTests.cs`: búsqueda parcial por nombre e identificador, incluidos los comodines `%` y `_` escapados (US1-7); filtros por rol y estado combinados con AND (US1-8); paginación con `total` y la búsqueda mantenida entre páginas (US1-11); `tamanoPagina` por encima del máximo configurado → 400 `validacion`.
- [ ] T045 [P] [US1] Pruebas de API de `GET /api/usuarios/roles-asignables` en `tests/Kryon.Api.Tests/Usuarios/RolesAsignablesTests.cs`: solo roles de A; `carla` → 403.
- [ ] T046 [P] [US1] Pruebas bUnit de la tabla del listado en `tests/Kryon.Web.Tests/Usuarios/ListadoUsuariosTablaTests.cs`: `<table>` con `<caption>` y columnas; estado como texto "Activo"/"Inactivo" (US1-3); etiqueta "Tú"; "Sin rol asignado" si el rol es nulo (EC-10); acciones por fila solo si su bandera es verdadera, con nombres accesibles "Ver detalle de {nombre}", etc.; "Nuevo usuario" solo si `puedeRegistrar`. Con un usuario cuyo nombre completo e identificador de acceso están cerca de los máximos provisionales configurados, la fila mantiene visibles el nombre, el rol y el estado, conserva su estructura semántica (una fila, las mismas celdas) y el texto completo sigue disponible para lectores de pantalla (EC-9).
- [ ] T047 [P] [US1] Pruebas bUnit de los estados del listado en `tests/Kryon.Web.Tests/Usuarios/ListadoUsuariosEstadosTests.cs`: estado vacío sin criterios frente a "sin resultados" con criterios y botón "Limpiar búsqueda y filtros" (US1-6, US1-10, EC-11); `<nav aria-label="Paginación de usuarios">` con el texto "Página X de Y · N usuarios"; anuncio "N usuarios encontrados" en la región viva; `sin-permiso` → mensaje sin tabla.

### Implementation for User Story 1

- [ ] T048 [US1] Implementar `ConsultaUsuarios` (implementa `IConsultaUsuarios` de T016 y devuelve tipos de Core) en `src/Kryon.Infrastructure/Usuarios/ConsultaUsuarios.cs`: búsqueda parcial parametrizada con `LIKE`, escapando `%`, `_` y `[` sobre `NombreCompleto` e `IdentificadorAcceso`; filtros `rolId` y `estado` con AND; orden `NombreCompleto`, luego `Id`; `Skip`/`Take` con el tamaño leído de `OpcionesUsuarios` (valor por defecto y máximo provisionales); `total` de la misma consulta filtrada (FR-014 a FR-018). Nunca usa `IgnoreQueryFilters()`. Registrar `IConsultaUsuarios` → `ConsultaUsuarios` en `src/Kryon.Api/Program.cs` con ciclo de vida por solicitud (depende de `KryonDbContext`).
- [ ] T049 [US1] Implementar el caso de uso `ListarUsuarios` en `src/Kryon.Core/Usuarios/ListarUsuarios.cs`: combina `IConsultaUsuarios` (T016) con `CalculadoraAcciones` y devuelve un resultado de aplicación definido en `Kryon.Core` (usuarios con sus acciones calculadas y `esCuentaPropia`, total, página y tamaño de página, y `puedeRegistrar` = capacidad de registrar **y** gate `RegistroUsuarios` abierto; el estado del gate lo recibe como dato de entrada; ver *Feature / release gating*). No depende de `Kryon.Infrastructure` ni usa `PaginaUsuarios` u otros DTOs de `Kryon.Contracts` (Core no referencia ninguno de los dos).
- [ ] T050 [US1] Crear `UsuariosEndpoints` con `GET /api/usuarios` (política `Usuarios.Acceder`) y `GET /api/usuarios/roles-asignables` (política `Usuarios.Acceder`) en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs`, y mapearlos en `src/Kryon.Api/Program.cs`. `GET /api/usuarios` consume el resultado de Core de T049 (pasándole el estado del gate `RegistroUsuarios`) y lo mapea a `PaginaUsuarios` de `Kryon.Contracts` con la forma exacta del contrato OpenAPI, incluido `puedeRegistrar`.
- [ ] T051 [US1] Crear la página `src/Kryon.Web/Usuarios/ListadoUsuarios.razor` (ruta `/usuarios`) según `contracts/ui-usuarios.md` → *Listado*: `<h1>` "Usuarios", tabla, región viva y carga con `ClienteUsuarios`. La búsqueda, los filtros y la página se leen y escriben en la query string. Estilos del listado en `src/Kryon.Web/Usuarios/ListadoUsuarios.razor.css`: el nombre completo y el identificador de acceso largos se ajustan en varias líneas (`overflow-wrap: anywhere`) sin truncar, para que las columnas Rol, Estado y Acciones sigan visibles (EC-9).
- [ ] T052 [P] [US1] Crear el componente `src/Kryon.Web/Usuarios/FiltrosUsuarios.razor`: búsqueda con etiqueta "Buscar por nombre o identificador", `<select>` "Rol" y "Estado" con la opción "Todos" (roles de `GET /usuarios/roles-asignables`), resumen de criterios activos y botón "Limpiar búsqueda y filtros" (FR-014, FR-015, FR-017).
- [ ] T053 [P] [US1] Crear el componente `src/Kryon.Web/Usuarios/PaginacionUsuarios.razor`: `<nav aria-label="Paginación de usuarios">`, botones "Página anterior" y "Página siguiente" deshabilitados en los extremos y texto "Página X de Y · N usuarios" (FR-018).
- [ ] T054 [US1] Manejar en `src/Kryon.Web/Usuarios/ListadoUsuarios.razor` los estados de acceso denegado (`sin-permiso`: mensaje y sin tabla), estado vacío ("No hay usuarios que mostrar"), sin resultados ("No hay usuarios que coincidan con la búsqueda y los filtros") y error de red ("No se pudo confirmar el resultado…").

**Checkpoint**: US1 funciona y se puede probar sola. Primer MVP entregable.

---

## Phase 4: User Story 2 - Activar o desactivar un usuario (Priority: P1)

**Goal**: desactivar con confirmación y activar sin confirmación, con las reglas de autodesactivación y del último administrador, control de concurrencia y trazabilidad.

**Independent Test**: `ana` desactiva a `beto` confirmando, lo ve Inactivo y hay un registro de auditoría; luego lo reactiva. En paralelo, las desactivaciones cruzadas de `ana` y `beto` dejan exactamente un administrador activo (quickstart V3, V7, V8, V9; SC-002, SC-006, SC-007, SC-009).

### Tests for User Story 2 ⚠️

- [ ] T055 [P] [US2] Pruebas unitarias de reglas en `tests/Kryon.Core.Tests/Usuarios/ReglasEstadoTests.cs`: no se puede desactivar a uno mismo (FR-034); no se desactiva un usuario ya Inactivo ni se activa uno Activo; `ReglaUltimoAdministrador` rechaza dejar cero administradores activos (FR-036); desactivar publica `UsuarioDesactivado`.
- [ ] T056 [P] [US2] Pruebas de API del flujo de desactivación y activación en `tests/Kryon.Api.Tests/Usuarios/CambiarEstadoTests.cs`: 200 con nuevo `ETag` y un registro de auditoría con los 5 datos (US2-2, US2-4, US2-6, SC-002); sin `If-Match` → 428 `falta-version`; `If-Match` obsoleto → 412 `usuario-modificado` con `actual` y sin registro de auditoría (EC-2, FR-038).
- [ ] T057 [P] [US2] Pruebas de API de reglas y rechazos de desactivación y activación en `tests/Kryon.Api.Tests/Usuarios/CambiarEstadoReglasTests.cs`: autodesactivación → 409 `no-puede-desactivarse-a-si-mismo` (EC-5); último administrador → 409 `ultimo-administrador` (EC-6); id de B → 404 idéntico al de un id inexistente (EC-1), tanto en desactivación como en activación. Cuando el id no es accesible (de B o inexistente), la respuesta 404 es idéntica en ambos casos y se registra `UsuarioNoAccesible` con actor, empresa actual (la del actor), id solicitado, `OperacionSolicitada` = `Desactivacion` o `Activacion` según el endpoint y momento, sin empresa propietaria ni datos del recurso (FR-046).; el rol sin la capacidad → 403.
- [ ] T058 [P] [US2] Prueba de concurrencia en `tests/Kryon.Api.Tests/Usuarios/UltimoAdministradorConcurrenciaTests.cs`: con `ana` y `beto` como únicos administradores, lanzar en paralelo las desactivaciones cruzadas, repetido al menos 50 veces. Siempre exactamente un 200 y un 409 `ultimo-administrador`, y queda al menos un administrador activo (SC-006, quickstart V7).
- [ ] T059 [P] [US2] Pruebas bUnit del diálogo en `tests/Kryon.Web.Tests/Usuarios/ConfirmarDesactivacionTests.cs`: título "Desactivar a {nombre}" y texto del contrato; el foco inicial en "Cancelar"; Escape equivale a Cancelar; el foco vuelve al botón de origen; Cancelar **no** llama a la API (US2-3, SC-007); "Activar" no abre diálogo (US2-4); los mensajes de éxito "{nombre} fue desactivado." y "{nombre} fue activado."
- [ ] T060 [P] [US2] Pruebas bUnit de fallo de comunicación en `tests/Kryon.Web.Tests/Usuarios/FalloComunicacionTests.cs`. Con el mecanismo de T042, cuyo `HttpMessageHandler` falso lanza `HttpRequestException` al desactivar (tras confirmar) y al activar, comprobar: (1) se muestra el mensaje del contrato de interfaz "No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo."; (2) se anuncia en la región `role="alert"` del componente de mensajes; (3) **no** aparece ningún mensaje de éxito; (4) la fila conserva el estado anterior y no se muestra como modificada (EC-8, FR-040, FR-042).

### Implementation for User Story 2

- [ ] T061 [US2] Añadir los métodos `Desactivar(actorId)` y `Activar()` a `src/Kryon.Core/Usuarios/Usuario.cs`, con las precondiciones de data-model.md (*Transiciones de estado*) que no dependen de la base de datos: no ser el actor, estar en el estado de origen correcto.
- [ ] T062 [P] [US2] Implementar `ReglaUltimoAdministrador` en `src/Kryon.Core/Usuarios/ReglaUltimoAdministrador.cs`: un administrador activo es un usuario Activo cuyo rol incluye la capacidad de acceso a la gestión (nombre provisional `usuarios.administrar`). Rechaza cualquier cambio que deje cero (FR-036).
- [ ] T063 [US2] Implementar `BloqueoAdministradoresEmpresa` (implementa `IBloqueoAdministradoresEmpresa` de T016) en `src/Kryon.Infrastructure/Usuarios/BloqueoAdministradoresEmpresa.cs`: `sp_getapplock` exclusivo de transacción con el recurso `usuarios-admin:{EmpresaId}` (research §R5). Registrar `IBloqueoAdministradoresEmpresa` → `BloqueoAdministradoresEmpresa` en `src/Kryon.Api/Program.cs` con ciclo de vida por solicitud (depende de `KryonDbContext`).
- [ ] T064 [US2] Implementar `RepositorioUsuarios` (implementa `IRepositorioUsuarios` de T016; obtener por id dentro de la empresa, contar administradores activos y guardar con `Version` como token de concurrencia, traduciendo `DbUpdateConcurrencyException` a `usuario-modificado`) en `src/Kryon.Infrastructure/Usuarios/RepositorioUsuarios.cs`. Registrar `IRepositorioUsuarios` → `RepositorioUsuarios` en `src/Kryon.Api/Program.cs` con ciclo de vida por solicitud (depende de `KryonDbContext`).
- [ ] T065 [US2] Implementar el caso de uso `CambiarEstadoUsuario` en `src/Kryon.Core/Usuarios/CambiarEstadoUsuario.cs`. Pasos: transacción → bloqueo por empresa (solo al desactivar) → cargar → verificar la versión → aplicar la regla → guardar → escribir la auditoría (`Activacion`/`Desactivacion`) → confirmar → publicar `UsuarioDesactivado` (DEP-1; sin suscriptores en esta feature).
- [ ] T066 [US2] Añadir `POST /api/usuarios/{id}/desactivacion` (política `Usuarios.Desactivar`) y `POST /api/usuarios/{id}/activacion` (política `Usuarios.Activar`), con `If-Match` obligatorio y `ETag` en la respuesta, en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs`.
- [ ] T067 [P] [US2] Crear el diálogo `src/Kryon.Web/Usuarios/ConfirmarDesactivacion.razor` según `contracts/ui-usuarios.md` → *Diálogo de confirmación*: `<dialog>` modal, título y texto exactos, botones "Desactivar" y "Cancelar", foco inicial en Cancelar, Escape cancela y el foco vuelve al origen.
- [ ] T068 [US2] Conectar Desactivar (con el diálogo) y Activar (directo) en `src/Kryon.Web/Usuarios/ListadoUsuarios.razor`: envían el `version` como `If-Match`, actualizan la fila con la respuesta, muestran los mensajes de éxito y, ante 409 o 412, muestran el texto de `TextosErrores` y el estado real (US2-7, FR-042).

**Checkpoint**: US1 y US2 funcionan. Es el MVP completo de P1.

---

## Phase 5: User Story 3 - Consultar el detalle de un usuario (Priority: P2)

**Goal**: ficha de solo lectura con las acciones permitidas; un usuario de otra empresa es indistinguible de uno inexistente.

**Independent Test**: desde el listado, `ana` abre el detalle de `beto` y vuelve al listado. Pedir el id de "Ana Beta" devuelve "usuario no encontrado", con la misma respuesta que un id inventado (quickstart V1).

### Tests for User Story 3 ⚠️

- [ ] T069 [P] [US3] Pruebas de API de `GET /api/usuarios/{id}` en `tests/Kryon.Api.Tests/Usuarios/ObtenerUsuarioTests.cs`: 200 con `ETag` y `acciones` (US3-1); id de B y un id inventado → 404 con cuerpos **byte a byte iguales** (US3-3, FR-003); ambos casos dejan un registro `UsuarioNoAccesible` con la misma forma (actor, empresa del actor, id solicitado, `OperacionSolicitada = Consulta`, momento), sin ningún dato del recurso y sin empresa propietaria (FR-046).
- [ ] T070 [P] [US3] Pruebas bUnit del detalle en `tests/Kryon.Web.Tests/Usuarios/DetalleUsuarioTests.cs`: campos de solo lectura, marca "Tú", acciones según las banderas, enlace "Volver al listado" que conserva la query string (US3-2), y "Usuario no encontrado" con enlace al listado.

### Implementation for User Story 3

- [ ] T071 [US3] Añadir `GET /api/usuarios/{id}` (política `Usuarios.Acceder`, `ETag` en la respuesta, filtro `RegistroUsuarioNoAccesible` de T031 con `OperacionSolicitada = Consulta`) en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs`.
- [ ] T072 [US3] Crear la página `src/Kryon.Web/Usuarios/DetalleUsuario.razor` (ruta `/usuarios/{id}`) según `contracts/ui-usuarios.md` → *Detalle*, reutilizando el diálogo de desactivación de T067.

**Checkpoint**: US1, US2 y US3 funcionan de forma independiente.

---

## Phase 6: User Story 4 - Registrar y editar usuarios (Priority: P2)

**Goal**: editar el nombre completo y el rol (entregable ya) y registrar usuarios (diseñado y probado con dobles; su integración real está bloqueada por DEP-2, DEP-3 y DEP-5). Editar el identificador está bloqueado por DEP-4.

**Independent Test**: `ana` edita el nombre y el rol de `beto`, y la auditoría registra solo los campos cambiados con su valor anterior y el nuevo. Guardar con datos inválidos muestra errores por campo. Una segunda sesión con la versión anterior recibe el conflicto (quickstart V3, V9). El registro se valida con dobles (V10, V11) hasta que se desbloquee.

### Tests for User Story 4 ⚠️ — Edición

- [ ] T073 [P] [US4] Pruebas unitarias del validador y de la edición en `tests/Kryon.Core.Tests/Usuarios/EditarUsuarioTests.cs`: `NombreCompleto` vacío o solo espacios → error de campo; se recortan los espacios; longitud por encima del máximo configurado → error; cambiar el propio rol → `no-puede-cambiar-su-propio-rol` (FR-035); quitar el rol de administrador al último administrador activo → `ultimo-administrador` (US4-11); el rol de otra empresa → `rol-no-valido` (US4-9); cambios calculados solo con los campos modificados (FR-044).
- [ ] T074 [P] [US4] Pruebas de API del flujo de `PUT /api/usuarios/{id}` en `tests/Kryon.Api.Tests/Usuarios/EditarUsuarioApiTests.cs`: 200 con auditoría `Modificacion` y `CambiosJson` solo con los campos cambiados (US4-7, SC-002); un administrador edita a otro (US4-10); 400 `validacion` con `errors` por campo (US4-3); 412 `usuario-modificado` con `actual` y sin cambios (US4-14, SC-009); 428 sin `If-Match`.
- [ ] T075 [P] [US4] Pruebas de API de reglas y rechazos de `PUT /api/usuarios/{id}` en `tests/Kryon.Api.Tests/Usuarios/EditarUsuarioReglasApiTests.cs`: 409 `no-puede-cambiar-su-propio-rol` (US4-13, SC-006); 409 `ultimo-administrador` al quitar el rol (US4-11); 409 `rol-no-valido` con un rol de B (US4-9); id de B → 404 idéntico al de un id inexistente (US4-8, EC-1). Cuando el id no es accesible (de B o inexistente), la respuesta 404 es idéntica en ambos casos y se registra `UsuarioNoAccesible` con actor, empresa actual (la del actor), id solicitado, `OperacionSolicitada` = `Edicion` y momento, sin empresa propietaria ni datos del recurso (FR-046).; sin la capacidad de editar → 403.
- [ ] T076 [P] [US4] Pruebas bUnit de validación del formulario en `tests/Kryon.Web.Tests/Usuarios/FormularioUsuarioValidacionTests.cs`: `<label>` visibles y marca de obligatorio en texto; errores junto a cada campo con `aria-describedby` y `aria-invalid="true"`; un resumen con `role="alert"` que recibe el foco (FR-025, FR-029); Cancelar vuelve sin cambios (US4-6).
- [ ] T077 [P] [US4] Pruebas bUnit de reglas del formulario en `tests/Kryon.Web.Tests/Usuarios/FormularioUsuarioReglasTests.cs`: rol de la propia cuenta en solo lectura con "No puedes cambiar tu propio rol" (FR-035); identificador editable o de solo lectura **según** `acciones.editarIdentificador` (se prueban ambos valores); ante `usuario-modificado`, el mensaje del contrato y la recarga de `actual` (FR-039).

### Implementation for User Story 4 — Edición

- [ ] T078 [US4] Implementar `ValidadorUsuario` en `src/Kryon.Core/Usuarios/ValidadorUsuario.cs`: `NombreCompleto` obligatorio, con espacios recortados, no vacío y con longitud máxima leída de `OpcionesUsuarios` (provisional); `IdentificadorAcceso` obligatorio, con longitud máxima leída de `OpcionesUsuarios` (provisional); `RolId` obligatorio y perteneciente a la empresa actual (FR-024 a FR-026). **No** valida el formato ni la unicidad del identificador (DEP-3).
- [ ] T079 [US4] ⛔ BLOQUEADA (DEP-4) Conectar `IPoliticaIdentificadorAcceso.PermiteEdicion` a `CalculadoraAcciones` (`editarIdentificador`) y aceptar `identificadorAcceso` en `EditarUsuario` (con `ValidarFormato` y `EstaEnUso` de DEP-3; responde `identificador-no-editable` o `identificador-en-uso` según lo que defina la spec de autenticación) en `src/Kryon.Core/Usuarios/CalculadoraAcciones.cs` y `src/Kryon.Core/Usuarios/EditarUsuario.cs`. **No iniciar** hasta que la futura spec de autenticación defina si el identificador es editable.
- [ ] T080 [US4] ⛔ BLOQUEADA (DEP-4, DEP-3) Implementar `PermiteEdicion` en el adaptador `src/Kryon.Infrastructure/Usuarios/Integraciones/PoliticaIdentificadorAcceso.cs` (creado en T091), abrir el gate `EdicionIdentificador`, añadir las pruebas de la regla definida en `tests/Kryon.Api.Tests/Usuarios/EdicionIdentificadorTests.cs` y añadir el caso `identificador-no-editable` a `tests/Kryon.Api.Tests/Errores/FormatoErroresTests.cs`.
- [ ] T081 [US4] Implementar el caso de uso `EditarUsuario` para nombre completo y rol en `src/Kryon.Core/Usuarios/EditarUsuario.cs`. Pasos: transacción → bloqueo por empresa si cambia el rol de un administrador → cargar → verificar la versión → validar (T078) → regla del propio rol (FR-035) → regla del último administrador (FR-036) → guardar → auditoría `Modificacion` solo con los campos cambiados (FR-044) → confirmar.
- [ ] T082 [US4] Añadir `PUT /api/usuarios/{id}` (política `Usuarios.Editar`, `If-Match` obligatorio y `ETag` en la respuesta) en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs`.
- [ ] T083 [US4] Crear el componente `src/Kryon.Web/Usuarios/FormularioUsuario.razor` según `contracts/ui-usuarios.md` → *Formulario*: campos, identificador editable o de solo lectura según `acciones.editarIdentificador`, rol propio en solo lectura, errores por campo, resumen con foco, y los botones "Guardar" y "Cancelar".
- [ ] T084 [US4] Crear la página de edición `src/Kryon.Web/Usuarios/EditarUsuario.razor` (ruta `/usuarios/{id}/editar`): carga el usuario y su `ETag`, usa `FormularioUsuario.razor`, envía `If-Match`, maneja `usuario-modificado` y, al guardar o cancelar, vuelve al listado conservando la query string (FR-022).

### Tests for User Story 4 ⚠️ — Registro (con dobles; no bloqueadas)

- [ ] T085 [P] [US4] Pruebas unitarias de `RegistrarUsuario` con dobles en `tests/Kryon.Core.Tests/Usuarios/RegistrarUsuarioTests.cs`: usa el estado que devuelve `IPoliticaAltaUsuario` (sin suponer ninguno); `EstaEnUso = true` → `identificador-en-uso` sin datos de otra empresa; si la fuente de DEP-5 informa un límite aplicable alcanzado → `limite-usuarios-alcanzado` sin crear nada (FR-028); si informa que no hay límite aplicable → no rechaza por cantidad (EC-12); `EmpresaId` siempre del contexto (FR-023).
- [ ] T086 [P] [US4] Pruebas de API de `POST /api/usuarios` con dobles (quickstart V10, V11) en `tests/Kryon.Api.Tests/Usuarios/RegistrarUsuarioApiTests.cs`: 201 con `Location`, `ETag` y auditoría `Alta` (US4-2, SC-002); 400 por campo (US4-3); 409 `identificador-en-uso`; 409 `limite-usuarios-alcanzado`; 409 `rol-no-valido`; sin la capacidad de registrar → 403. Documentar en el archivo que solo validan la conexión.

### Implementation for User Story 4 — Registro

- [ ] T087 [US4] Implementar el caso de uso `RegistrarUsuario` en `src/Kryon.Core/Usuarios/RegistrarUsuario.cs`, que depende **solo** de las interfaces de T016. Pasos: validar (T078) → `ValidarFormato` y `EstaEnUso` (DEP-3) → `IFuenteLimiteUsuarios` (DEP-5, regla FR-028) → estado inicial y credencial por `IPoliticaAltaUsuario` (DEP-2) → guardar con `EmpresaId` del contexto → auditoría `Alta`, todo en una transacción. No contiene ningún valor sustituto para las decisiones externas.
- [ ] T088 [US4] Añadir `POST /api/usuarios` (política `Usuarios.Registrar`; 201 con `Location` y `ETag`) en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs`. El endpoint se mapea solo con el gate `RegistroUsuarios` abierto (ver *Feature / release gating*). En las pruebas, el gate se abre con los dobles de T034.
- [ ] T089 [US4] Crear la página de registro `src/Kryon.Web/Usuarios/NuevoUsuario.razor` (ruta `/usuarios/nuevo`) reutilizando `FormularioUsuario.razor`. El botón "Nuevo usuario" del listado solo aparece si `puedeRegistrar` (T049), así que depende del gate `RegistroUsuarios`.
- [ ] T090 [US4] ⛔ BLOQUEADA (DEP-2) Implementar el adaptador real de `IPoliticaAltaUsuario` en `src/Kryon.Infrastructure/Usuarios/Integraciones/PoliticaAltaUsuario.cs`, según el estado inicial y el mecanismo de credencial que defina la futura spec de autenticación. Si esa spec añade estados, ampliar `EstadoUsuario`, `EstadoUsuario` del contrato y los textos de la interfaz. **No iniciar** hasta que exista esa spec.
- [ ] T091 [US4] ⛔ BLOQUEADA (DEP-3) Implementar el adaptador real de `IPoliticaIdentificadorAcceso` (`ValidarFormato`, `EstaEnUso`) en `src/Kryon.Infrastructure/Usuarios/Integraciones/PoliticaIdentificadorAcceso.cs`, según la unicidad y el formato que defina la futura spec de autenticación. **No iniciar** hasta que exista esa spec.
- [ ] T092 [US4] ⛔ BLOQUEADA (DEP-3) Si la regla de unicidad lo requiere, crear la migración del índice único con el ámbito definido (por empresa o global) en `src/Kryon.Infrastructure/Persistencia/Migraciones/`.
- [ ] T093 [US4] ⛔ BLOQUEADA (DEP-5) Implementar el adaptador real de `IFuenteLimiteUsuarios` en `src/Kryon.Infrastructure/Usuarios/Integraciones/FuenteLimiteUsuarios.cs`, conectado a la fuente que defina la decisión comercial (existencia, cifra y forma de contar). **No iniciar** hasta que esa decisión exista.
- [ ] T094 [US4] ⛔ BLOQUEADA (DEP-2, DEP-3, DEP-5) Registrar los adaptadores de T090 a T093 en `src/Kryon.Api/Program.cs`, repetir V10 y V11 con las implementaciones reales en `tests/Kryon.Api.Tests/Usuarios/RegistrarUsuarioIntegracionRealTests.cs` y añadir las pruebas de las reglas que definan esas specs.

**Checkpoint**: la edición de nombre y rol está entregada. El registro y la edición del identificador están implementados contra interfaces y probados con dobles; se entregan al completar T079, T080 y T090 a T094.

---

## Phase 7: User Story 5 - Acciones visibles según permisos (Priority: P2)

**Goal**: mostrar solo las acciones permitidas y rechazar en el servidor cualquier intento sin permiso, incluso si el permiso se retira durante la sesión.

**Independent Test**: con el rol sin la capacidad de desactivar, "Desactivar" no aparece en ninguna fila y `POST …/desactivacion` directo responde 403 (quickstart V6, SC-005).

### Tests for User Story 5 ⚠️

- [ ] T095 [P] [US5] Pruebas de API de autorización por operación en `tests/Kryon.Api.Tests/Usuarios/AutorizacionOperacionesTests.cs`: para cada uno de los 7 endpoints, una identidad sin la capacidad correspondiente → 403 `sin-permiso` sin cambios ni datos (US5-2); permiso retirado entre dos solicitudes → la segunda es 403 (US5-3, EC-7).
- [ ] T096 [P] [US5] Pruebas de API de coherencia de acciones en `tests/Kryon.Api.Tests/Usuarios/CoherenciaAccionesTests.cs`: para cada combinación de capacidades, `acciones` y `puedeRegistrar` coinciden exactamente con lo que la API permite ejecutar (SC-005).
- [ ] T097 [P] [US5] Pruebas bUnit de visibilidad en `tests/Kryon.Web.Tests/Usuarios/AccionesVisiblesTests.cs`: ninguna acción con bandera falsa se renderiza en el listado ni en el detalle (US5-1). Ante un 403 `sin-permiso` en una acción, se muestra "No tienes permiso para realizar esta acción." y la fila se recarga sin esa acción (US5-3).

### Implementation for User Story 5

- [ ] T098 [US5] En `src/Kryon.Web/Usuarios/ListadoUsuarios.razor` y `src/Kryon.Web/Usuarios/DetalleUsuario.razor`, ante `sin-permiso` en cualquier acción: mostrar el mensaje de `TextosErrores` y volver a pedir el usuario afectado (o la página) para refrescar `acciones` (US5-3).
- [ ] T099 [US5] Verificar y documentar en `src/Kryon.Api/Usuarios/UsuariosEndpoints.cs` (comentario por endpoint) que cada endpoint exige su política y que `CalculadoraAcciones` usa las **mismas** capacidades que esas políticas, para que las acciones visibles no puedan divergir de las permitidas (SC-005). Si divergen, corregir la calculadora.

**Checkpoint**: la visibilidad y la autorización son coherentes en todas las operaciones.

---

## Phase 8: User Story 6 - Uso accesible de las acciones principales (Priority: P2)

**Goal**: todas las acciones principales se pueden ejecutar solo con teclado, y la feature cumple WCAG 2.2 AA en sus pantallas.

**Independent Test**: recorrer el listado, el detalle, la edición y la desactivación con confirmación, la activación y la paginación solo con teclado; axe no informa violaciones serias ni críticas (quickstart V4, SC-003).

### Tests for User Story 6 ⚠️

- [ ] T100 [US6] Crear el entorno E2E de la API en `tests/Kryon.E2E.Tests/Infraestructura/EntornoApiE2E.cs`: crea y gestiona su propio `MsSqlContainer` (Testcontainers), aplica al contenedor el SQL idempotente generado **previamente** con `scripts/generar-migracion.ps1` (`artifacts/sql/kryon-migraciones-idempotente.sql`; si no existe, la preparación falla con un mensaje que indica generarlo), luego prepara en él un dataset equivalente al dataset semilla de quickstart.md (sin usar el código de `SemillaDatos` de `Kryon.Api.Tests`) y levanta `Kryon.Api` como **proceso real** en entorno `Test`, con la cadena de conexión del contenedor, el esquema `IdentidadPrueba` (T009) y su política CORS de pruebas (T010). Gestiona el inicio, la espera hasta que la API esté disponible y el cierre del proceso. No usa `WebApplicationFactory` ni referencia `Kryon.Api` ni `Kryon.Api.Tests` (caja negra, research §R11), y **no** ejecuta `dotnet ef database update`, `dotnet ef migrations script` ni `Database.Migrate`. El SQL se aplica con un mecanismo que soporten las dependencias ya aprobadas; si eso exigiera un paquete nuevo, detenerse y reportarlo. Al introducir las pruebas E2E, actualizar también `.github/workflows/ci.yml` para que el CI, en este orden: (1) haga el restore y el build necesarios; (2) ejecute `dotnet tool restore`; (3) ejecute `scripts/generar-migracion.ps1`; (4) compruebe que existe `artifacts/sql/kryon-migraciones-idempotente.sql`; (5) instale los navegadores de Playwright con `pwsh` y el `playwright.ps1` que genera `Microsoft.Playwright` al compilar `Kryon.E2E.Tests` (en su carpeta de salida `bin/<configuración>/net10.0/`, el mismo mecanismo que documenta quickstart.md); (6) instale las dependencias del sistema del runner Linux que exija el mecanismo oficial de Playwright; y (7) solo entonces ejecute `Kryon.E2E.Tests`. La generación del SQL y la instalación de navegadores ocurren en el CI, no en el código de pruebas.
- [ ] T101 [US6] Crear el entorno E2E de la interfaz en `tests/Kryon.E2E.Tests/Infraestructura/EntornoWebE2E.cs`: levanta `Kryon.Web` (standalone) como **proceso real**, configurado con la URL de la API de T100; gestiona el inicio, la espera hasta que esté disponible y el cierre; no añade referencia de proyecto a `Kryon.Web`. Playwright trabaja contra la URL HTTP real de la interfaz y crea un `BrowserContext` de Playwright con `SetExtraHTTPHeadersAsync` que envía los encabezados `X-Kryon-Prueba-*` de `ana` (empresa A, todas las capacidades de gestión). Las pruebas E2E se autentican **solo** así; ninguna usa un mecanismo de autenticación real.
- [ ] T102 [P] [US6] Prueba E2E solo con teclado de navegación en `tests/Kryon.E2E.Tests/Usuarios/FlujoTecladoListadoTests.cs`: usando solo Tab, Shift+Tab, Enter, Espacio y Escape, buscar, filtrar, paginar, abrir el detalle y volver (US6-1, FR-047).
- [ ] T103 [P] [US6] Prueba E2E solo con teclado de acciones en `tests/Kryon.E2E.Tests/Usuarios/FlujoTecladoAccionesTests.cs`: editar y guardar, desactivar (el foco entra en el diálogo, se confirma y el foco vuelve al origen), cancelar otra desactivación y activar (US6-1, US6-3, FR-047, FR-049).
- [ ] T104 [P] [US6] Prueba E2E de accesibilidad automática en `tests/Kryon.E2E.Tests/Usuarios/AccesibilidadTests.cs`: ejecutar axe (`Deque.AxeCore.Playwright`) en el listado (con datos, vacío y sin resultados), el detalle, el formulario (con errores) y el diálogo abierto. Cero violaciones de impacto `serious` o `critical` (SC-003).
- [ ] T105 [P] [US6] Pruebas bUnit de nombres accesibles y anuncios en `tests/Kryon.Web.Tests/Usuarios/NombresAccesiblesTests.cs`: todo `button`, `a`, `input` y `select` tiene nombre accesible; las acciones por fila incluyen el nombre del usuario (US6-2, FR-048); los mensajes de éxito van a `aria-live="polite"` y los errores a `role="alert"`, siempre con texto (US6-4, FR-050).

### Implementation for User Story 6

- [ ] T106 [US6] Corregir las violaciones que detecten T102 a T105 en el listado y sus componentes (`src/Kryon.Web/Usuarios/ListadoUsuarios.razor`, `src/Kryon.Web/Usuarios/FiltrosUsuarios.razor`, `src/Kryon.Web/Usuarios/PaginacionUsuarios.razor`, `src/Kryon.Web/Compartido/Mensajes.razor`, `src/Kryon.Web/Usuarios/ListadoUsuarios.razor.css`): tratamiento de texto extenso (EC-9), orden de foco, nombres, anuncios, contraste y estados de deshabilitado de la paginación.
- [ ] T107 [P] [US6] Corregir las violaciones que detecten T102 a T105 en el detalle y el diálogo (`src/Kryon.Web/Usuarios/DetalleUsuario.razor`, `src/Kryon.Web/Usuarios/ConfirmarDesactivacion.razor`): foco del diálogo, nombres y contraste.
- [ ] T108 [P] [US6] Corregir las violaciones que detecten T102 a T105 en el formulario (`src/Kryon.Web/Usuarios/FormularioUsuario.razor`, `src/Kryon.Web/Usuarios/EditarUsuario.razor`, `src/Kryon.Web/Usuarios/NuevoUsuario.razor`): etiquetas, asociación de errores, resumen de errores y foco.

**Checkpoint**: todas las historias funcionan y son accesibles.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: calidad, preparación para operar en Azure y verificación final.

**Fuera de alcance de esta feature**: la infraestructura y la automatización de despliegue a Azure (App Service, Static Web Apps, Key Vault, Managed Identity, etc.) pertenecen al trabajo de plataforma/deployment. Esta feature solo queda preparada y configurable para Azure según plan.md y research §R12 (T109 y T110); no añade tareas de infraestructura cloud.

- [ ] T109 Configurar Application Insights en `src/Kryon.Api/Program.cs` con un filtro de telemetría en `src/Kryon.Api/Seguridad/FiltroTelemetriaSinDatosSensibles.cs` que elimine tokens, encabezados de autorización, cuerpos de solicitud y datos personales (research §R12).
- [ ] T110 [P] Reutilizar en `.github/workflows/ci.yml` el flujo de generación del SQL que ya añadió T100 (`dotnet tool restore` y `scripts/generar-migracion.ps1`, creado en T024), **sin** volver a introducir esos pasos: conservar y publicar `artifacts/sql/kryon-migraciones-idempotente.sql` como artefacto revisable de CI para su uso en el despliegue, y documentar el mecanismo. Verificar que no se usa `database update` como mecanismo de despliegue. No se introduce `database update` como mecanismo de despliegue y `Kryon.Api` no migra la base de datos al arrancar (research §R12).
- [ ] T111 [P] Documentar la configuración provisional (`Usuarios:Paginacion`, `Usuarios:Validacion`, `Seguridad:Permisos`), cómo cambiarla y que no es requisito de negocio, en `docs/configuracion-usuarios.md`.
- [ ] T112 Escribir la prueba parametrizada `tests/Kryon.Api.Tests/Errores/FormatoErroresTests.cs` que provoca cada código de error de la feature (`no-autenticado`, `sin-permiso`, `usuario-no-encontrado`, `validacion`, `rol-no-valido`, `identificador-en-uso`, `limite-usuarios-alcanzado`, `no-puede-desactivarse-a-si-mismo`, `no-puede-cambiar-su-propio-rol`, `ultimo-administrador`, `usuario-modificado`, `falta-version`) y además `error-interno` (500 genérico de T030). Para cada caso comprueba: formato ProblemDetails consistente (`type`, `title`, `status`, `codigo`); código HTTP esperado según el contrato; código funcional esperado; ausencia de stack trace y de información interna (nombres de tipos, SQL, rutas); ausencia de secretos (la cadena de conexión y cualquier valor sensible de configuración de la factoría no aparecen en el cuerpo); y ausencia de datos de la empresa B (ids, nombres e identificadores de sus usuarios y su `EmpresaId`). Usa los dobles de T034 para `identificador-en-uso` y `limite-usuarios-alcanzado`. `identificador-no-editable` solo puede producirse tras DEP-4, así que su caso lo añade la tarea bloqueada de DEP-4 que abre el gate `EdicionIdentificador` (ver *Tareas bloqueadas por dependencias externas*) (SC-004, FR-040, FR-041).
- [ ] T113 Revisión de seguridad del código de la feature: ningún `IgnoreQueryFilters()`, ningún SQL sin `SESSION_CONTEXT`, ningún `empresaId` leído de la solicitud, ningún secreto en `appsettings*.json`. Registrar los hallazgos en `specs/001-gestion-usuarios/checklists/revision-seguridad.md`.
- [ ] T114 Revisión manual con lector de pantalla (NVDA en Windows) del listado, el formulario y el diálogo. Registrar el resultado en `specs/001-gestion-usuarios/checklists/revision-accesibilidad.md` (quickstart, criterio de salida).
- [ ] T115 Ejecutar la validación completa de `specs/001-gestion-usuarios/quickstart.md` (V1 a V9 con implementación real y V10 y V11 con dobles), confirmar que CI está en verde y anotar en `specs/001-gestion-usuarios/checklists/revision-seguridad.md` las tareas ⛔ que siguen pendientes.
- [ ] T116 Revisión humana de todo el código generado (Principio VIII): la persona revisora confirma que comprende cada cambio y lo verifica contra la spec y la Constitución antes de integrar. Registrar la aprobación en `specs/001-gestion-usuarios/checklists/revision-seguridad.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias.
- **Foundational (Fase 2)**: depende de Setup y **bloquea** todas las historias.
- **US1 (Fase 3)** y **US2 (Fase 4)**: dependen de Foundational. US2 usa la página del listado de US1 (T051) para sus acciones en la interfaz; su API y sus pruebas de API no dependen de US1.
- **US3 (Fase 5)**: depende de Foundational y reutiliza el diálogo de US2 (T067).
- **US4 (Fase 6)**: depende de Foundational y de `RepositorioUsuarios` (T064) y `BloqueoAdministradoresEmpresa` (T063) de US2.
- **US5 (Fase 7)**: depende de que existan los endpoints que verifica (US1 a US4).
- **US6 (Fase 8)**: depende de las pantallas de US1 a US4.
- **Polish (Fase 9)**: depende de las historias que se quieran entregar.

### Tareas bloqueadas por dependencias externas

| Tarea | Bloqueada por | Se desbloquea cuando |
|-------|---------------|----------------------|
| T079 | DEP-4 | La futura spec de autenticación define si el identificador es editable. |
| T080 | DEP-4, DEP-3 | T079 y T091 están completas. |
| T090 | DEP-2 | La futura spec de autenticación define el estado inicial, la credencial y los estados adicionales. |
| T091 | DEP-3 | La futura spec de autenticación define la unicidad y el formato del identificador. |
| T092 | DEP-3 | T091 está completa y la regla de unicidad requiere un índice. |
| T093 | DEP-5 | Existe la decisión comercial sobre el límite de usuarios. |
| T094 | DEP-2, DEP-3, DEP-5 | T090 a T093 están completas. |

Son **7 tareas bloqueadas**. Las únicas dependencias hacia una tarea ⛔ son de otras tareas ⛔
(T080 → T079 y T091; T092 → T091; T094 → T090 a T093). Ninguna tarea no bloqueada depende de
una ⛔: las referencias a T080 y T094 en *Feature / release gating*, T049, T088 y T089 solo
indican cuándo se abre un gate, no una dependencia de implementación.

### Within Each User Story

- Las pruebas se escriben primero y deben fallar.
- Dominio → casos de uso → infraestructura → endpoints → interfaz.
- Las tareas sobre el mismo archivo (`UsuariosEndpoints.cs`, `ListadoUsuarios.razor`, `Usuario.cs`, `Program.cs`) son secuenciales entre sí.

### Parallel Opportunities

- Setup: T003, T004 y T005.
- Foundational: T009 a T017 y T021 (archivos distintos) y, una vez hecho T032, T034 a T037; también T039 a T042.
- En cada historia, todas las pruebas marcadas [P] se pueden hacer a la vez.
- Con Foundational terminado, la API de US1 (T048 a T050) y las reglas de US2 (T061 a T063) pueden avanzar en paralelo en lo que toca archivos distintos; los registros en `src/Kryon.Api/Program.cs` (T048, T050, T063 y T064) se integran de forma secuencial.

---

## Parallel Example: User Story 2

```text
# Pruebas de US2 a la vez:
Task: "T055 Pruebas unitarias de reglas en tests/Kryon.Core.Tests/Usuarios/ReglasEstadoTests.cs"
Task: "T056 Pruebas de API del flujo de desactivación y activación en tests/Kryon.Api.Tests/Usuarios/CambiarEstadoTests.cs"
Task: "T057 Pruebas de API de reglas y rechazos en tests/Kryon.Api.Tests/Usuarios/CambiarEstadoReglasTests.cs"
Task: "T058 Prueba de concurrencia en tests/Kryon.Api.Tests/Usuarios/UltimoAdministradorConcurrenciaTests.cs"
Task: "T059 Pruebas bUnit del diálogo en tests/Kryon.Web.Tests/Usuarios/ConfirmarDesactivacionTests.cs"
Task: "T060 Pruebas bUnit de fallo de comunicación en tests/Kryon.Web.Tests/Usuarios/FalloComunicacionTests.cs"

# Piezas independientes de US2 a la vez:
Task: "T062 ReglaUltimoAdministrador en src/Kryon.Core/Usuarios/ReglaUltimoAdministrador.cs"
Task: "T067 Diálogo en src/Kryon.Web/Usuarios/ConfirmarDesactivacion.razor"
```

---

## Implementation Strategy

### MVP First (US1)

1. Fase 1: Setup.
2. Fase 2: Foundational (incluye las pruebas de RLS y de seguridad por defecto).
3. Fase 3: US1 (listado).
4. **Detenerse y validar** con quickstart V1 y V2. Ya se puede demostrar el listado aislado por empresa.

### Entrega incremental

1. Setup + Foundational.
2. US1 → validar → demostrar (MVP).
3. US2 → validar (V3, V7, V8, V9) → MVP de P1 completo.
4. US3 → US4 (solo edición) → US5 → US6 → Polish → entrega.
5. **Registro y edición del identificador**: se entregan cuando se desbloqueen T079, T080 y T090 a T094.

### Equipo en paralelo

Tras Foundational: una persona con US1 y US3 (lectura), otra con US2 y US4 (escritura y reglas), y US5 y US6 como verificación transversal al final.

---

## Notes

- [P] = archivos distintos y sin dependencias pendientes.
- ⛔ = no iniciar: depende de una decisión que no pertenece a esta feature.
- Hacer commit por tarea o grupo lógico; detenerse en cada checkpoint para validar.
- Ninguna tarea debe introducir valores sustitutos para DEP-1 a DEP-5 ni fijar como constante un valor provisional.
