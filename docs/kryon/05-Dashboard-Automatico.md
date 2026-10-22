# Dashboard automático — Kryon

> [!warning]
> Archivo generado automáticamente.
> No editar manualmente.

**Última actualización:** 21/10/2026 19:36:34

## Feature

`001-gestion-usuarios`

## Progreso

| Métrica | Valor |
|---|---:|
| Total | 116 |
| Completadas | 60 |
| Pendientes | 56 |
| Avance | 51.7 % |

## Última tarea completada

**T060** [P] [US2] Pruebas bUnit de fallo de comunicaciÃ³n en `tests/Kryon.Web.Tests/Usuarios/FalloComunicacionTests.cs`. Con el mecanismo de T042, cuyo `HttpMessageHandler` falso lanza `HttpRequestException` al desactivar (tras confirmar) y al activar, comprobar: (1) se muestra el mensaje del contrato de interfaz "No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo."; (2) se anuncia en la regiÃ³n `role="alert"` del componente de mensajes; (3) **no** aparece ningÃºn mensaje de Ã©xito; (4) la fila conserva el estado anterior y no se muestra como modificada (EC-8, FR-040, FR-042).

## Siguiente tarea

**T061** [US2] AÃ±adir los mÃ©todos `Desactivar(actorId)` y `Activar()` a `src/Kryon.Core/Usuarios/Usuario.cs`, con las precondiciones de data-model.md (*Transiciones de estado*) que no dependen de la base de datos: no ser el actor, estar en el estado de origen correcto.

## Próximas tareas

- [ ] **T061** [US2] AÃ±adir los mÃ©todos `Desactivar(actorId)` y `Activar()` a `src/Kryon.Core/Usuarios/Usuario.cs`, con las precondiciones de data-model.md (*Tr...
- [ ] **T062** [P] [US2] Implementar `ReglaUltimoAdministrador` en `src/Kryon.Core/Usuarios/ReglaUltimoAdministrador.cs`: un administrador activo es un usuario Ac...
- [ ] **T063** [US2] Implementar `BloqueoAdministradoresEmpresa` (implementa `IBloqueoAdministradoresEmpresa` de T016) en `src/Kryon.Infrastructure/Usuarios/Bloqu...
- [ ] **T064** [US2] Implementar `RepositorioUsuarios` (implementa `IRepositorioUsuarios` de T016; obtener por id dentro de la empresa, contar administradores act...
- [ ] **T065** [US2] Implementar el caso de uso `CambiarEstadoUsuario` en `src/Kryon.Core/Usuarios/CambiarEstadoUsuario.cs`. Pasos: transacciÃ³n â†’ bloqueo por e...
- [ ] **T066** [US2] AÃ±adir `POST /api/usuarios/{id}/desactivacion` (polÃ­tica `Usuarios.Desactivar`) y `POST /api/usuarios/{id}/activacion` (polÃ­tica `Usuarios...

## Git

**Branch:** `001-gestion-usuarios`

**HEAD:** `ff61b77` — test(usuarios): definir contratos US2 T055-T060

### Cambios locales

- ` M .gitignore`
- ` M src/Kryon.Web/wwwroot/appsettings.Development.json`
- `?? docs/`
- `?? scripts/Update-KryonObsidian.ps1`

## Navegación

- [[00-Control-Center]]
- [[features/001-Gestion-Usuarios]]
- [[../../specs/001-gestion-usuarios/tasks|tasks.md]]
- [[../../specs/001-gestion-usuarios/spec|spec.md]]
- [[../../specs/001-gestion-usuarios/plan|plan.md]]

---

Generado por `scripts/Update-KryonObsidian.ps1`.
