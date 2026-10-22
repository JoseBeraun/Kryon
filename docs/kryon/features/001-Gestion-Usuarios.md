# 001 — Gestión de Usuarios

## Estado

Feature activa de Kryon.

Branch:

`001-gestion-usuarios`

Progreso actual:

- [x] T001-T054
- [ ] T055 en adelante

## Documentos oficiales SpecKit+

- [[../../../specs/001-gestion-usuarios/spec]]
- [[../../../specs/001-gestion-usuarios/plan]]
- [[../../../specs/001-gestion-usuarios/tasks]]
- [[../../../specs/001-gestion-usuarios/research]]
- [[../../../specs/001-gestion-usuarios/data-model]]

Los archivos de `specs/001-gestion-usuarios` son la fuente oficial de verdad.

Esta nota solo funciona como resumen y navegación.

---

# US1 — Listado de usuarios

Estado:

COMPLETADA A NIVEL WEB HASTA T054.

## Backend

Implementado:

- ConsultaUsuarios
- búsqueda por texto
- filtro por rol
- filtro por estado
- ordenamiento
- paginación servidor
- aislamiento por empresa
- GET /api/usuarios
- GET /api/usuarios/roles-asignables
- autorización mediante políticas
- ProblemDetails contractual para 401/403
- GateRegistroUsuarios
- strong ETag basado en rowversion

## Web

Ruta:

`/usuarios`

Implementado:

- listado de usuarios
- búsqueda por nombre o identificador
- filtro por rol
- filtro por estado
- limpiar filtros
- query string
- paginación servidor
- estados activo/inactivo
- indicador "Tú"
- acciones permitidas
- botón Nuevo usuario controlado por backend
- estado vacío
- estado sin resultados
- estado sin permiso
- error de red
- región viva accesible

## Filtros

Componente:

`FiltrosUsuarios.razor`

Incluye:

- Buscar por nombre o identificador
- Rol
- Estado
- Todos
- Limpiar filtros
- resumen de criterios activos

El texto de búsqueda se aplica al enviar.

Los selects aplican inmediatamente.

Cuando cambia un criterio:

`pagina` vuelve a 1.

`tamanoPagina` se conserva.

## Paginación

Componente:

`PaginacionUsuarios.razor`

Incluye:

- Anterior
- Siguiente
- Página X de Y
- N usuarios
- navegación accesible

La paginación es servidor.

## Estados Web T054

Estado vacío:

`No hay usuarios que mostrar`

Sin resultados:

`No hay usuarios que coincidan con la búsqueda y los filtros`

Error de red:

`No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo.`

## Tests

T046:

25/25

T047:

7/7

Total US1 Web:

32/32

Validación adicional bUnit:

22/22

Regresión API ligera:

10/10

## Build

- 0 errores
- 0 warnings
- format limpio
- git diff --check limpio

---

# Backend T050

Endpoints:

`GET /api/usuarios`

`GET /api/usuarios/roles-asignables`

Política:

`Usuarios.Acceder`

Archivos principales:

- UsuariosEndpoints.cs
- VersionEtag.cs
- GateRegistroUsuarios.cs
- RespuestaAutorizacion.cs

## Gate RegistroUsuarios

Verifica disponibilidad de:

- IPoliticaAltaUsuario
- IPoliticaIdentificadorAcceso
- IFuenteLimiteUsuarios

No instancia ni ejecuta esos servicios.

`puedeRegistrar` depende de:

capacidad de registro + gate abierto.

---

# Web T051

Archivo principal:

`ListadoUsuarios.razor`

Ruta:

`/usuarios`

T051 introdujo la estructura inicial del listado.

T052-T054 completaron:

- filtros
- paginación
- estados UX

---

# Multiempresa

El frontend no envía EmpresaId como criterio de negocio.

El contexto de empresa se obtiene desde la identidad autenticada.

Cadena:

Identidad
↓
IContextoSolicitud
↓
EF
↓
SESSION_CONTEXT
↓
RLS

---

# Entorno de desarrollo

Web:

`http://localhost:5106`

API:

`http://localhost:5292`

Base:

`Kryon`

Motor:

SQL Server LocalDB

La base local puede estar vacía.

Por eso `/usuarios` puede mostrar correctamente:

`No hay usuarios que mostrar`

---

# Riesgos / pendientes conocidos

## Datos locales

Pendiente localizar o reutilizar un mecanismo existente de seed/datos de prueba antes de insertar usuarios manualmente.

No crear inserts SQL improvisados sin revisar:

- RLS
- auditoría
- relaciones
- tenant
- identidad de desarrollo

## Docker

Algunas pruebas de integración física dependen de Docker/Testcontainers.

Cuando Docker no está disponible, esas pruebas permanecen pendientes.

## Paginación manual fuera de rango

Una URL introducida manualmente con una página superior al total puede mostrar algo como:

`Página 10 de 3`

No era requisito de T052-T054.

---

# Commits conocidos

T050:

`736a6f2 feat(usuarios): implementar endpoints de listado T050`

T051:

`55abf2a feat(usuarios): implementar listado web T051`

El hash definitivo de T052-T054 debe obtenerse de Git y agregarse cuando se confirme.

---

# Siguiente etapa

Continuar desde T055.

Antes de implementar:

1. leer tasks.md;
2. identificar qué pide exactamente T055 en adelante;
3. revisar dependencias;
4. formar un bloque Turbo seguro;
5. implementar;
6. ejecutar tests;
7. ejecutar build;
8. revisar diff;
9. commit del bloque.

---

# Navegación

- [[../00-Control-Center]]
- [[../01-Arquitectura-Kryon]]
- [[../02-Estado-Actual]]
- [[../03-Entorno-Local]]
- [[../04-Roadmap-Kryon]]
