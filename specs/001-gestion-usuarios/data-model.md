# Data Model: Interfaz de Gestión de Usuarios

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Convenciones: SQL Server, identificadores `uniqueidentifier`, fechas `datetimeoffset` en UTC y
texto `nvarchar` con intercalación `_CI_AI` en los campos de búsqueda. Toda tabla de empresa
tiene `EmpresaId` y está cubierta por el filtro global de EF Core y por RLS (research §R3).

Las longitudes máximas de texto son **decisiones técnicas provisionales y configurables**
(plan, *Technical Context*), no requisitos de negocio: la spec no las fija.

---

## Empresa *(referencia; la gestiona otra feature)*

| Campo | Tipo | Notas |
|-------|------|-------|
| `Id` | uniqueidentifier | PK. Es la frontera de aislamiento. |

Esta feature solo la lee a través del contexto de solicitud y no la crea ni la modifica.

## Rol *(referencia; lo gestiona el catálogo de roles, fuera de alcance)*

| Campo | Tipo | Notas |
|-------|------|-------|
| `Id` | uniqueidentifier | PK |
| `EmpresaId` | uniqueidentifier | FK → Empresa. Solo son asignables los roles de la empresa actual (FR-026). |
| `Nombre` | texto | Se muestra en el listado, el detalle, el formulario y el filtro. Su tipo y longitud los define el catálogo de roles. |
| `Permisos` | colección de nombres | Esta feature solo lee las capacidades de research §R4, cuyos nombres son **provisionales** y configurables. Un rol que incluye la capacidad de acceso a la gestión de usuarios (provisionalmente `usuarios.administrar`) convierte a sus usuarios en administradores. |

## Usuario

| Campo | Tipo | Obligatorio | Validación / regla | Requisito |
|-------|------|-------------|--------------------|-----------|
| `Id` | uniqueidentifier | Sí | Lo genera el servidor. | — |
| `EmpresaId` | uniqueidentifier | Sí | Se toma **siempre** de `IContextoSolicitud`. No existe en ningún DTO de entrada. Es inmutable. | FR-001, FR-023 |
| `NombreCompleto` | nvarchar(200) | Sí | Se recortan los espacios y no puede quedar vacío. Longitud máxima: 200, **provisional y configurable** (no es requisito de negocio). | FR-024, FR-025 |
| `IdentificadorAcceso` | nvarchar(256) | Sí | Obligatorio. Longitud máxima: 256, **provisional y configurable**. El **formato** y la **unicidad** dependen de la futura spec de autenticación, a través de `IPoliticaIdentificadorAcceso` (DEP-3). Si es **editable** tras el registro también depende de esa spec (DEP-4, `PermiteEdicion`); este modelo no lo decide. | FR-024, FR-027 |
| `RolId` | uniqueidentifier | Sí | Debe ser un rol de la empresa actual. Nadie puede cambiar su propio `RolId`. No puede dejar a la empresa sin administradores activos. | FR-026, FR-035, FR-036 |
| `Estado` | tinyint (enum `EstadoUsuario`) | Sí | `Activo = 1`, `Inactivo = 2`. El valor inicial al registrar y cualquier estado adicional dependen de la futura spec de autenticación (DEP-2), a través de `IPoliticaAltaUsuario`. | FR-010, FR-030 |
| `Version` | rowversion | Sí | Token de concurrencia, expuesto como ETag. | FR-038, FR-039 |
| `CreadoEn` | datetimeoffset | Sí | UTC. | — |
| `ModificadoEn` | datetimeoffset | Sí | UTC. | — |

**Índices**: `UX` sobre `Id`; `IX (EmpresaId, NombreCompleto, Id)`; `IX (EmpresaId, IdentificadorAcceso)`; `IX (EmpresaId, Estado, RolId)`.
El índice único del identificador **no se crea** en esta feature, porque su ámbito (por empresa o en todo Kryon) es DEP-3.

**No hay borrado**: no existe ninguna operación DELETE sobre `Usuario` (FR-033, "Fuera de Alcance").

### Transiciones de estado

