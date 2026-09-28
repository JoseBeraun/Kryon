# Feature Specification: Interfaz de Gestión de Usuarios

**Feature Branch**: `001-gestion-usuarios`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Interfaz de Gestión de Usuarios: espacio donde el administrador del negocio visualiza y gestiona las cuentas de usuario de su empresa, manteniendo el aislamiento de información entre empresas. Kryon se reconstruye desde cero; el sistema antiguo es solo referencia de negocio y no se copian sus limitaciones (p. ej. el límite de cuatro usuarios)."

## Contexto

Kryon es un ERP SaaS multiempresa que se reconstruye desde cero. El Kryon antiguo sirve
únicamente como referencia de negocio. Esta especificación **no** hereda sus reglas,
limitaciones ni comportamientos de forma automática (en particular, **no** se hereda el límite
de cuatro usuarios por empresa; si existe un límite, su política se define fuera de esta
feature, ver DEP-5). Esta especificación contiene únicamente las reglas ya definidas para la
Gestión de Usuarios; las decisiones que pertenecen a otras specs o al negocio se registran en
[Dependencias y Decisiones Diferidas](#dependencias-y-decisiones-diferidas) en lugar de asumirse.

### Actores y roles

| Actor | Descripción |
|-------|-------------|
| **Administrador del negocio** | Usuario de una empresa con permiso para gestionar las cuentas de usuario **de su propia empresa**. Es el usuario principal de esta feature. |
| **Usuario sin permiso de gestión** | Cualquier usuario de la empresa que no tenga permiso para gestionar usuarios. No puede acceder a esta interfaz. |
| **Empresa actual** | La empresa asociada a la sesión verificada del administrador. Nunca se toma de un dato elegido o modificable por quien usa la interfaz. |

### Operaciones y roles autorizados

| Operación | Roles autorizados | ¿Crítica? (Principio V) |
|-----------|-------------------|--------------------------|
| Ver el listado de usuarios de la empresa | Administrador del negocio | No |
| Consultar el detalle de un usuario | Administrador del negocio | No |
| Registrar un nuevo usuario | Administrador del negocio | **Sí** |
| Editar un usuario existente | Administrador del negocio | **Sí** |
| Desactivar un usuario | Administrador del negocio | **Sí** |
| Activar (reactivar) un usuario | Administrador del negocio | **Sí** |
| Cualquier operación sobre usuarios de otra empresa | **Ningún rol** | — (siempre denegada) |

## Clarifications

### Session 2026-09-28

- Q: ¿Los formularios de registro y edición de usuario se especifican en esta feature o en una separada? (OQ-1) → A: Se incluyen en esta feature: formularios completos de registro y edición (datos, validaciones y asignación de rol).
- Q: ¿Puede un administrador desactivar su propia cuenta, y puede una empresa quedarse sin ningún administrador activo? (OQ-2) → A: No. Nadie puede desactivar su propia cuenta y no se puede desactivar al último administrador activo de la empresa.
- Q: ¿Puede un administrador editar, cambiar el rol, activar o desactivar a otro administrador de su misma empresa? (OQ-4) → A: Sí. Todos los administradores de la empresa son iguales y pueden gestionarse entre sí, sujetos a la regla del último administrador activo, que también aplica al quitar el rol de administrador.
- Q: ¿Debe la interfaz pedir confirmación antes de desactivar o activar a un usuario? (OQ-9) → A: Solo al desactivar, mostrando el nombre del usuario afectado; cancelar no produce cambios. Activar se aplica sin confirmación.
- Q: ¿El identificador de acceso debe ser único solo dentro de la empresa o en todo Kryon? (OQ-11) → A: Aún no se decide; diferida a la futura spec de autenticación (ver DEP-3).
- Q: ¿En qué estado debe quedar un usuario justo después de que el administrador lo registra? (OQ-12 / OQ-6) → A: Aún no se decide; diferida a la futura spec de autenticación (ver DEP-2).
- Q: ¿Puede el administrador cambiar el identificador de acceso de un usuario después de registrarlo? (OQ-10) → A: Aún no se decide; diferida a la futura spec de autenticación (ver DEP-4).
- Q: ¿Debe el administrador ver su propia cuenta en el listado de usuarios de su empresa? (OQ-3) → A: Sí. Aparece en el listado marcada como cuenta propia ("Tú"), sin la acción "Desactivar". Como la empresa siempre tiene al menos un usuario, el estado vacío no aparece en condiciones normales.
- Q: ¿Qué herramientas debe tener el listado para encontrar usuarios? (OQ-7) → A: Búsqueda por nombre o identificador de acceso, más filtros por rol y por estado, siempre limitados a la empresa actual. (La forma de navegar listados grandes se resolvió en una pregunta posterior de esta sesión.)
- Q: ¿Debe esta feature tener en cuenta un posible límite de usuarios por empresa al registrar nuevos usuarios? (OQ-8) → A: Sí. Puede existir un límite definido fuera de esta feature (p. ej. por plan); si existe y se alcanza, el registro se rechaza con un mensaje claro. La cifra y la política del límite son una decisión comercial externa (ver DEP-5).
- Q: ¿Puede un administrador cambiar su propio rol cuando la empresa tiene otros administradores activos? → A: No. Nadie puede cambiar su propio rol; en la edición de su propia cuenta el rol es de solo lectura. Otro administrador sí puede cambiárselo.
- Q: Si dos administradores editan al mismo usuario al mismo tiempo, ¿qué pasa cuando el segundo guarda? → A: Se rechaza el guardado del segundo; se le informa que el usuario fue modificado por otra persona, se le muestran los datos actuales y puede volver a aplicar sus cambios.
- Q: Además de nombre completo, identificador de acceso y rol, ¿el registro debe pedir algún otro dato en esta versión? (OQ-10) → A: No. Solo esos tres datos, los tres obligatorios.
- Q: Cuando una empresa tiene muchos usuarios, ¿cómo se presenta el listado? (OQ-7) → A: Por páginas, con controles de página anterior/siguiente e indicando el total de usuarios que coinciden con la búsqueda y los filtros. El tamaño de página no se define aquí.
- Q: Cuando se modifica un usuario, ¿la trazabilidad debe guardar qué campos cambiaron, con su valor anterior y el nuevo? → A: Sí. Cada modificación registra los campos cambiados con su valor anterior y el nuevo, además de los datos mínimos.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver los usuarios de mi empresa (Priority: P1)

Como administrador del negocio, quiero ver el listado de las cuentas de usuario de mi empresa,
con la información necesaria para identificarlas, su rol y su estado, para saber quién tiene
acceso a Kryon en mi negocio.

**Why this priority**: Es la base de toda la feature: sin el listado no se puede consultar,
editar ni activar/desactivar a nadie. Además es donde se materializa la garantía de
aislamiento multiempresa.

**Independent Test**: Con dos empresas (A y B) con usuarios propios, un administrador de A
abre la gestión de usuarios y verifica que ve exactamente los usuarios de A, con su nombre,
identificador de acceso, rol y estado, y ningún usuario de B.

**Acceptance Scenarios**:

1. **Dado** que la empresa A tiene 3 usuarios y la empresa B tiene 2 usuarios, **Cuando** el administrador de A abre la gestión de usuarios, **Entonces** ve exactamente los 3 usuarios de A y ninguno de B.
2. **Dado** que el administrador de A está en el listado, **Cuando** observa cada fila, **Entonces** cada usuario muestra como mínimo: nombre completo, identificador de acceso (p. ej. correo), rol y estado (Activo / Inactivo).
3. **Dado** que un usuario está inactivo, **Cuando** el administrador ve el listado, **Entonces** el estado "Inactivo" se comunica con texto (no solo con color o icono).
4. **Dado** un usuario de la empresa A sin permiso de gestión de usuarios, **Cuando** intenta acceder a la gestión de usuarios, **Entonces** se le deniega el acceso con un mensaje que indica que no tiene permiso, y no se le muestra ningún dato de usuarios.
5. **Dado** que el administrador es el único usuario de su empresa, **Cuando** abre la gestión de usuarios, **Entonces** ve una única fila con su propia cuenta, marcada como "Tú", sin la acción "Desactivar", y tiene disponible la acción "Nuevo usuario" (si tiene permiso para ello).
6. **Dado** que el listado no tiene ningún usuario que mostrar (situación que no debería ocurrir porque el administrador siempre aparece), **Cuando** se presenta el listado, **Entonces** se muestra un estado vacío que explica que no hay usuarios que mostrar, en lugar de una tabla vacía sin explicación.
7. **Dado** el listado de la empresa A, **Cuando** el administrador busca por una parte del nombre o del identificador de acceso, **Entonces** el listado muestra solo los usuarios de la empresa A que coinciden.
8. **Dado** el listado, **Cuando** el administrador filtra por un rol y/o por un estado, combinados o no con una búsqueda, **Entonces** el listado muestra solo los usuarios de su empresa que cumplen todos los criterios aplicados, y se indica qué criterios están activos.
9. **Dado** que la empresa B tiene un usuario cuyo nombre coincide con la búsqueda, **Cuando** el administrador de la empresa A busca ese nombre, **Entonces** ese usuario no aparece en los resultados ni se refleja en ningún contador.
10. **Dado** una búsqueda o un filtro sin coincidencias, **Cuando** se aplica, **Entonces** se muestra un mensaje de "sin resultados" distinto del estado vacío, con la opción de limpiar la búsqueda y los filtros.
11. **Dado** que los usuarios que coinciden con la búsqueda y los filtros no caben en una página, **Cuando** el administrador ve el listado, **Entonces** se indica el total de coincidencias y puede ir a la página siguiente y anterior, también solo con teclado, manteniendo la búsqueda y los filtros aplicados.

---

### User Story 2 - Activar o desactivar un usuario (Priority: P1)

Como administrador del negocio, quiero desactivar la cuenta de un usuario que ya no debe
acceder, y reactivarla cuando corresponda, para controlar quién opera en mi empresa sin perder
su historial.

**Why this priority**: Retirar el acceso de una persona es la acción de gestión con mayor
impacto en seguridad; debe estar disponible desde el primer entregable.

**Independent Test**: El administrador desactiva un usuario activo desde la interfaz y verifica que el estado cambia a Inactivo y que la operación queda registrada con actor,
momento, empresa y usuario afectado; luego lo reactiva y verifica lo mismo.

**Acceptance Scenarios**:

1. **Dado** un usuario activo de la empresa del administrador, **Cuando** el administrador elige "Desactivar", **Entonces** se le pide confirmación mostrando el nombre del usuario afectado, y no se aplica ningún cambio hasta que confirme.
2. **Dado** que el administrador confirmó la desactivación, **Cuando** la operación se completa, **Entonces** el usuario aparece como "Inactivo" en el listado y en su detalle, y se muestra un mensaje de éxito.
3. **Dado** que se pidió confirmación de desactivación, **Cuando** el administrador cancela, **Entonces** el estado del usuario no cambia y no se genera registro de trazabilidad.
4. **Dado** un usuario inactivo, **Cuando** el administrador elige "Activar", **Entonces** la activación se aplica sin paso de confirmación y, al completarse, el usuario aparece como "Activo" y se muestra un mensaje de éxito.
5. **Dado** un usuario activo, **Cuando** el administrador ve sus acciones, **Entonces** se ofrece "Desactivar" y no "Activar"; y a la inversa para un usuario inactivo.
6. **Dado** que se completó una activación o desactivación, **Cuando** se consulta el registro de trazabilidad, **Entonces** existe una entrada con: actor, fecha y hora, empresa, usuario afectado y tipo de operación (activación / desactivación).
7. **Dado** que la operación no pudo completarse (p. ej. el usuario cambió de estado entretanto o el servicio no respondió), **Cuando** falla, **Entonces** se muestra un mensaje que indica que la acción no se realizó, el motivo comprensible y qué puede hacer el administrador; el estado mostrado es el estado real del usuario.

---

### User Story 3 - Consultar el detalle de un usuario (Priority: P2)

Como administrador del negocio, quiero abrir la ficha de un usuario de mi empresa para
consultar su información completa sin modificarla.

**Why this priority**: Permite verificar datos antes de actuar (editar, desactivar), pero el
listado ya cubre la identificación básica.

**Independent Test**: Desde el listado, el administrador abre la ficha de un usuario y verifica
que ve su información en modo solo lectura y que puede volver al listado.

**Acceptance Scenarios**:

1. **Dado** un usuario de la empresa del administrador, **Cuando** el administrador elige "Ver detalle", **Entonces** ve en modo solo lectura: nombre completo, identificador de acceso, rol, estado y las acciones permitidas sobre ese usuario.
2. **Dado** que el administrador está en el detalle, **Cuando** elige volver, **Entonces** regresa al listado.
3. **Dado** un usuario perteneciente a la empresa B, **Cuando** el administrador de la empresa A intenta abrir su detalle por cualquier vía (p. ej. un enlace directo o un identificador), **Entonces** recibe la misma respuesta que para un usuario inexistente ("usuario no encontrado"), sin revelar que el usuario existe ni ningún dato suyo.

---

### User Story 4 - Registrar y editar usuarios (Priority: P2)

Como administrador del negocio, quiero registrar nuevos usuarios de mi empresa y editar los
existentes mediante formularios completos (datos, validaciones y asignación de rol), para
mantener actualizadas las cuentas de mi empresa.

**Why this priority**: Es necesaria para una gestión completa; el listado y la
activación/desactivación (P1) aportan valor aunque el registro y la edición lleguen después.

**Independent Test**: El administrador pulsa "Nuevo usuario", completa el formulario con datos
válidos y un rol, y verifica que el usuario aparece en el listado de su empresa; luego edita
ese usuario, cambia un dato y su rol, y verifica el cambio; en ambos casos intenta guardar con
datos inválidos y verifica que se rechaza con mensajes por campo.

**Acceptance Scenarios**:

1. **Dado** que el administrador tiene permiso para registrar usuarios, **Cuando** está en el listado (con o sin usuarios), **Entonces** tiene disponible la acción "Nuevo usuario" que lo lleva al formulario de registro.
2. **Dado** el formulario de registro, **Cuando** el administrador ingresa nombre completo, identificador de acceso y un rol válidos y guarda, **Entonces** se crea el usuario asociado a la empresa actual y aparece en el listado (su estado inicial lo define la futura spec de autenticación, ver [DEP-2](#dependencias-y-decisiones-diferidas)).
3. **Dado** el formulario de registro o de edición, **Cuando** el administrador guarda con un dato obligatorio vacío o con formato inválido, **Entonces** no se crea ni modifica nada y se muestra, junto a cada campo afectado, un mensaje que indica qué debe corregirse.
4. **Dado** el formulario de registro o de edición, **Cuando** el administrador elige el rol, **Entonces** solo puede elegir entre los roles disponibles para su empresa.
5. **Dado** un usuario de su empresa, **Cuando** el administrador elige "Editar", **Entonces** accede al formulario de edición con los datos actuales precargados y, al guardar cambios válidos, el listado y el detalle reflejan los datos modificados.
6. **Dado** que el administrador cancela el registro o la edición, **Cuando** vuelve al listado, **Entonces** no se ha creado ni modificado ningún usuario.
7. **Dado** que se completó un alta o una modificación, **Cuando** se consulta el registro de trazabilidad, **Entonces** existe una entrada con actor, fecha y hora, empresa, usuario afectado y tipo de operación; en una modificación, la entrada incluye además cada campo cambiado con su valor anterior y el nuevo.
8. **Dado** un usuario de la empresa B, **Cuando** el administrador de la empresa A intenta acceder a su edición o guardar cambios sobre él por cualquier vía, **Entonces** recibe "usuario no encontrado" y no se modifica nada.
9. **Dado** que el formulario recibe un identificador de empresa o de rol ajeno a la empresa actual (por cualquier vía), **Cuando** se guarda, **Entonces** el sistema lo rechaza sin crear ni modificar nada.
10. **Dado** que la empresa tiene dos administradores activos, **Cuando** uno de ellos edita al otro (datos o rol), **Entonces** el cambio se aplica igual que sobre cualquier otro usuario.
11. **Dado** que un usuario es el último administrador activo de la empresa, **Cuando** alguien intenta quitarle el rol de administrador mediante la edición, **Entonces** el sistema lo rechaza, no modifica nada y explica el motivo.
12. **Dado** que existe un límite de usuarios aplicable a la empresa y ya se alcanzó, **Cuando** el administrador intenta registrar un nuevo usuario, **Entonces** el sistema rechaza el registro sin crear nada y muestra un mensaje que indica que se alcanzó el límite de usuarios de la empresa.
13. **Dado** que el administrador edita su propia cuenta, **Cuando** abre el formulario de edición, **Entonces** el rol se muestra solo como lectura; y si intenta cambiar su propio rol por cualquier otra vía, el sistema lo rechaza sin modificar nada y explica el motivo.
14. **Dado** que dos administradores abrieron la edición del mismo usuario y el primero ya guardó cambios, **Cuando** el segundo guarda, **Entonces** el sistema rechaza su guardado sin modificar nada, le informa que el usuario fue modificado por otra persona y le muestra los datos actuales para que pueda volver a aplicar sus cambios.

---

### User Story 5 - Acciones visibles según permisos (Priority: P2)

Como administrador del negocio, quiero ver únicamente las acciones que tengo permitido
realizar, para no intentar operaciones que el sistema va a rechazar.

**Why this priority**: Mejora la claridad y reduce errores, pero la protección real la da la
validación de permisos en el sistema (Principio II), cubierta en P1.

**Independent Test**: Con un administrador al que se le retira un permiso concreto (p. ej.
desactivar), se verifica que la acción no aparece y que, si se intenta igualmente por otra vía,
el sistema la rechaza.

**Acceptance Scenarios**:

1. **Dado** que el administrador no tiene permiso para una acción (registrar, editar, activar o desactivar), **Cuando** ve el listado o el detalle, **Entonces** esa acción no se muestra.
2. **Dado** que el administrador intenta ejecutar una acción para la que no tiene permiso por una vía distinta a la interfaz, **Cuando** el sistema recibe la solicitud, **Entonces** la rechaza, no aplica ningún cambio y responde con un mensaje de falta de permiso.
3. **Dado** que al administrador se le retiró un permiso mientras tenía la interfaz abierta, **Cuando** intenta la acción que aún ve en pantalla, **Entonces** el sistema la rechaza con un mensaje de falta de permiso y la interfaz deja de ofrecer esa acción.

---

### User Story 6 - Uso accesible de las acciones principales (Priority: P2)

Como administrador del negocio que utiliza teclado o tecnologías de apoyo, quiero poder
realizar todas las acciones principales de la gestión de usuarios sin depender del ratón ni
del color.

**Why this priority**: Es exigido por los estándares de accesibilidad de la constitución y
aplica transversalmente a todas las historias anteriores.

**Independent Test**: Usando solo teclado, el administrador recorre el listado, abre un
detalle, inicia un registro, inicia una edición, desactiva y activa un usuario y vuelve
al listado.

**Acceptance Scenarios**:

1. **Dado** el listado de usuarios, **Cuando** el administrador navega solo con teclado, **Entonces** puede alcanzar y ejecutar: Nuevo usuario, Ver detalle, Editar, Activar y Desactivar.
2. **Dado** cualquier control interactivo de la interfaz, **Cuando** se inspecciona con una tecnología de apoyo, **Entonces** tiene un nombre accesible que describe su función y, en acciones por fila, identifica al usuario afectado (p. ej. "Desactivar a Ana Pérez").
3. **Dado** cualquier diálogo que la interfaz abra durante una acción principal, **Cuando** se abre, **Entonces** el foco del teclado se sitúa dentro del diálogo, puede completarse o cerrarse con teclado y, al cerrarse, el foco vuelve a un punto lógico del listado.
4. **Dado** un mensaje de éxito o de error, **Cuando** aparece, **Entonces** es anunciado a las tecnologías de apoyo y su significado no depende únicamente del color.

---

### Edge Cases

- **EC-1 – Acceso a un usuario de otra empresa**: si el administrador de la empresa A usa un enlace directo, un identificador o cualquier otra vía para ver, editar, activar o desactivar un usuario de la empresa B, el sistema responde exactamente igual que ante un usuario inexistente ("usuario no encontrado"), no aplica ningún cambio y no revela nombre, estado ni existencia del usuario.
- **EC-2 – Cambio de estado concurrente**: si el usuario ya fue desactivado (o activado) por otra persona entre que el administrador cargó la pantalla y ejecutó la acción, el sistema informa que el estado del usuario cambió, muestra el estado actual y no aplica una operación duplicada ni genera un registro de trazabilidad falso.
- **EC-3 – Edición concurrente**: si el usuario fue modificado por otra persona (datos, rol o estado) desde que el administrador abrió el formulario de edición, el guardado se rechaza sin aplicar cambios, se informa del conflicto, se muestran los datos actuales y no se genera un registro de trazabilidad por el intento rechazado como si fuera una modificación.
- **EC-4 – Usuario eliminado o inexistente**: si el usuario deja de existir mientras el administrador consulta su detalle o intenta una acción, se muestra "usuario no encontrado" y se ofrece volver al listado.
- **EC-5 – Autodesactivación o cambio del propio rol**: si el administrador intenta desactivar su propia cuenta o cambiar su propio rol por cualquier vía, el sistema lo rechaza, no aplica ningún cambio y explica el motivo; la interfaz no le ofrece "Desactivar" sobre sí mismo y muestra su rol como solo lectura.
- **EC-6 – Último administrador activo**: si desactivar a un usuario o quitarle el rol de administrador dejaría a la empresa sin ningún administrador activo, el sistema lo rechaza, no aplica ningún cambio y explica el motivo. La regla se cumple también cuando dos administradores intentan desactivarse mutuamente al mismo tiempo: al menos uno permanece activo.
- **EC-7 – Pérdida de permiso durante la sesión**: si el administrador pierde el permiso de gestión mientras usa la interfaz, la siguiente operación o consulta se deniega con un mensaje de falta de permiso y no se muestran datos nuevos.
- **EC-8 – Fallo de comunicación**: si una operación no obtiene respuesta, la interfaz indica que no se pudo confirmar el resultado, no muestra un estado que no esté confirmado y permite reintentar.
- **EC-9 – Datos extensos**: si un usuario tiene un nombre completo o un identificador de acceso muy largo, el listado lo muestra de forma legible sin romper la fila ni ocultar el rol o el estado.
- **EC-10 – Usuario sin rol asignado o con rol no reconocido** (p. ej. porque su rol dejó de existir en el catálogo de roles, gestionado fuera de esta feature): el listado lo muestra con el texto "Sin rol asignado" y la interfaz no le atribuye permisos por defecto.
- **EC-11 – Búsqueda sin resultados o con datos de otra empresa**: una búsqueda o filtro sin coincidencias muestra "sin resultados" con la opción de limpiar criterios; una búsqueda que solo coincidiría con usuarios de otra empresa se comporta exactamente igual que una búsqueda sin coincidencias.
- **EC-12 – Límite de usuarios alcanzado**: si existe un límite aplicable (definido fuera de esta feature, DEP-5) y se alcanzó, el registro se rechaza con un mensaje claro; ningún mensaje revela datos de otras empresas. Si no existe límite definido, el registro no se rechaza por cantidad.

## Requirements *(mandatory)*

### Functional Requirements

**Aislamiento multiempresa (Principio I)**

- **FR-001**: El sistema DEBE mostrar en la gestión de usuarios únicamente los usuarios que pertenecen a la empresa actual, determinada por la sesión verificada del administrador y nunca por un dato proporcionado desde la interfaz.
- **FR-002**: El sistema DEBE rechazar cualquier consulta, edición, activación o desactivación de un usuario que no pertenezca a la empresa actual, con independencia de la vía por la que se solicite.
- **FR-003**: Ante una solicitud sobre un usuario de otra empresa, el sistema DEBE responder de forma indistinguible a la de un usuario inexistente, sin revelar su existencia ni ninguno de sus datos.
- **FR-004**: Ningún mensaje, contador, estado vacío o resultado de la interfaz DEBE incluir ni permitir deducir información de usuarios de otra empresa.

**Control de acceso (Principios II y VI)**

- **FR-005**: El sistema DEBE permitir el acceso a la gestión de usuarios solo a usuarios con permiso de administración de usuarios en la empresa actual; el resto DEBE recibir un mensaje de acceso denegado sin ver datos.
- **FR-006**: El sistema DEBE validar el permiso y la empresa en cada operación en el momento de ejecutarla, independientemente de las acciones que muestre la interfaz.
- **FR-007**: La interfaz DEBE mostrar solo las acciones (registrar, ver detalle, editar, activar, desactivar) que el administrador tiene permitidas sobre cada usuario concreto.
- **FR-008**: Ante ausencia de permiso, configuración incompleta o estado ambiguo, el sistema DEBE denegar la operación.

**Listado y detalle**

- **FR-009**: El listado DEBE mostrar para cada usuario, como mínimo: nombre completo, identificador de acceso, rol y estado.
- **FR-010**: El estado DEBE mostrarse con texto explícito ("Activo" / "Inactivo"), pudiendo complementarse con color o icono pero no depender de ellos.
- **FR-011**: El administrador DEBE poder abrir el detalle de solo lectura de cualquier usuario de su empresa y volver al listado desde él.
- **FR-012**: El listado DEBE incluir la cuenta del propio administrador, identificada como cuenta propia ("Tú"), sin ofrecer sobre ella la acción "Desactivar".
- **FR-013**: Si el listado no tuviera ningún usuario que mostrar (situación que no debería ocurrir en condiciones normales, dado FR-012), la interfaz DEBE presentar un estado vacío con un texto explicativo y, si el administrador tiene permiso, la acción para registrar un nuevo usuario.

**Búsqueda y filtros**

- **FR-014**: El listado DEBE permitir buscar usuarios por nombre completo o identificador de acceso, incluida la coincidencia parcial.
- **FR-015**: El listado DEBE permitir filtrar por rol (entre los roles de la empresa actual) y por estado; los filtros y la búsqueda DEBEN poder combinarse, y el resultado DEBE cumplir todos los criterios aplicados.
- **FR-016**: La búsqueda y los filtros DEBEN operar exclusivamente sobre los usuarios de la empresa actual; sus resultados, contadores y mensajes NO DEBEN reflejar usuarios de otra empresa.
- **FR-017**: La interfaz DEBE indicar qué búsqueda y filtros están aplicados, permitir limpiarlos, y mostrar un mensaje de "sin resultados" distinto del estado vacío cuando no haya coincidencias.
- **FR-018**: El listado DEBE presentarse por páginas, con controles para ir a la página anterior y siguiente, e indicar el total de usuarios que coinciden con la búsqueda y los filtros aplicados; cambiar de página NO DEBE descartar la búsqueda ni los filtros. El tamaño de página no se define en esta especificación. El total y la paginación DEBEN contar solo usuarios de la empresa actual.
- **FR-019**: Los controles de búsqueda, filtros y paginación DEBEN tener nombre accesible, ser operables solo con teclado, y el cambio en la cantidad de resultados DEBE anunciarse a las tecnologías de apoyo.

**Registro y edición**

- **FR-020**: La interfaz DEBE ofrecer una acción "Nuevo usuario" que lleve al formulario de registro de un usuario en la empresa actual.
- **FR-021**: La interfaz DEBE ofrecer una acción "Editar" por usuario que lleve al formulario de edición de ese usuario con sus datos actuales precargados.
- **FR-022**: Al completar o cancelar un registro o una edición, el administrador DEBE regresar al listado, y este DEBE reflejar el resultado real de la operación.
- **FR-023**: Todo usuario registrado desde esta interfaz DEBE quedar asociado a la empresa actual, sin posibilidad de elegir otra empresa.
- **FR-024**: Los formularios de registro y edición DEBEN capturar exactamente tres datos, todos obligatorios: nombre completo, identificador de acceso y rol. En la edición, el nombre completo y el rol son editables (salvo el propio rol, ver FR-035); si el identificador de acceso es editable lo define la futura spec de autenticación ([DEP-4](#dependencias-y-decisiones-diferidas)).
- **FR-025**: El sistema DEBE validar los datos antes de crear o modificar un usuario; si algún dato obligatorio falta o es inválido, NO DEBE crear ni modificar nada y DEBE indicar, junto a cada campo afectado, qué debe corregirse.
- **FR-026**: La asignación de rol DEBE limitarse a los roles disponibles para la empresa actual; el sistema DEBE rechazar cualquier rol ajeno a ella. El catálogo de roles se gestiona fuera de esta feature.
- **FR-027**: El sistema DEBE rechazar un identificador de acceso duplicado según la regla de unicidad que defina la futura spec de autenticación ([DEP-3](#dependencias-y-decisiones-diferidas)). Cualquiera que sea esa regla, el mensaje de identificador duplicado NO DEBE revelar que el identificador existe en otra empresa (Principio I). La credencial inicial y el estado inicial del nuevo usuario los define esa misma spec ([DEP-2](#dependencias-y-decisiones-diferidas)).
- **FR-028**: Si existe un límite de usuarios aplicable a la empresa actual (definido fuera de esta feature) y se ha alcanzado, el sistema DEBE rechazar el registro de nuevos usuarios sin crear nada y DEBE mostrar un mensaje claro que indique que se alcanzó el límite. Esta feature NO define la cifra, a quién aplica ni cómo se cuenta (p. ej. si incluye usuarios inactivos o si afecta a las reactivaciones); eso es una decisión comercial externa ([DEP-5](#dependencias-y-decisiones-diferidas)).
- **FR-029**: Los formularios DEBEN cumplir los requisitos de accesibilidad: cada campo con etiqueta visible y nombre accesible, operables solo con teclado, y mensajes de error asociados al campo que no dependan únicamente del color.

**Activación y desactivación**

- **FR-030**: La interfaz DEBE ofrecer "Desactivar" para usuarios activos y "Activar" para usuarios inactivos, y nunca ambas a la vez para el mismo usuario.
- **FR-031**: Tras una activación o desactivación exitosa, el nuevo estado DEBE reflejarse en el listado y en el detalle, acompañado de un mensaje de éxito.
- **FR-032**: La desactivación DEBE requerir confirmación explícita que muestre el nombre del usuario afectado; cancelar NO DEBE producir cambios ni registro de trazabilidad. La activación NO requiere confirmación.
- **FR-033**: Desactivar un usuario NO DEBE eliminar su cuenta ni su información; la cuenta DEBE poder reactivarse.
- **FR-034**: El sistema DEBE impedir que cualquier usuario desactive su propia cuenta, explicando el motivo.
- **FR-035**: El sistema DEBE impedir que cualquier usuario cambie su propio rol, explicando el motivo; en la edición de la propia cuenta, el rol DEBE mostrarse como solo lectura. El cambio de rol de un administrador solo puede hacerlo otro administrador de la empresa.
- **FR-036**: El sistema DEBE impedir cualquier desactivación o cambio de rol que deje a la empresa sin al menos un administrador activo (usuario activo con permiso de administración de usuarios), incluso ante solicitudes simultáneas, explicando el motivo.
- **FR-037**: Todos los administradores de una empresa tienen el mismo nivel: un administrador PUEDE editar, cambiar el rol, activar y desactivar a otro administrador de su empresa, sujeto a FR-034 y FR-036. No existe jerarquía entre administradores.
- **FR-038**: Si el estado del usuario cambió desde que se cargó la pantalla, el sistema DEBE informar del cambio, mostrar el estado actual y no aplicar una operación duplicada.
- **FR-039**: Si el usuario fue modificado por otra persona desde que el administrador abrió su edición, el sistema DEBE rechazar el guardado sin aplicar cambios, informar que el usuario fue modificado por otra persona y mostrar los datos actuales para que el administrador pueda volver a aplicar sus cambios. Ningún cambio confirmado DEBE sobrescribirse sin aviso.

**Mensajes de error**

- **FR-040**: Cuando una operación no pueda realizarse, la interfaz DEBE mostrar un mensaje que indique qué acción no se realizó, el motivo en lenguaje comprensible y, cuando sea posible, qué puede hacer el administrador (reintentar, volver al listado, contactar a quien corresponda).
- **FR-041**: Los mensajes de error NO DEBEN mostrar detalles técnicos internos ni información de otras empresas.
- **FR-042**: La interfaz NO DEBE mostrar como confirmado un cambio que el sistema no ha confirmado.

**Trazabilidad (Principio V)**

- **FR-043**: El sistema DEBE registrar cada activación, desactivación, alta y modificación de usuario con: actor, fecha y hora, empresa, usuario afectado y tipo de operación.
- **FR-044**: En cada modificación de un usuario, el registro de trazabilidad DEBE incluir además los campos cambiados, cada uno con su valor anterior y su valor nuevo. Los campos no modificados NO se registran como cambiados.
- **FR-045**: Los registros de trazabilidad NO DEBEN contener credenciales ni secretos y NO DEBEN poder ser alterados ni borrados por quien originó la operación.
- **FR-046**: Los intentos denegados de operar sobre usuarios de otra empresa DEBEN poder registrarse para auditoría, sin exponer esa información al administrador que los intentó.

**Accesibilidad**

- **FR-047**: Todas las acciones principales (Nuevo usuario, Ver detalle, Editar, Activar, Desactivar, confirmar o cancelar la desactivación, completar o cerrar diálogos, volver) DEBEN poder ejecutarse únicamente con teclado.
- **FR-048**: Todo control interactivo DEBE tener un nombre accesible que describa su función; las acciones por fila DEBEN identificar al usuario afectado.
- **FR-049**: Todo diálogo que abra la interfaz (incluida la confirmación de desactivación) DEBE recibir el foco al abrirse y devolverlo a un punto lógico al cerrarse.
- **FR-050**: Los mensajes de éxito y error DEBEN ser anunciados a las tecnologías de apoyo y no depender únicamente del color.

### Key Entities *(include if feature involves data)*

- **Empresa**: Negocio cliente de Kryon. Delimita qué usuarios puede ver y gestionar un administrador. Todo usuario pertenece a una empresa.
- **Usuario**: Cuenta de una persona que opera en Kryon dentro de una empresa. Atributos relevantes para esta feature: nombre completo, identificador de acceso, rol, estado (Activo / Inactivo; cualquier estado adicional lo define la futura spec de autenticación, DEP-2), empresa a la que pertenece.
- **Rol**: Conjunto de permisos que determina qué puede hacer un usuario. En esta feature solo se muestra y se usa para decidir qué acciones están permitidas; su catálogo y definición quedan fuera de alcance.
- **Registro de trazabilidad**: Constancia de una operación crítica sobre un usuario: actor, momento, empresa, usuario afectado y tipo de operación; en las modificaciones, también los campos cambiados con su valor anterior y nuevo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En las pruebas de aislamiento con al menos dos empresas, el 100 % de los intentos de un administrador de la empresa A de ver, editar, activar o desactivar usuarios de la empresa B son rechazados, y 0 datos de la empresa B aparecen en listados, detalles o mensajes.
- **SC-002**: El 100 % de las activaciones, desactivaciones, altas y modificaciones completadas generan un registro de trazabilidad con los 5 datos exigidos (actor, momento, empresa, usuario afectado, tipo de operación), y el 100 % de las modificaciones registran cada campo cambiado con su valor anterior y nuevo.
- **SC-003**: El 100 % de las acciones principales se pueden completar usando solo teclado, y el 100 % de los controles interactivos tienen nombre accesible, verificado en revisión de accesibilidad.
- **SC-004**: El 100 % de las operaciones fallidas o denegadas presentan un mensaje que indica la acción no realizada y el motivo; 0 mensajes contienen detalles técnicos internos.
- **SC-005**: El 100 % de las acciones mostradas a un administrador corresponden a permisos que efectivamente tiene (0 acciones visibles que el sistema luego rechace por falta de permiso, salvo cambio de permisos durante la sesión).
- **SC-006**: En las pruebas de reglas de administración, el 100 % de los intentos de autodesactivación, de cambio del propio rol y de desactivar o quitar el rol al último administrador activo de una empresa son rechazados sin aplicar cambios, incluidos los intentos simultáneos.
- **SC-007**: El 100 % de las desactivaciones completadas fueron precedidas por una confirmación explícita, y el 100 % de las confirmaciones canceladas no producen cambios.
- **SC-008**: En las pruebas de búsqueda y filtros con al menos dos empresas, el 100 % de los resultados pertenecen a la empresa actual y cumplen todos los criterios aplicados.
- **SC-009**: En las pruebas de edición concurrente, el 100 % de los guardados sobre un usuario modificado por otra persona desde que se abrió su edición son rechazados, y 0 cambios confirmados se pierden sin aviso.

## Dependencias y Decisiones Diferidas

Decisiones que **no pertenecen a esta feature**: dependen de otras especificaciones o del
negocio. No son preguntas abiertas de la Gestión de Usuarios y **no bloquean** su plan: esta
spec ya define cómo se comporta la gestión de usuarios en cada caso, y solo delega el dato o la
regla que corresponde a otra spec. Cuando esas decisiones se tomen, esta spec se actualizará si
alguna de ellas cambia su comportamiento (Principio III).

| ID | Decisión diferida | Origen | Responsable | Qué define ya esta feature |
|----|-------------------|--------|-------------|----------------------------|
| **DEP-1** | Efecto de desactivar un usuario sobre sus sesiones abiertas. | OQ-5 | Futura spec de autenticación | La desactivación cambia el estado del usuario a Inactivo, requiere confirmación, se registra en la trazabilidad y no elimina la cuenta (FR-031 a FR-033, FR-043). |
| **DEP-2** | Estado inicial de un usuario recién registrado, forma de obtener su credencial inicial y existencia de estados adicionales a Activo / Inactivo. | OQ-6, OQ-12 | Futura spec de autenticación | El registro crea el usuario en la empresa actual con sus tres datos obligatorios y lo registra en la trazabilidad (FR-020 a FR-029, FR-043). Esta feature gestiona los estados Activo e Inactivo. |
| **DEP-3** | Ámbito de unicidad del identificador de acceso (por empresa o en todo Kryon). | OQ-11 | Futura spec de autenticación | Se rechaza el duplicado según la regla que se defina, con un mensaje que no revela datos de otras empresas (FR-027). |
| **DEP-4** | Si el identificador de acceso puede editarse después del registro. | OQ-10 (parte pendiente) | Futura spec de autenticación | En la edición son editables el nombre completo y el rol, salvo el propio rol (FR-024, FR-035). |
| **DEP-5** | Existencia, cifra y política del límite de usuarios por empresa (a quién aplica, cómo se cuenta, si afecta a reactivaciones). | OQ-8 (parte pendiente) | Decisión comercial externa | Si existe un límite y se alcanzó, el registro se rechaza con un mensaje claro (FR-028, EC-12). El límite de cuatro usuarios del Kryon antiguo **no** se hereda. |

### Registro de preguntas abiertas

Todas las preguntas abiertas de esta feature están resueltas o reubicadas; no queda ninguna
pregunta abierta propia de la Gestión de Usuarios.

| ID | Estado |
|----|--------|
| OQ-1, OQ-2, OQ-3, OQ-4, OQ-7, OQ-9 | Resueltas (ver Clarifications). |
| OQ-8, OQ-10 | Resueltas en lo que corresponde a esta feature; la parte pendiente pasa a DEP-5 y DEP-4. |
| OQ-5, OQ-6, OQ-11, OQ-12 | Reubicadas como dependencias DEP-1, DEP-2 y DEP-3. |

## Fuera de Alcance

- Autenticación e inicio de sesión.
- Recuperación o cambio de contraseña.
- Credencial inicial, estado inicial y efecto de la desactivación sobre sesiones abiertas (DEP-1, DEP-2).
- Reglas de unicidad y editabilidad del identificador de acceso (DEP-3, DEP-4).
- Cifra y política comercial del límite de usuarios (DEP-5).
- Gestión completa del catálogo de roles (crear, modificar o eliminar roles).
- Definición detallada de permisos individuales.
- Administración de usuarios de otras empresas, incluso por administradores de la plataforma.
- Configuración general de la empresa.
- Eliminación definitiva de usuarios (no solicitada; la desactivación no elimina).
- Implementación técnica (tecnologías, arquitectura, almacenamiento, interfaces de integración).

## Assumptions

- La sesión del administrador ya está autenticada y el sistema conoce de forma verificada su empresa y su rol (la autenticación se especifica aparte).
- Existe al menos un rol con permiso de administración de usuarios; el nombre y catálogo de roles se definen en otra feature.
- Un usuario pertenece a una sola empresa en el contexto de esta feature.
- La desactivación es reversible y preserva la información del usuario.
- El registro de trazabilidad de las operaciones críticas se consulta por medios de auditoría; su interfaz de consulta no forma parte de esta feature.
- El Kryon antiguo se usó solo como referencia de negocio; ninguna de sus reglas se asume vigente salvo que figure explícitamente en esta especificación.
