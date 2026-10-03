# UI Contract: Gestión de Usuarios (Blazor WebAssembly)

**Feature**: [spec.md](../spec.md) | **API**: [usuarios-api.openapi.yaml](usuarios-api.openapi.yaml)

Este documento define lo que la interfaz **garantiza** a quien la usa y a las pruebas (bUnit y
Playwright): las rutas, los elementos, los nombres accesibles, el comportamiento con teclado y
los mensajes. No fija el diseño visual.

El registro de usuarios y la edición del identificador dependen de decisiones externas (DEP-2 a
DEP-5). Este contrato fija su presentación, pero no el comportamiento que esas decisiones
determinen.

La interfaz **nunca** decide permisos. Solo muestra lo que indican `acciones` y `puedeRegistrar`
en las respuestas de la API (FR-007). Cualquier rechazo de la API se muestra como mensaje.

## Rutas

| Ruta | Pantalla | Historia |
|------|----------|----------|
| `/usuarios` | Listado | US1, US2, US5 |
| `/usuarios/nuevo` | Formulario de registro | US4 |
| `/usuarios/{id}` | Detalle | US3 |
| `/usuarios/{id}/editar` | Formulario de edición | US4 |

La búsqueda, los filtros y la página se reflejan en la query string de `/usuarios`, así que al
volver del detalle o del formulario se conservan (FR-018, FR-022).

## Listado (`/usuarios`)

| Elemento | Contrato |
|----------|----------|
| Título | `<h1>` "Usuarios". |
| Búsqueda | Un `<input type="search">` con la etiqueta visible "Buscar por nombre o identificador". |
| Filtros | Dos `<select>` con etiquetas "Rol" y "Estado". Cada uno tiene la opción "Todos". Los roles vienen de `GET /usuarios/roles-asignables`. |
| Criterios activos | Un texto que resume la búsqueda y los filtros aplicados, y un botón "Limpiar búsqueda y filtros" (FR-017). |
| Tabla | Un `<table>` con `<caption>` y las columnas Nombre, Identificador de acceso, Rol, Estado y Acciones. El estado se muestra como texto ("Activo" / "Inactivo"; FR-010). Un rol nulo se muestra como "Sin rol asignado" (EC-10). |
| Cuenta propia | La fila del actor muestra la etiqueta de texto "Tú" junto al nombre (FR-012). |
| Texto extenso | Un nombre completo o un identificador de acceso largo se ajusta en varias líneas dentro de su celda; no desplaza ni oculta las columnas Rol, Estado y Acciones, y no se trunca sin que el texto completo quede disponible para lectores de pantalla (EC-9). |
| Acciones por fila | Botones o enlaces que solo aparecen si su valor en `acciones` es verdadero. Sus nombres accesibles son: "Ver detalle de {nombre}", "Editar a {nombre}", "Activar a {nombre}" y "Desactivar a {nombre}" (FR-048). |
| Nuevo usuario | Un botón "Nuevo usuario", solo si `puedeRegistrar` (FR-020). |
| Paginación | Un `<nav aria-label="Paginación de usuarios">` con los botones "Página anterior" y "Página siguiente" (deshabilitados en los extremos) y el texto "Página X de Y · N usuarios" (FR-018). |
| Anuncio de resultados | Una región `aria-live="polite"` que anuncia "N usuarios encontrados" cuando cambian la búsqueda, los filtros o la página (FR-019). |
| Sin resultados | Si `total = 0` con criterios aplicados, muestra "No hay usuarios que coincidan con la búsqueda y los filtros" y el botón para limpiarlos (FR-017, EC-11). |
| Estado vacío | Si `total = 0` sin criterios, muestra "No hay usuarios que mostrar" y "Nuevo usuario" si `puedeRegistrar` (FR-013). |
| Sin permiso | Si la API responde `sin-permiso`, muestra el mensaje de acceso denegado y **no** muestra la tabla (FR-005). |

## Detalle (`/usuarios/{id}`)

Muestra en solo lectura el nombre completo, el identificador de acceso, el rol, el estado y la
marca "Tú" si corresponde. Debajo aparecen las mismas acciones que en el listado, según
`acciones`, y el enlace "Volver al listado". Si la API responde `usuario-no-encontrado`, muestra
"Usuario no encontrado" y el enlace "Volver al listado" (EC-1, EC-4).

