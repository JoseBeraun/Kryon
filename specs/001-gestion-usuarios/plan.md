# Implementation Plan: Interfaz de Gestión de Usuarios

**Branch**: `001-gestion-usuarios` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-gestion-usuarios/spec.md`

## Summary

El administrador del negocio gestiona las cuentas de usuario de **su** empresa: listado con
búsqueda, filtros y paginación; detalle; registro y edición (nombre completo, identificador de
acceso y rol); activación y desactivación con confirmación; y visibilidad de acciones según
permisos. Las reglas centrales son el aislamiento multiempresa, la protección del último
administrador activo, la prohibición de autodesactivarse o cambiarse el propio rol, el control de
concurrencia y la trazabilidad inmutable de las operaciones críticas.

Enfoque técnico: una **ASP.NET Core Web API** (.NET 10, C#) que es la única autoridad de
permisos y empresa; una interfaz **Blazor WebAssembly** que consume esa API; **SQL Server** con
**Entity Framework Core**. El aislamiento se aplica en dos capas: filtros globales de EF Core y
Row-Level Security de SQL Server. La concurrencia se controla con `rowversion` expuesto como
ETag. El destino de despliegue es **Microsoft Azure**. Las decisiones que pertenecen a la futura
spec de autenticación (DEP-1 a DEP-4) y la decisión comercial del límite (DEP-5) **no se resuelven
en este plan**. Se aíslan detrás de puntos de integración explícitos cuyo comportamiento definirán
esas specs (ver [research.md](research.md) §R8 y la sección *Dependencias externas*).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS)

**Primary Dependencies**: ASP.NET Core Web API, Blazor WebAssembly, Entity Framework Core 10 (proveedor SQL Server)

**Storage**: Microsoft SQL Server (Azure SQL Database en la nube; SQL Server en contenedor para desarrollo y pruebas)

**Testing**: xUnit; `WebApplicationFactory` + Testcontainers (SQL Server) para integración de la API; bUnit para componentes Blazor; Playwright para .NET + Deque axe-core para flujos end-to-end y accesibilidad

**Target Platform**: API en Azure App Service (Linux); interfaz Blazor WebAssembly en Azure Static Web Apps; Azure SQL Database; Azure Key Vault para secretos

**Project Type**: Aplicación web (API + interfaz web SPA)

**Performance Goals**: El negocio no definió ninguno y el plan no inventa criterios de aceptación. Como decisión de diseño, el listado se pagina y filtra en el servidor para que el costo de cada consulta dependa del tamaño de página y no del total de usuarios (ver research §R6).

**Constraints**: La empresa siempre se deriva del contexto verificado, nunca de un parámetro del cliente (Principio I). La autorización siempre se valida en el servidor (Principio II). Se deniega por defecto (Principio VI). La trazabilidad no se puede alterar (Principio V). Objetivo de accesibilidad: WCAG 2.2 nivel AA en las pantallas de esta feature.

**Scale/Scope**: 1 feature, 6 historias de usuario, 7 endpoints REST, 4 pantallas o diálogos (listado, detalle, formulario de registro y edición, confirmación de desactivación). Si existe un límite de usuarios por empresa, es una decisión externa (DEP-5); el diseño no supone ninguna cifra.

**Decisiones técnicas provisionales y configurables** (no son requisitos de negocio; la spec no fija estos valores y se pueden cambiar por configuración sin modificar la spec):

| Valor | Decisión provisional | Dónde se configura | Justificación |
|-------|----------------------|--------------------|---------------|
| Tamaño de página | 25 por defecto, máximo 100 | Opciones de la API (`Usuarios:Paginacion`) | Hace falta un valor para paginar en el servidor (FR-018); la spec dice explícitamente que no lo define. |
| Longitud máxima del nombre completo | 200 caracteres | Opciones de validación (`Usuarios:Validacion`) y tamaño de columna | Toda columna de texto necesita un tope técnico. Si el negocio lo define, se ajusta. |
| Longitud máxima del identificador de acceso | 256 caracteres | Opciones de validación y tamaño de columna | Es un tope técnico provisional. Su formato y reglas definitivas dependen de la futura spec de autenticación (DEP-3). |
| Nombres de permisos (`usuarios.administrar`, `usuarios.registrar`, `usuarios.editar`, `usuarios.activar`, `usuarios.desactivar`) | Nombres **provisionales** de contrato | Un mapa de configuración de políticas de autorización a permisos del catálogo | La spec no define un catálogo de permisos. Esta feature necesita nombrar las capacidades que consume; el catálogo de roles definirá los nombres reales (research §R4). |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio / Puerta | Cómo lo cumple el plan | Estado |
|--------------------|------------------------|--------|
| **I. Aislamiento multiempresa** | `EmpresaId` sale de la sesión verificada (`IContextoSolicitud`) y nunca de la solicitud. Hay filtro global de EF Core por empresa y RLS de SQL Server con `SESSION_CONTEXT` como segunda capa. Un recurso de otra empresa responde 404, igual que uno inexistente. Hay pruebas A/B obligatorias en la API y en la base de datos (quickstart §V1). | ✅ |
| **II. Control de acceso por rol** | Cada endpoint usa políticas de autorización del servidor. Cada política se asocia a un permiso del catálogo mediante configuración; los nombres actuales son provisionales (research §R4). La interfaz solo oculta acciones a partir de `accionesPermitidas`, calculadas por la API. Hay pruebas de casos denegados para cada operación. | ✅ |
| **III. Especificación antes de implementación** | La spec está revisada y tiene 3 sesiones de clarify. Las dependencias externas están registradas (DEP-1 a DEP-5) y el plan **no fija** reglas que pertenecen a otras specs: diseña los puntos de integración y deja su comportamiento a esas specs. Las partes cuyo comportamiento depende de ellas no se implementan hasta que estén definidas (sección *Dependencias externas*). | ✅ |
| **IV. Criterios verificables y pruebas** | Cada SC-001 a SC-009 tiene un escenario de validación en [quickstart.md](quickstart.md) y cada código de error en [contracts/](contracts/) tiene al menos una prueba. | ✅ |
| **V. Trazabilidad** | Tabla `AuditoriaUsuarios` de solo inserción, escrita en la misma transacción que la operación. El usuario de la base de datos de la aplicación no tiene permisos UPDATE ni DELETE sobre ella. Registra el actor, el momento (UTC), la empresa, el usuario afectado, el tipo de operación y los cambios con su valor anterior y el nuevo. Nunca guarda credenciales. | ✅ |
| **VI. Seguridad por defecto** | Las políticas de autorización deniegan si falta el permiso o si la asociación política→permiso no está configurada. Los errores usan ProblemDetails sin detalles internos. No se entrega ninguna operación cuyo comportamiento dependa de una decisión externa todavía indefinida (DEP-2 a DEP-5), así que no existe un estado ambiguo en producción. | ✅ |
| **VII. Desarrollo incremental** | Se entrega por historias: P1 (listado y activar/desactivar) → P2. `/speckit-tasks` generará tareas pequeñas con dependencias. | ✅ |
| **VIII. Uso responsable de IA** | Todo artefacto generado requiere revisión humana antes de integrarse. El quickstart define una validación observable para cada criterio. | ✅ (proceso) |
| **Accesibilidad (estándares)** | Operación completa con teclado, nombres accesibles, foco gestionado en diálogos, mensajes anunciados (live regions) y pruebas axe automáticas más una revisión manual. | ✅ |
| **Puertas de calidad** | Existe prueba de aislamiento A/B, validación de rol y empresa en el servidor para cada operación y registro de todas las operaciones críticas. | ✅ |

**Resultado**: el plan pasa la verificación sin violaciones. La complejidad añadida está justificada en *Complexity Tracking*.

**Re-verificación post-diseño (Fase 1)**: ✅ El modelo de datos, los contratos y el quickstart mantienen todas las puertas. Notas:
1. El contrato no acepta `empresaId` en ninguna solicitud.
2. El 404 es indistinguible entre un recurso de otra empresa y uno inexistente.
3. El contrato del punto de integración de unicidad (DEP-3) no permite devolver en qué empresa existe un duplicado.
4. Ningún artefacto de diseño fija comportamiento para DEP-1 a DEP-5. Los valores provisionales (página, longitudes, nombres de permisos) están marcados como configurables.

## Project Structure

### Documentation (this feature)

```text
specs/001-gestion-usuarios/
├── plan.md              # Este archivo
├── research.md          # Fase 0: decisiones técnicas
├── data-model.md        # Fase 1: entidades, validaciones y estados
├── quickstart.md        # Fase 1: guía de validación
├── contracts/
│   ├── usuarios-api.openapi.yaml   # Contrato REST de la API
│   └── ui-usuarios.md              # Contrato de interfaz y accesibilidad
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks; no la crea este comando)
```

### Source Code (repository root)

```text
Kryon.sln
src/
├── Kryon.Api/                     # ASP.NET Core Web API (única autoridad de permisos y empresa)
│   ├── Usuarios/                  # Endpoints de esta feature, mapeo de errores a ProblemDetails
│   └── Seguridad/                 # IContextoSolicitud, políticas de autorización, auditoría de denegaciones
├── Kryon.Core/                    # Dominio + casos de uso (sin dependencias de infraestructura)
│   └── Usuarios/                  # Usuario, reglas (último admin, autogestión), casos de uso, puntos de integración DEP-*
├── Kryon.Infrastructure/          # EF Core, SQL Server, RLS, auditoría, bloqueo por empresa
│   ├── Persistencia/              # KryonDbContext, configuraciones, migraciones, interceptor de SESSION_CONTEXT
│   └── Usuarios/                  # Repositorio de usuarios; adaptadores de los puntos de integración cuando existan
├── Kryon.Contracts/               # DTOs compartidos entre la API y Blazor (solicitudes, respuestas, códigos de error)
└── Kryon.Web/                     # Blazor WebAssembly
    └── Usuarios/                  # Páginas: listado, detalle, formulario; diálogo de confirmación

