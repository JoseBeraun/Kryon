---
name: kryon-multitenancy
description: "Reglas de aislamiento multiempresa (multitenancy) de Kryon. Úsala siempre que el trabajo toque la empresa o el tenant del usuario: EmpresaId/TenantId, datos o consultas tenant-aware, EF Core (filtros, consultas, relaciones), SQL Server y Row-Level Security, seguridad o fugas cross-tenant, pruebas de aislamiento entre empresas, auditoría o analítica/KPIs por empresa, y revisiones de seguridad multiempresa. Complementa a kryon-sdd."
---

# Kryon: aislamiento multiempresa

Kryon es un ERP SaaS multiempresa. El aislamiento entre empresas es el Principio I de la
Constitución (**no negociable**): una única fuga entre empresas es un incidente de
confidencialidad, no un defecto menor.

Esta skill complementa a `kryon-sdd`. La precedencia de artefactos, el trabajo por tareas y el
formato de reporte están en `kryon-sdd`; aquí solo están las reglas de aislamiento. Los detalles
técnicos concretos (nombres, tablas, predicados, contratos) están en el `plan.md`, `research.md`,
`data-model.md` y `contracts/` de cada feature. Esta skill no los sustituye.

## 1. Principio fundamental

Toda operación de negocio pertenece a una empresa (tenant), y toda entidad con datos de negocio
está asociada a una empresa identificable.

> **Ningún dato de una empresa puede ser leído, modificado, relacionado, contado, auditado o
> expuesto desde el contexto de otra empresa.**

La única excepción es un acceso entre empresas que el negocio haya autorizado explícitamente, que
esté registrado como tal y sea verificable y auditable. Ninguna feature actual lo define; no lo
supongas.

El aislamiento **falla de forma cerrada**. Si una operación que necesita tenant no tiene uno
confiable:
- no lo adivines;
- no uses un tenant por defecto;
- no elijas el primero disponible;
- rechaza o detén la operación como indiquen los artefactos de la feature.

## 2. Fuente confiable del tenant

- El tenant efectivo sale del **contexto de solicitud** de Kryon, derivado de la sesión o credencial verificada en el servidor. En la arquitectura actual ese concepto se llama `IContextoSolicitud`. Úsalo como lo defina la feature; una feature futura puede definir otro mecanismo por su propia spec.
- **Nunca** uses como autoridad de aislamiento un `EmpresaId`/`TenantId` enviado por el cliente en el body, la query string, la ruta, un header, un formulario o JavaScript, salvo que una spec defina explícitamente un mecanismo seguro y autorizado para ello.
- Un identificador recibido del cliente puede servir para **localizar** un recurso dentro de la empresa actual, nunca para **decidir** a qué empresa pertenece la sesión.
- Si un contrato de entrada trae un campo de empresa que la feature no autoriza, no lo uses para aislar. Repórtalo si contradice el contrato.

## 3. Lecturas

Toda consulta de datos tenant-aware se limita a la empresa actual. Incluye:
- obtener por id, listados, búsquedas, filtros y paginación;
- conteos, totales y agregados;
- joins e `Include`s;
- validaciones de existencia y validaciones de unicidad cuyo ámbito sea la empresa;
- exportaciones, informes, notificaciones y métricas.

**Nunca** consultes de forma global, traigas el resultado y filtres después en memoria. El
aislamiento forma parte de la consulta.

## 4. Escrituras

Antes de crear, modificar, activar, desactivar o relacionar datos:
- comprueba que **todas** las entidades relacionadas pertenecen a la empresa actual (por ejemplo, un rol, una categoría o un documento referenciado por id);
- no permitas relaciones entre empresas;
- no dejes que el cliente asigne la empresa efectiva: se establece desde el contexto confiable o según el mecanismo que definan los artefactos;
- no muevas en silencio una entidad de una empresa a otra; la empresa de una entidad no cambia salvo que una spec lo defina.

## 5. EF Core

- Aplica el aislamiento con los mecanismos que prevé el plan de la feature, incluidos los filtros de consulta por tenant cuando el plan los defina.
- Los filtros de EF Core son **una defensa de la aplicación**, no un permiso para olvidar el aislamiento en servicios, validaciones o contratos.
- **No uses `IgnoreQueryFilters()`** en código de negocio normal, salvo que una tarea y el diseño lo autoricen explícitamente.
- No sustituyas una estrategia de aislamiento aprobada por otra porque "sea más simple".
- El SQL escrito a mano también debe quedar aislado; no puede apoyarse en que "EF lo filtra".

## 6. SQL Server y Row-Level Security

Cuando el plan contemple Row-Level Security (RLS) como defensa en profundidad:
- RLS es una **segunda barrera**, no sustituye el aislamiento en la aplicación.
- El contexto que usa RLS se deriva de información confiable del servidor (el contexto de solicitud), **nunca** directamente de un dato enviado por el cliente.
- Con conexiones reutilizadas (pooling), el contexto de un tenant no puede quedar visible para otra solicitud. Establécelo y protégelo como indique el diseño de la feature.
- Los nombres de funciones, políticas, claves de contexto de sesión y scripts están en el `plan.md`, `research.md` y `data-model.md` de la feature. No los inventes ni los cambies desde esta skill.

