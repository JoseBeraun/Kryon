# Quickstart: validar la Gestión de Usuarios

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Modelo**: [data-model.md](data-model.md) | **Contratos**: [contracts/](contracts/)

Guía para demostrar que la feature cumple sus criterios de éxito. No contiene código de
implementación: los proyectos y rutas son los definidos en [plan.md](plan.md#project-structure).

## Prerrequisitos

- .NET 10 SDK.
- Docker, para el SQL Server de Testcontainers y para ejecutar la API en local.
- Los navegadores de Playwright, instalados después de compilar con `pwsh tests/Kryon.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install`.
- Datos semilla de prueba (se cargan automáticamente en las pruebas de integración):
  - **Empresa A**: `ana` (administradora, rol con todas las capacidades de gestión de usuarios; nombres de permiso provisionales, research §R4), `beto` (administrador), `carla` (sin permisos de gestión) y `dario` (Inactivo).
  - **Empresa B**: `berta` (administradora) y un usuario llamado "Ana Beta", que coincide con la búsqueda "Ana" desde la empresa A.
  - Un rol de A sin la capacidad de desactivar, para las pruebas de acciones visibles.
  - Los puntos de integración de DEP-2, DEP-3, DEP-4 y DEP-5 se sustituyen por **dobles de prueba** configurables en cada prueba. Los dobles solo ejercitan la conexión; **no** representan las reglas reales, que definirán la futura spec de autenticación y la decisión comercial.

## Ejecutar

```powershell
dotnet build Kryon.sln
dotnet test tests/Kryon.Core.Tests
dotnet test tests/Kryon.Api.Tests          # requiere Docker
dotnet test tests/Kryon.Web.Tests
dotnet test tests/Kryon.E2E.Tests          # levanta API + Web en local
```

Para explorar a mano: `dotnet run --project src/Kryon.Api` y `dotnet run --project src/Kryon.Web`,
y entrar con el esquema de autenticación de desarrollo como `ana` (empresa A).

## Escenarios de validación

Cada escenario indica el criterio de éxito (SC) que demuestra y el resultado esperado.

| # | Escenario | Dónde | Resultado esperado | SC |
|---|-----------|-------|--------------------|----|
| V1 | Como `ana` (A): listar, buscar "Ana", pedir el detalle, editar, activar y desactivar el id de "Ana Beta" (B). | Api.Tests | El listado y la búsqueda nunca incluyen usuarios de B, y el `total` tampoco los cuenta. Toda operación sobre el id de B responde `404 usuario-no-encontrado`, igual que un id inventado. Cada intento deja un registro `UsuarioNoAccesible` bajo la empresa A con el id solicitado, igual que con el id inventado (sin empresa propietaria). | SC-001, SC-008 |
| V2 | Consultar directamente la tabla `Usuarios` con `SESSION_CONTEXT` de A, sin filtro de EF Core. | Api.Tests (SQL) | RLS devuelve solo filas de A. Una inserción con `EmpresaId` de B es bloqueada. | SC-001 |
| V3 | Registrar, editar (nombre y rol), desactivar y activar. | Api.Tests | Cada operación deja exactamente un registro con actor, momento, empresa, usuario y tipo. La edición guarda solo los campos cambiados, con su valor anterior y el nuevo. Intentar `UPDATE` o `DELETE` sobre `AuditoriaUsuarios` con el usuario de base de datos de la aplicación falla. | SC-002 |
| V4 | Recorrer el listado, el detalle, el formulario, la desactivación con confirmación, la activación y la paginación **solo con teclado**. Ejecutar axe en cada pantalla. | E2E.Tests | Todas las acciones se completan sin ratón y axe no informa violaciones serias ni críticas. El foco entra en el diálogo y vuelve al botón de origen. Los mensajes se anuncian. | SC-003 |
| V5 | Provocar cada código de error del contrato. | Api.Tests + Web.Tests | Cada respuesta es ProblemDetails con `codigo`, sin trazas ni nombres internos. La interfaz muestra el mensaje de [ui-usuarios.md](contracts/ui-usuarios.md#mensajes). | SC-004 |
| V6 | Como usuario con el rol sin la capacidad de desactivar, y como `carla`. | Web.Tests + Api.Tests | "Desactivar" no aparece en ninguna fila. `POST …/desactivacion` directo responde `403 sin-permiso`. `carla` recibe acceso denegado sin datos. | SC-005 |
| V7 | `ana` intenta desactivarse, cambiar su propio rol y dejar a la empresa sin administradores. Con `ana` y `beto` como únicos administradores, lanzar en paralelo las dos desactivaciones cruzadas. | Api.Tests | Los intentos sobre sí misma responden `409 no-puede-desactivarse-a-si-mismo` o `409 no-puede-cambiar-su-propio-rol`. En la prueba paralela, exactamente una desactivación tiene éxito y la otra responde `409 ultimo-administrador`, y queda al menos un administrador activo. Esto se repite N veces en la prueba de concurrencia. | SC-006 |
| V8 | Desactivar confirmando, y desactivar cancelando. | Web.Tests + E2E.Tests | Confirmar llama a la API y cambia el estado. Cancelar no hace ninguna llamada, no cambia nada y no deja registro. Activar no muestra diálogo. | SC-007 |
| V9 | Dos sesiones cargan el mismo usuario. La primera edita y guarda; luego la segunda guarda con el ETag anterior. Repetirlo con desactivación y activación. | Api.Tests + E2E.Tests | La segunda recibe `412 usuario-modificado` con `actual`, no se aplica ningún cambio y el formulario muestra los datos vigentes. | SC-009 |
| V10 | Registrar con dobles de DEP-2 y DEP-3 que devuelven un estado inicial y una respuesta de unicidad elegidos por la prueba. Editar con el doble de DEP-4 en ambos valores de `PermiteEdicion`. | Api.Tests + Web.Tests | El registro usa el estado que devuelve el punto de integración y responde `201`. Si el doble indica que el identificador está en uso, responde `409 identificador-en-uso` sin revelar la empresa. La interfaz muestra el identificador editable o de solo lectura según `acciones.editarIdentificador`. **Esta prueba solo valida la conexión**; las reglas reales se validarán cuando DEP-2 a DEP-4 estén definidas. | FR-027 / DEP |
| V11 | Con el doble de DEP-5 informando "existe un límite aplicable y se alcanzó", registrar un usuario. Con el doble informando "no existe límite aplicable", registrar otro. | Api.Tests | Límite alcanzado: `409 limite-usuarios-alcanzado`, sin crear nada. Sin límite aplicable: el registro no se rechaza por cantidad (EC-12). La cifra y la forma de contar no se prueban aquí porque son externas. | FR-028, EC-12 |

## Criterio de salida

La feature se considera verificada cuando:
- V1 a V9 pasan en CI, y V10 y V11 pasan con dobles de prueba.
- El registro y la edición del identificador solo se dan por terminados cuando DEP-2 a DEP-5 estén definidas y V10 y V11 se repitan con las implementaciones reales.
- La revisión manual con lector de pantalla del listado, el formulario y el diálogo está registrada en la revisión de la PR.
- Una persona revisó el código generado (Principio VIII).