## Formulario de registro y edición

| Elemento | Contrato |
|----------|----------|
| Campos | "Nombre completo" (texto), "Identificador de acceso" (texto) y "Rol" (`<select>`). Cada uno tiene un `<label>` visible y la marca de obligatorio en texto (FR-024, FR-029). |
| Identificador en edición | La interfaz lo muestra editable o de solo lectura según `acciones.editarIdentificador`. El valor de esa bandera depende de DEP-4 (futura spec de autenticación), que este contrato no decide. Ambas presentaciones se prueban para que la interfaz se adapte sin cambios cuando DEP-4 se resuelva. |
| Rol en la propia cuenta | Es solo lectura, con el texto de ayuda "No puedes cambiar tu propio rol" (FR-035). |
| Validación | Los errores de `errors` se muestran junto a cada campo, se asocian con `aria-describedby` y el campo recibe `aria-invalid="true"`. Arriba del formulario aparece un resumen con `role="alert"`, con enlaces a cada campo, y el foco se mueve a ese resumen (FR-025). |
| Botones | "Guardar" y "Cancelar". Cancelar vuelve al listado sin hacer cambios (FR-022). |
| Conflicto de versión | Ante `usuario-modificado`, muestra "Otra persona modificó este usuario. Revisa los datos actuales y vuelve a aplicar tus cambios.", recarga los datos actuales desde `actual` y no guarda nada (FR-039). |

## Diálogo de confirmación de desactivación

- Un `<dialog>` modal con el título "Desactivar a {nombre}" y el texto "{nombre} dejará de tener acceso a Kryon. Puedes volver a activarlo más adelante." (FR-032).
- Tiene dos botones: "Desactivar" y "Cancelar". Al abrirse, el foco va a "Cancelar" (la opción segura). Escape equivale a Cancelar. Al cerrarse, el foco vuelve al botón que abrió el diálogo (FR-049).
- Cancelar no llama a la API (no hay cambio ni registro de trazabilidad).
- Activar **no** abre ningún diálogo.

## Mensajes

| Código de la API / evento | Mensaje en la interfaz (acción + motivo + qué hacer) |
|---------------------------|--------------------------------------------------------|
| Éxito de desactivación | "{nombre} fue desactivado." |
| Éxito de activación | "{nombre} fue activado." |
| Éxito de registro / edición | "Usuario {nombre} guardado." |
| `no-puede-desactivarse-a-si-mismo` | "No se desactivó la cuenta: no puedes desactivar tu propia cuenta. Pide a otro administrador que lo haga." |
| `no-puede-cambiar-su-propio-rol` | "No se cambió el rol: no puedes cambiar tu propio rol. Pide a otro administrador que lo haga." |
| `ultimo-administrador` | "No se aplicó el cambio: la empresa debe tener al menos un administrador activo. Asigna el rol de administrador a otra persona primero." |
| `usuario-modificado` | "No se aplicó el cambio: otra persona modificó este usuario. Se muestran los datos actuales; revisa y vuelve a intentarlo." |
| `usuario-no-encontrado` | "Usuario no encontrado. Vuelve al listado." |
| `sin-permiso` | "No tienes permiso para realizar esta acción." (y la acción deja de mostrarse al recargar; US5-3) |
| `identificador-en-uso` | Se muestra junto al campo: "Este identificador de acceso no está disponible. Usa otro." (nunca menciona otra empresa; el ámbito de la unicidad lo define DEP-3). |
| `limite-usuarios-alcanzado` | "No se registró el usuario: la empresa alcanzó su límite de usuarios." (solo si existe un límite externo aplicable; DEP-5) |
| `rol-no-valido` | Se muestra junto al campo: "Selecciona un rol de la lista." |
| Sin respuesta o error de red | "No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo." (y no se muestra un estado no confirmado; FR-042, EC-8) |
| `error-interno` | "No se pudo completar la acción por un error interno. Vuelve a intentarlo." (no muestra ningún detalle técnico ni un estado no confirmado; FR-040, FR-041, FR-042) |

Presentación: los mensajes de éxito se anuncian con `aria-live="polite"` y los de error con
`role="alert"`. Siempre incluyen texto y, si llevan icono, este no es la única señal (FR-050).