tests/
├── Kryon.Core.Tests/              # Unitarias: reglas de dominio y casos de uso
├── Kryon.Api.Tests/               # Integración: API + SQL Server real (Testcontainers), aislamiento A/B, concurrencia
├── Kryon.Web.Tests/               # bUnit: componentes, acciones visibles, foco, mensajes
└── Kryon.E2E.Tests/               # Playwright + axe: flujos completos con teclado y accesibilidad
```

**Structure Decision**: aplicación web con una API y una SPA Blazor en una sola solución .NET.
`Kryon.Core` concentra el dominio y los casos de uso, así que las reglas de negocio se prueban sin
base de datos. `Kryon.Contracts` evita duplicar DTOs entre la API y Blazor, porque ambos usan C#.
Cada proyecto organiza su código por feature (`Usuarios/`), para que las futuras features de Kryon
se añadan como carpetas hermanas.

## Dependencias externas y puntos de integración

Este plan **no resuelve** DEP-1 a DEP-5 ni define su comportamiento. Solo diseña el punto donde
esta feature se conecta con la decisión externa, para que integrarla no obligue a rehacer la
feature. Mientras una dependencia no esté definida, **no se implementa ni se entrega** la parte
de la feature que depende de ella. No se inventa ningún comportamiento sustituto.

| Dependencia (spec) | Punto de integración en `Kryon.Core` | Qué diseña este plan | Qué NO define este plan |
|--------------------|---------------------------------------|----------------------|-------------------------|
| DEP-1 Sesiones abiertas al desactivar | Evento de dominio `UsuarioDesactivado` | La desactivación publica el evento después de confirmarse la transacción. | Qué se hace con las sesiones del usuario desactivado. |
| DEP-2 Estado inicial, credencial inicial y estados adicionales | `IPoliticaAltaUsuario` | El caso de uso de registro pide a este puerto el estado inicial y le delega la entrega de la credencial, dentro de la transacción del alta. | El estado inicial, el mecanismo de credencial y si hay estados adicionales. |
| DEP-3 Unicidad y formato del identificador | `IPoliticaIdentificadorAcceso` (`ValidarFormato`, `EstaEnUso`) | El registro, y la edición si DEP-4 lo permite, consultan este puerto. `EstaEnUso` solo responde sí o no, así que el contrato no permite revelar otra empresa (FR-027). | El ámbito de la unicidad y las reglas de formato. |
| DEP-4 Editabilidad del identificador | `IPoliticaIdentificadorAcceso.PermiteEdicion` | La API expone `acciones.editarIdentificador` y acepta `identificadorAcceso` en la edición. La interfaz muestra el campo editable o de solo lectura según esa bandera. Así la API y la interfaz se adaptan sin cambios cuando DEP-4 se resuelva. | Si el identificador es editable o no. |
| DEP-5 Límite de usuarios | `IFuenteLimiteUsuarios` | Aplica la única regla de la spec (FR-028): si la fuente externa informa que existe un límite aplicable y se alcanzó, el registro se rechaza con `limite-usuarios-alcanzado`. | Si existe un límite, su cifra, a quién aplica y cómo se cuenta (p. ej. inactivos, reactivaciones). |

**Consecuencia para la entrega**:
- **Se entregan sin depender de DEP-2 a DEP-5**: las historias 1, 2, 3, 5 y 6, y la edición de nombre completo y rol de la historia 4.
- **Dependen de la futura spec de autenticación y de la decisión comercial**: el registro de usuarios (FR-020, FR-027, FR-028) y la edición del identificador (DEP-4). Se diseñan y sus contratos quedan fijados, pero su implementación final y su entrega esperan a que DEP-2, DEP-3, DEP-4 y DEP-5 estén definidas. `/speckit-tasks` debe marcar esas tareas como bloqueadas por esas dependencias.

## Complexity Tracking

> No hay violaciones de la Constitución. Estas entradas justifican la complejidad añadida, como pide la sección *Governance*.

| Decisión | Por qué se necesita | Alternativa más simple descartada porque |
|----------|---------------------|------------------------------------------|
| Aislamiento en dos capas (filtro de EF Core + RLS de SQL Server) | Una sola omisión de filtro en una consulta sería una fuga entre empresas (Principio I: "incidente de confidencialidad"). | Con solo el filtro de EF Core, una consulta SQL directa o un `IgnoreQueryFilters()` accidental expondrían datos sin que nada lo impida. |
| 5 proyectos en `src/` | Separan dominio sin infraestructura (pruebas rápidas de reglas), contratos compartidos y los dos ejecutables (API y SPA). | Un único proyecto mezclaría Blazor WASM (que corre en el navegador) con EF Core y SQL Server, que nunca deben llegar al navegador. |
| Bloqueo de aplicación por empresa (`sp_getapplock`) en operaciones que afectan administradores | FR-036 exige que la regla del último administrador se cumpla ante solicitudes simultáneas (EC-6, SC-006). | El control optimista por fila no protege: dos desactivaciones cruzadas modifican filas distintas y ambas pasarían. |
