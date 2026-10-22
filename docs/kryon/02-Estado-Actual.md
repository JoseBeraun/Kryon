# Estado actual de Kryon

## Feature activa

`001-gestion-usuarios`

## Completado

T001-T054.

## US1 — Listado de usuarios

Estado: COMPLETADA A NIVEL WEB HASTA T054.

### Backend

Implementado:

- ConsultaUsuarios
- ListarUsuarios
- GET /api/usuarios
- GET /api/usuarios/roles-asignables
- políticas de acceso
- mapping Core → Contracts
- ProblemDetails de autorización
- gate RegistroUsuarios
- representación ETag

### Web

Implementado:

- `/usuarios`
- tabla
- búsqueda
- filtro por rol
- filtro por estado
- limpiar filtros
- query string
- paginación servidor
- estado vacío
- sin resultados
- sin permiso
- error de red
- región viva
- acciones por capacidades
- `puedeRegistrar`
- rol nulo
- estado activo/inactivo

## Pruebas

### US1 Web

T046:
25/25

T047:
7/7

Total:
32/32

## Pendientes conocidos

### Integración física completa

Las pruebas API que dependen de Testcontainers/Docker siguen pendientes cuando Docker no está disponible.

El entorno local sí utiliza SQL Server LocalDB para pruebas manuales.

### Datos locales

La base local puede estar vacía.

Si `/usuarios` muestra:

`No hay usuarios que mostrar`

eso no implica error del sistema.

## Siguiente etapa

Continuar desde:

T055

Agrupar tareas compatibles en bloques Turbo SDD.

## Commits confirmados previos

- `736a6f2` — T050 endpoints de listado
- `55abf2a` — T051 listado Web

- `67a952f` — T052-T054 cierre US1 Web
