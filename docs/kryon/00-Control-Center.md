# Kryon â€” Control Center

## Estado actual

**Proyecto:** Kryon ERP SaaS multiempresa
**MetodologÃ­a:** SDD + SpecKit+
**Feature activa:** `001-gestion-usuarios`
**Branch:** `001-gestion-usuarios`

## Progreso actual

- [x] T001-T054
- [ ] T055 en adelante

### Ãšltimo bloque implementado

**T052-T054 â€” Cierre de US1 Web**

Resultado:

- T046: 25/25
- T047: 7/7
- Total US1 Web: 32/32
- Build: 0 errores
- Warnings: 0
- Format: OK
- git diff --check: OK

## Estado funcional

### GestiÃ³n de Usuarios

- [x] Listado
- [x] BÃºsqueda
- [x] Filtro por rol
- [x] Filtro por estado
- [x] Limpiar filtros
- [x] PaginaciÃ³n
- [x] Estado vacÃ­o
- [x] Sin resultados
- [x] Sin permiso
- [x] Error de red
- [ ] Activar / desactivar
- [ ] Detalle
- [ ] Registro
- [ ] EdiciÃ³n

## Entorno local

**Web:**
http://localhost:5106

**API:**
http://localhost:5292

**Base de datos:**
SQL Server LocalDB

**Instancia:**
MSSQLLocalDB

**Base:**
Kryon

## ConfiguraciÃ³n local importante

`src/Kryon.Web/wwwroot/appsettings.Development.json`

usa actualmente:

`http://localhost:5292/`

Este es un cambio local de desarrollo y no debe mezclarse accidentalmente con commits funcionales.

## Siguiente bloque

T055-T060 — Pruebas US2 Activar / Desactivar.

Antes de implementar un nuevo bloque:
1. Revisar `tasks.md`.
2. Confirmar dependencias.
3. Agrupar tareas compatibles.
4. Ejecutar en modo Turbo SDD.
5. Validar build + tests + format + diff.
6. Revisar antes del commit.

## NavegaciÃ³n

- [[01-Arquitectura-Kryon]]
- [[02-Estado-Actual]]
- [[03-Entorno-Local]]
- [[04-Roadmap-Kryon]]
- [[features/001-Gestion-Usuarios]]