```text
                    (alta: estado inicial decidido por IPoliticaAltaUsuario — DEP-2)
                                          │
                                          ▼
          ┌──────────── Desactivar (con confirmación; FR-032) ────────────┐
          │                                                               ▼
       Activo                                                          Inactivo
          ▲                                                               │
          └────────────── Activar (sin confirmación; FR-032) ─────────────┘
```

Precondiciones de **Desactivar** (se evalúan en el servidor, dentro de la transacción con el bloqueo por empresa):
1. El actor tiene la capacidad de desactivar (nombre provisional `usuarios.desactivar`; FR-006).
2. El usuario pertenece a la empresa actual (si no, 404; FR-002 y FR-003).
3. El usuario no es el actor (`no-puede-desactivarse-a-si-mismo`; FR-034).
4. `If-Match` coincide con `Version` (si no, 412; FR-038).
5. El usuario está Activo (si no, 412 con el estado actual; EC-2).
6. Si el usuario es administrador, después de desactivarlo debe quedar al menos un administrador activo (`ultimo-administrador`; FR-036).

Precondiciones de **Activar**: 1 (con la capacidad de activar), 2, 4 y el usuario está Inactivo.
Si reactivar cuenta para un límite de usuarios es parte de DEP-5 y no lo define esta feature.

Estados adicionales (p. ej. "Pendiente"): los decidirá DEP-2. El enum se amplía si esa spec lo define.

## AuditoriaUsuario *(solo inserción)*

| Campo | Tipo | Notas |
|-------|------|-------|
| `Id` | bigint identity | PK |
| `EmpresaId` | uniqueidentifier | La empresa del **actor**. |
| `ActorUsuarioId` | uniqueidentifier | Quién realizó la acción. |
| `OcurridoEn` | datetimeoffset | UTC, asignado por el servidor. |
| `TipoOperacion` | tinyint (enum) | `Alta`, `Modificacion`, `Activacion`, `Desactivacion`, `AccesoDenegadoOtraEmpresa`. |
| `UsuarioAfectadoId` | uniqueidentifier | El usuario de la empresa sobre el que se actuó. En `AccesoDenegadoOtraEmpresa` es el identificador **solicitado**, sin ningún dato del recurso. |
| `CambiosJson` | nvarchar(max), null | Solo en `Modificacion`: `[{"campo":"RolId","anterior":"…","nuevo":"…"}]`, únicamente con los campos que cambiaron (FR-044). |

Reglas:
- Se inserta en la misma transacción que la operación. Una operación rechazada o cancelada no genera un registro de ese tipo (FR-032, EC-2, EC-3).
- El rol de base de datos de la aplicación solo tiene `INSERT` y `SELECT` sobre esta tabla (FR-045, research §R7).
- Nunca contiene credenciales, tokens ni secretos (FR-045).

## Valores calculados (no persistidos)

- **Administrador activo**: un `Usuario` con `Estado = Activo` cuyo rol incluye la capacidad de acceso a la gestión de usuarios (nombre provisional `usuarios.administrar`; FR-036).
- **accionesPermitidas** (por usuario, calculado en la API; FR-007). Los nombres de permiso son provisionales (research §R4):

| Acción | Condición |
|--------|-----------|
| `verDetalle` | Siempre, para quien tiene acceso a la gestión. |
| `editar` | El actor tiene `usuarios.editar`. |
| `cambiarRol` | `editar` y además el usuario no es el actor (FR-035). |
| `editarIdentificador` | `editar` y lo que determine `IPoliticaIdentificadorAcceso.PermiteEdicion`. Depende de DEP-4, que esta feature no decide. |
| `activar` | El actor tiene `usuarios.activar` y el usuario está Inactivo. |
| `desactivar` | El actor tiene `usuarios.desactivar`, el usuario está Activo y no es el actor. La regla del último administrador **no** oculta la acción: se valida al ejecutar y se informa del motivo (FR-036). |

- **esCuentaPropia**: `usuario.Id == actor.Id`. En la interfaz se muestra como "Tú" (FR-012).
