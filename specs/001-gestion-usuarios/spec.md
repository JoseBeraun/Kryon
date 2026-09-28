# Feature Specification: Interfaz de Gestión de Usuarios

**Feature Branch**: `001-gestion-usuarios`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Interfaz de Gestión de Usuarios: espacio donde el administrador del negocio visualiza y gestiona las cuentas de usuario de su empresa, manteniendo el aislamiento de información entre empresas. Kryon se reconstruye desde cero; el sistema antiguo es solo referencia de negocio y no se copian sus limitaciones (p. ej. el límite de cuatro usuarios)."

## Contexto

Kryon es un ERP SaaS multiempresa que se reconstruye desde cero. El Kryon antiguo sirve
únicamente como referencia de negocio. Esta especificación **no** hereda sus reglas,
limitaciones ni comportamientos de forma automática (en particular, **no** se hereda el límite
de cuatro usuarios por empresa; si el nuevo Kryon tendrá algún límite es una pregunta abierta,
ver OQ-8). Toda regla de negocio no definida explícitamente se
registra en [Preguntas Abiertas](#preguntas-abiertas) en lugar de asumirse.

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
| Acceder al registro de un nuevo usuario | Administrador del negocio | Sí (el alta resultante) |
| Acceder a la edición de un usuario | Administrador del negocio | Sí (la modificación resultante) |
| Desactivar un usuario | Administrador del negocio | **Sí** |
| Activar (reactivar) un usuario | Administrador del negocio | **Sí** |
| Cualquier operación sobre usuarios de otra empresa | **Ningún rol** | — (siempre denegada) |

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
5. **Dado** que la empresa del administrador no tiene usuarios que mostrar según la regla de [OQ-3](#preguntas-abiertas), **Cuando** abre la gestión de usuarios, **Entonces** ve un estado vacío que explica que no hay usuarios registrados y ofrece la acción para registrar uno nuevo (si tiene permiso para ello).

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

1. **Dado** un usuario activo de la empresa del administrador, **Cuando** el administrador elige "Desactivar" y la operación se completa, **Entonces** el usuario aparece como "Inactivo" en el listado y en su detalle, y se muestra un mensaje de éxito. (Si debe existir un paso de confirmación previo es una decisión pendiente, ver [OQ-9](#preguntas-abiertas).)
2. **Dado** un usuario inactivo, **Cuando** el administrador elige "Activar" y la operación se completa, **Entonces** el usuario aparece como "Activo" y se muestra un mensaje de éxito.
3. **Dado** un usuario activo, **Cuando** el administrador ve sus acciones, **Entonces** se ofrece "Desactivar" y no "Activar"; y a la inversa para un usuario inactivo.
4. **Dado** que se completó una activación o desactivación, **Cuando** se consulta el registro de trazabilidad, **Entonces** existe una entrada con: actor, fecha y hora, empresa, usuario afectado y tipo de operación (activación / desactivación).
5. **Dado** que la operación no pudo completarse (p. ej. el usuario cambió de estado entretanto o el servicio no respondió), **Cuando** falla, **Entonces** se muestra un mensaje que indica que la acción no se realizó, el motivo comprensible y qué puede hacer el administrador; el estado mostrado es el estado real del usuario.

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

### User Story 4 - Acceder al registro y a la edición de usuarios (Priority: P2)

Como administrador del negocio, quiero acceder desde la gestión de usuarios al registro de un
nuevo usuario y a la edición de uno existente, para mantener actualizadas las cuentas de mi
empresa.

**Why this priority**: Es necesaria para una gestión completa, pero el contenido detallado de
los formularios de alta y edición depende de [OQ-1](#preguntas-abiertas).

**Independent Test**: El administrador pulsa "Nuevo usuario" y llega al registro; pulsa
"Editar" sobre un usuario y llega a su edición con los datos precargados; al terminar o
cancelar, vuelve al listado, que refleja el resultado.

**Acceptance Scenarios**:

1. **Dado** que el administrador tiene permiso para registrar usuarios, **Cuando** está en el listado (con o sin usuarios), **Entonces** tiene disponible la acción "Nuevo usuario" que lo lleva al registro.
2. **Dado** un usuario de su empresa, **Cuando** el administrador elige "Editar", **Entonces** accede a la edición de ese usuario con sus datos actuales precargados.
3. **Dado** que el administrador completó un registro o edición con éxito, **Cuando** vuelve al listado, **Entonces** el listado refleja el nuevo usuario o los datos modificados.
4. **Dado** que el administrador cancela el registro o la edición, **Cuando** vuelve al listado, **Entonces** no se ha creado ni modificado ningún usuario.
5. **Dado** que se completó un alta o una modificación, **Cuando** se consulta el registro de trazabilidad, **Entonces** existe una entrada con actor, fecha y hora, empresa, usuario afectado y tipo de operación.
6. **Dado** un usuario de la empresa B, **Cuando** el administrador de la empresa A intenta acceder a su edición por cualquier vía, **Entonces** recibe "usuario no encontrado" y no se modifica nada.

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
- **EC-3 – Usuario eliminado o inexistente**: si el usuario deja de existir mientras el administrador consulta su detalle o intenta una acción, se muestra "usuario no encontrado" y se ofrece volver al listado.
- **EC-4 – Autodesactivación**: si el administrador intenta desactivar su propia cuenta, el comportamiento depende de [OQ-2](#preguntas-abiertas); mientras no se decida, el sistema **no** debe permitirlo (denegación por defecto, Principio VI) y debe explicar el motivo.
- **EC-5 – Último administrador activo**: si desactivar a un usuario dejaría a la empresa sin ningún administrador activo, el comportamiento depende de [OQ-2](#preguntas-abiertas); mientras no se decida, el sistema **no** debe permitirlo y debe explicar el motivo.
- **EC-6 – Pérdida de permiso durante la sesión**: si el administrador pierde el permiso de gestión mientras usa la interfaz, la siguiente operación o consulta se deniega con un mensaje de falta de permiso y no se muestran datos nuevos.
- **EC-7 – Fallo de comunicación**: si una operación no obtiene respuesta, la interfaz indica que no se pudo confirmar el resultado, no muestra un estado que no esté confirmado y permite reintentar.
- **EC-8 – Datos incompletos o extensos**: si un usuario tiene un dato opcional vacío o un nombre muy largo, el listado lo muestra de forma legible (p. ej. "—" para vacío) sin romper la fila ni ocultar el rol o el estado.
- **EC-9 – Usuario sin rol asignado o con rol no reconocido**: el listado lo muestra con el texto "Sin rol asignado" y la interfaz no le atribuye permisos por defecto.

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
- **FR-012**: Cuando no haya usuarios que mostrar, la interfaz DEBE presentar un estado vacío con un texto explicativo y, si el administrador tiene permiso, la acción para registrar un nuevo usuario.

**Registro y edición (acceso)**

- **FR-013**: La interfaz DEBE ofrecer una acción "Nuevo usuario" que lleve al registro de un usuario en la empresa actual.
- **FR-014**: La interfaz DEBE ofrecer una acción "Editar" por usuario que lleve a la edición de ese usuario con sus datos actuales.
- **FR-015**: Al completar o cancelar un registro o una edición, el administrador DEBE regresar al listado, y este DEBE reflejar el resultado real de la operación.
- **FR-016**: Todo usuario registrado desde esta interfaz DEBE quedar asociado a la empresa actual, sin posibilidad de elegir otra empresa.

**Activación y desactivación**

- **FR-017**: La interfaz DEBE ofrecer "Desactivar" para usuarios activos y "Activar" para usuarios inactivos, y nunca ambas a la vez para el mismo usuario.
- **FR-018**: Tras una activación o desactivación exitosa, el nuevo estado DEBE reflejarse en el listado y en el detalle, acompañado de un mensaje de éxito.
- **FR-019**: Desactivar un usuario NO DEBE eliminar su cuenta ni su información; la cuenta DEBE poder reactivarse.
- **FR-020**: Mientras [OQ-2](#preguntas-abiertas) no esté resuelta, el sistema DEBE impedir que un administrador desactive su propia cuenta y que se desactive al último administrador activo de la empresa, explicando el motivo.
- **FR-021**: Si el estado del usuario cambió desde que se cargó la pantalla, el sistema DEBE informar del cambio, mostrar el estado actual y no aplicar una operación duplicada.

**Mensajes de error**

- **FR-022**: Cuando una operación no pueda realizarse, la interfaz DEBE mostrar un mensaje que indique qué acción no se realizó, el motivo en lenguaje comprensible y, cuando sea posible, qué puede hacer el administrador (reintentar, volver al listado, contactar a quien corresponda).
- **FR-023**: Los mensajes de error NO DEBEN mostrar detalles técnicos internos ni información de otras empresas.
- **FR-024**: La interfaz NO DEBE mostrar como confirmado un cambio que el sistema no ha confirmado.

**Trazabilidad (Principio V)**

- **FR-025**: El sistema DEBE registrar cada activación, desactivación, alta y modificación de usuario con: actor, fecha y hora, empresa, usuario afectado y tipo de operación.
- **FR-026**: Los registros de trazabilidad NO DEBEN contener credenciales ni secretos y NO DEBEN poder ser alterados ni borrados por quien originó la operación.
- **FR-027**: Los intentos denegados de operar sobre usuarios de otra empresa DEBEN poder registrarse para auditoría, sin exponer esa información al administrador que los intentó.

**Accesibilidad**

- **FR-028**: Todas las acciones principales (Nuevo usuario, Ver detalle, Editar, Activar, Desactivar, completar o cerrar diálogos, volver) DEBEN poder ejecutarse únicamente con teclado.
- **FR-029**: Todo control interactivo DEBE tener un nombre accesible que describa su función; las acciones por fila DEBEN identificar al usuario afectado.
- **FR-030**: Todo diálogo que abra la interfaz (incluida una eventual confirmación, si se decide en OQ-9) DEBE recibir el foco al abrirse y devolverlo a un punto lógico al cerrarse.
- **FR-031**: Los mensajes de éxito y error DEBEN ser anunciados a las tecnologías de apoyo y no depender únicamente del color.

### Key Entities *(include if feature involves data)*

- **Empresa**: Negocio cliente de Kryon. Delimita qué usuarios puede ver y gestionar un administrador. Todo usuario pertenece a una empresa.
- **Usuario**: Cuenta de una persona que opera en Kryon dentro de una empresa. Atributos relevantes para esta feature: nombre completo, identificador de acceso, rol, estado (Activo / Inactivo), empresa a la que pertenece.
- **Rol**: Conjunto de permisos que determina qué puede hacer un usuario. En esta feature solo se muestra y se usa para decidir qué acciones están permitidas; su catálogo y definición quedan fuera de alcance.
- **Registro de trazabilidad**: Constancia de una operación crítica sobre un usuario: actor, momento, empresa, usuario afectado y tipo de operación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En las pruebas de aislamiento con al menos dos empresas, el 100 % de los intentos de un administrador de la empresa A de ver, editar, activar o desactivar usuarios de la empresa B son rechazados, y 0 datos de la empresa B aparecen en listados, detalles o mensajes.
- **SC-002**: El 100 % de las activaciones, desactivaciones, altas y modificaciones completadas generan un registro de trazabilidad con los 5 datos exigidos (actor, momento, empresa, usuario afectado, tipo de operación).
- **SC-003**: El 100 % de las acciones principales se pueden completar usando solo teclado, y el 100 % de los controles interactivos tienen nombre accesible, verificado en revisión de accesibilidad.
- **SC-004**: El 100 % de las operaciones fallidas o denegadas presentan un mensaje que indica la acción no realizada y el motivo; 0 mensajes contienen detalles técnicos internos.
- **SC-005**: El 100 % de las acciones mostradas a un administrador corresponden a permisos que efectivamente tiene (0 acciones visibles que el sistema luego rechace por falta de permiso, salvo cambio de permisos durante la sesión).

## Preguntas Abiertas

Decisiones de negocio pendientes, a resolver con `/speckit-clarify`. No se asumen reglas del
Kryon antiguo. Cuando una pregunta afecta a la seguridad o al acceso, rige provisionalmente el
comportamiento más restrictivo (Principio VI); en las demás, la spec no toma posición
("Sin definir").

| ID | Pregunta | Impacto | Comportamiento provisional |
|----|----------|---------|----------------------------|
| **OQ-1** | ¿Los formularios de registro y edición de usuario (campos, validaciones, asignación de rol, credencial inicial) forman parte de esta feature o de una especificación separada? | Alcance | Esta spec cubre solo el **acceso** al registro/edición y el retorno al listado. |
| **OQ-2** | ¿Puede un administrador desactivarse a sí mismo? ¿Puede quedar una empresa sin ningún administrador activo? | Seguridad / continuidad del negocio | Ambas acciones se **impiden** (FR-020). |
| **OQ-3** | ¿El administrador que consulta aparece en el listado? En consecuencia, ¿cuándo se muestra el estado vacío (sin ningún usuario, o sin usuarios distintos del propio administrador)? | Experiencia de usuario | El administrador aparece en el listado identificado como la cuenta propia; el estado vacío se muestra cuando no hay usuarios que mostrar. |
| **OQ-4** | ¿Puede un administrador editar, activar o desactivar a otro administrador de la misma empresa, o existe jerarquía entre administradores? | Permisos | Se permite solo lo que el permiso del administrador conceda explícitamente; sin permiso explícito, se deniega. |
| **OQ-5** | ¿Qué efecto inmediato tiene la desactivación sobre las sesiones abiertas del usuario desactivado? | Seguridad | Fuera de alcance de esta feature (depende de autenticación); debe resolverse en la spec correspondiente. |
| **OQ-6** | ¿Existen estados de usuario adicionales a Activo / Inactivo (p. ej. "Pendiente de activación", "Bloqueado")? | Alcance / modelo | Solo se consideran Activo e Inactivo. |
| **OQ-7** | ¿Se requiere búsqueda o filtros (por nombre, rol, estado) en el listado? ¿Cómo debe navegarse el listado cuando la empresa tiene muchos usuarios? | Experiencia de usuario | **Sin definir.** Esta spec no establece búsqueda, filtros ni forma de navegación del listado. |
| **OQ-8** | ¿Existe un límite de usuarios por empresa en el nuevo Kryon (por ejemplo, ligado al plan contratado)? | Negocio / comercial | **Sin definir.** Esta spec no establece un límite ni su ausencia. Lo único definido es que el límite de cuatro usuarios del Kryon antiguo **no** se hereda automáticamente. |
| **OQ-9** | ¿Debe pedirse confirmación antes de desactivar (o activar) un usuario? Si es así, ¿qué información debe mostrar? | Experiencia de usuario / seguridad | **Sin definir.** Esta spec no exige ni prohíbe un paso de confirmación. |

## Fuera de Alcance

- Autenticación e inicio de sesión.
- Recuperación o cambio de contraseña.
- Gestión completa del catálogo de roles (crear, modificar o eliminar roles).
- Definición detallada de permisos individuales.
- Administración de usuarios de otras empresas, incluso por administradores de la plataforma.
- Configuración general de la empresa.
- Eliminación definitiva de usuarios (no solicitada; la desactivación no elimina).
- Contenido detallado de los formularios de registro y edición, salvo decisión contraria en OQ-1.
- Implementación técnica (tecnologías, arquitectura, almacenamiento, interfaces de integración).

## Assumptions

- La sesión del administrador ya está autenticada y el sistema conoce de forma verificada su empresa y su rol (la autenticación se especifica aparte).
- Existe al menos un rol con permiso de administración de usuarios; el nombre y catálogo de roles se definen en otra feature.
- Un usuario pertenece a una sola empresa en el contexto de esta feature.
- La desactivación es reversible y preserva la información del usuario.
- El registro de trazabilidad de las operaciones críticas se consulta por medios de auditoría; su interfaz de consulta no forma parte de esta feature.
- El Kryon antiguo se usó solo como referencia de negocio; ninguna de sus reglas se asume vigente salvo que figure explícitamente en esta especificación.