## 7. Defensa en profundidad

En funcionalidades tenant-aware, verifica el aislamiento en cada capa que el diseño establezca:

```text
contexto de solicitud → aplicación/servicio → consulta EF Core → SQL Server/RLS (si corresponde)
→ serialización/respuesta → auditoría
```

Que exista una barrera no justifica eliminar las demás cuando los artefactos establecen varias.

## 8. Respuestas y filtración de información

- No reveles que un recurso existe en otra empresa. Un mensaje como "el usuario existe pero pertenece a otra empresa" permite enumerar datos entre empresas.
- Los errores externos siguen el contrato de la feature y usan la respuesta neutral que ese contrato defina.
- Los datos de otra empresa no aparecen en listados, agregados, informes, exportaciones, búsquedas, notificaciones, mensajes de error ni logs.
- Esta skill **no** define códigos HTTP ni códigos funcionales: salen de los contratos de la feature.

## 9. Auditoría

- Los registros de auditoría también están aislados: no mezcles eventos de empresas ni muestres a una empresa la auditoría de otra.
- La empresa del evento sale del contexto confiable o de una entidad ya validada, nunca de un valor no confiable del cliente.
- No infieras ni registres la empresa propietaria de un recurso que el actor no puede ver.
- No registres secretos ni credenciales, ni información de otra empresa innecesaria en los mensajes.

## 10. Pruebas multiempresa

La Constitución exige que toda feature tenga al menos una prueba que demuestre que la empresa A no
obtiene datos de la empresa B. Cuando una tarea involucre datos tenant-aware, usa este checklist de
amenazas para **elegir** las pruebas que la tarea autoriza. No generes todas automáticamente:

- A accede a sus propios datos, y B a los suyos.
- A no puede leer ni modificar datos de B.
- A no puede relacionar sus entidades con entidades de B.
- Búsquedas, listados, paginación, conteos y totales no incluyen datos de B.
- Un identificador conocido de B no permite saltarse el aislamiento, y la respuesta no revela que existe.
- La auditoría no filtra información entre empresas.
- Las operaciones concurrentes no rompen el aislamiento.
- Los filtros de la aplicación y RLS no contradicen el comportamiento esperado.

Las pruebas usan al menos dos empresas con datos propios. Una prueba con una sola empresa no
demuestra aislamiento.

## 11. Identidad de prueba

- El mecanismo de identidad para Development y Test que defina la feature (hoy, `IdentidadPrueba`) **nunca** es autenticación de producción.
- Respeta las restricciones de entorno que fijen `plan.md` y `research.md`.
- Nunca lo sugieras como autenticación real ni quites sus restricciones para facilitar las pruebas.

## 12. Frontend

- El frontend **no** es una frontera de seguridad. Ocultar botones, rutas o campos mejora la experiencia de uso, pero no sustituye la autorización ni el aislamiento del backend.
- Nunca decidas la empresa autorizada solo con información guardada en el navegador.
- El frontend no envía una empresa para que el servidor la use como autoridad.

## 13. Analítica y KPIs

- Toda consulta analítica o KPI sobre datos tenant-aware respeta el aislamiento.
- El agente de analítica es de solo lectura y no agrega datos de varias empresas salvo que una spec futura autorice explícitamente un contexto entre empresas.
- No se inventan KPIs ni fórmulas.
- "Solo estoy leyendo" no es motivo para saltarse el aislamiento.

## 14. Operaciones globales

- No todas las operaciones son tenant-aware: migraciones, infraestructura o futuros procesos administrativos globales pueden necesitar otro contexto.
- Una operación global debe estar **autorizada explícitamente por diseño** (spec, plan o tarea).
- La ausencia de tenant **no** convierte una operación en global: es un error que falla cerrado.
- Nunca crees un bypass general que el código de negocio pueda reutilizar.

## 15. Checklist de revisión

Al revisar código multiempresa, busca específicamente:
- consultas sin tenant, "obtener por id" globales y conteos globales;
- joins o `Include`s que puedan traer datos de otra empresa;
- validaciones de existencia o unicidad globales cuando su ámbito es la empresa;
- `EmpresaId`/`TenantId` tomado del cliente;
- `IgnoreQueryFilters()`;
- SQL directo sin aislamiento;
- contexto de RLS que no se establece o puede filtrarse entre solicitudes;
- cachés sin la empresa en la clave;
- logs o auditoría con datos de otra empresa;
- respuestas que revelan la existencia de recursos ajenos;
- pruebas que usan una sola empresa.

## 16. Ante la duda

Si hace falta una regla multiempresa que los artefactos no definen:
- no la inventes;
- no la copies del Kryon antiguo;
- no la supongas por "buenas prácticas".

Detente y reporta qué decisión falta, siguiendo `kryon-sdd`.
