# Spec de Ingeniería Inversa — Gestión de Usuarios

**Proyecto:** Kryon
**Módulo:** Configuración de Empresa y Aislamiento
**Funcionalidad:** Gestión de Usuarios
**Tipo:** Spec de ingeniería inversa — Día 2
**Estado:** Validada

---

## 1. PROBLEMA

Una empresa que utiliza Kryon necesita administrar las personas que pueden acceder al sistema, identificando a qué empresa pertenecen, qué rol poseen, cuál es su estado y qué permisos tienen disponibles.

Sin una gestión centralizada de usuarios, el administrador del negocio tendría dificultades para controlar quién puede utilizar Kryon, mantener actualizados los datos de los usuarios y restringir el acceso de cuentas que ya no deben utilizar el sistema.

La Gestión de Usuarios debe permitir administrar únicamente los usuarios pertenecientes a la empresa actual, manteniendo el aislamiento de información entre empresas.

---

## 2. USUARIOS INVOLUCRADOS

### Administrador del negocio

Usuario encargado de administrar las cuentas pertenecientes a su empresa.

Dependiendo de sus permisos, puede visualizar usuarios, crear nuevas cuentas, editar usuarios existentes, cambiar su estado y acceder a la configuración de sus permisos.

No debe administrar usuarios pertenecientes a otras empresas.

### Usuario de la empresa

Persona registrada dentro de una empresa que puede acceder a Kryon de acuerdo con su estado, rol y permisos asignados.

Un usuario puede encontrarse asociado a una sucursal de la empresa.

---

## 3. HISTORIAS DE USUARIO

### HU-01 — Visualizar usuarios

Como administrador del negocio,
quiero visualizar los usuarios registrados en mi empresa,
para conocer quiénes poseen acceso a Kryon y cuál es su situación actual.

### HU-02 — Registrar usuario

Como administrador del negocio con permiso para crear usuarios,
quiero registrar un nuevo usuario en mi empresa,
para permitirle acceder a Kryon.

### HU-03 — Visualizar información de los usuarios

Como administrador del negocio,
quiero visualizar la información principal de cada usuario,
para conocer su nombre, usuario, correo, rol, sucursal, estado y último acceso.

### HU-04 — Editar usuario

Como administrador del negocio con permiso de edición,
quiero modificar los datos de un usuario perteneciente a mi empresa,
para mantener su información actualizada.

### HU-05 — Asignar rol

Como administrador del negocio con permiso de edición,
quiero asignar un rol disponible a un usuario,
para establecer el rol con el que utilizará Kryon.

### HU-06 — Asociar usuario a una sucursal

Como administrador del negocio,
quiero asociar un usuario a una sucursal activa de mi empresa,
para relacionar su operación con una ubicación determinada.

### HU-07 — Cambiar estado de usuario

Como administrador del negocio con permiso de edición,
quiero activar o desactivar un usuario,
para controlar si puede continuar accediendo al sistema.

### HU-08 — Administrar permisos

Como administrador del negocio con permiso de edición,
quiero acceder a la configuración de permisos de un usuario de mi empresa,
para controlar las funcionalidades a las que puede acceder.

---

## 4. CRITERIOS DE ACEPTACIÓN

### CA-01 — Listado limitado a la empresa actual

Dado que el administrador se encuentra en Gestión de Usuarios,
cuando visualiza el listado,
entonces únicamente se muestran usuarios pertenecientes a la empresa actual.

### CA-02 — Información mostrada en el listado

Dado que existen usuarios registrados en la empresa,
cuando el administrador visualiza el listado,
entonces puede consultar para cada usuario su nombre completo, nombre de usuario, correo electrónico, rol, sucursal, estado y último acceso.

### CA-03 — Registro de usuario

Dado que el administrador posee permiso para crear usuarios y la empresa no ha alcanzado su límite,
cuando completa los datos obligatorios con un nombre de usuario que no se encuentra registrado,
entonces el nuevo usuario queda registrado en la empresa actual con estado ACTIVO.

### CA-04 — Datos obligatorios durante el registro

Dado que el administrador intenta crear un usuario,
cuando no proporciona nombre completo, nombre de usuario, correo electrónico o contraseña,
entonces el formulario no debe completar el registro.

### CA-05 — Nombre de usuario no duplicado

Dado que ya existe una cuenta con un determinado nombre de usuario,
cuando se intenta registrar otra cuenta utilizando ese mismo nombre de usuario,
entonces el sistema rechaza el registro.

### CA-06 — Límite de usuarios por empresa

Dado que la empresa ya tiene cuatro usuarios registrados,
cuando el administrador intenta crear un usuario adicional,
entonces el sistema impide el registro.

### CA-07 — Roles disponibles en Gestión de Usuarios

Dado que el administrador está creando o editando un usuario desde la interfaz existente,
cuando selecciona el rol del usuario,
entonces puede elegir entre los roles ofrecidos por la pantalla: VENDEDOR, ALMACEN, CAJERO o ADMINISTRADOR.

### CA-08 — Asociación con sucursal

Dado que el administrador selecciona una sucursal para un usuario,
cuando confirma el registro o la edición,
entonces la sucursal debe existir, pertenecer a la empresa actual y encontrarse activa.

### CA-09 — Registro cuando existen sucursales activas

Dado que la empresa posee sucursales activas,
cuando el administrador utiliza el formulario de creación de usuario,
entonces la interfaz solicita seleccionar una sucursal.

### CA-10 — Usuario sin sucursal

Dado que un usuario no tiene una sucursal asociada,
cuando aparece en el listado de usuarios,
entonces el sistema lo identifica como "Sin sucursal".

### CA-11 — Edición de usuario

Dado que el administrador posee permiso de edición y selecciona un usuario perteneciente a su empresa,
cuando modifica sus datos y confirma la operación,
entonces el sistema actualiza la información del usuario.

### CA-12 — Cambio de estado

Dado que el administrador posee permiso de edición,
cuando cambia el estado de un usuario perteneciente a su empresa,
entonces un usuario ACTIVO pasa a INACTIVO y un usuario INACTIVO pasa a ACTIVO.

### CA-13 — Acceso de usuario inactivo

Dado que un usuario se encuentra en estado INACTIVO,
cuando intenta iniciar sesión en Kryon,
entonces el sistema impide el acceso e informa que la cuenta se encuentra inactiva.

### CA-14 — Aislamiento entre empresas

Dado que existen usuarios registrados en diferentes empresas,
cuando un administrador visualiza, edita, cambia el estado o administra permisos de un usuario,
entonces la operación solo puede realizarse sobre usuarios pertenecientes a la empresa actual.

### CA-15 — Control por permisos

Dado que un usuario no posee el permiso requerido para una operación de Gestión de Usuarios,
cuando intenta realizar dicha operación,
entonces el sistema le impide continuar.

### CA-16 — Listado sin usuarios

Dado que no existen usuarios registrados para mostrar,
cuando se carga el listado,
entonces la interfaz muestra el mensaje "No hay usuarios registrados".

### CA-17 — Rol y permisos individuales

Dado que un usuario posee un rol asignado,
cuando se consulta su configuración,
entonces sus permisos individuales se gestionan separadamente del rol.

---

## 5. CASOS BORDE

### CB-01 — Nombre de usuario ya existente

Dado que existe una cuenta con un determinado nombre de usuario,
cuando el administrador intenta registrar otra cuenta utilizando el mismo nombre,
entonces el sistema rechaza el registro.

La revisión del sistema existente confirmó que la comprobación del nombre de usuario no está limitada a la empresa actual.

### CB-02 — Empresa con cuatro usuarios

Dado que una empresa ya posee cuatro usuarios registrados,
cuando se intenta registrar un quinto usuario,
entonces el sistema impide la creación.

### CB-03 — Usuario perteneciente a otra empresa

Dado que existe un usuario perteneciente a otra empresa,
cuando se intenta editarlo, cambiar su estado o administrar sus permisos desde la empresa actual,
entonces la operación no debe afectar al usuario de la otra empresa.

### CB-04 — Sucursal perteneciente a otra empresa

Dado que se intenta asociar una sucursal a un usuario,
cuando dicha sucursal pertenece a otra empresa,
entonces el sistema rechaza la asociación.

### CB-05 — Sucursal inactiva

Dado que una sucursal se encuentra INACTIVA,
cuando se intenta asociarla a un usuario,
entonces el sistema rechaza la operación.

### CB-06 — Empresa sin sucursales activas

Dado que una empresa no posee sucursales activas,
cuando el administrador abre el formulario de creación de usuario,
entonces no dispone de una sucursal activa para seleccionar y la interfaz informa que primero debe crear una.

### CB-07 — Usuario sin sucursal

Dado que un usuario no posee una sucursal asociada,
cuando aparece en el listado,
entonces el sistema muestra que se encuentra "Sin sucursal".

### CB-08 — Usuario inactivo intenta iniciar sesión

Dado que un usuario se encuentra INACTIVO,
cuando intenta acceder a Kryon,
entonces el sistema rechaza el inicio de sesión.

### CB-09 — Usuario sin permisos suficientes

Dado que un usuario no posee el permiso requerido para administrar usuarios,
cuando intenta realizar una operación protegida,
entonces el sistema rechaza el acceso a dicha operación.

### CB-10 — Usuario inexistente o fuera de la empresa

Dado que se proporciona un identificador de usuario inexistente o perteneciente a otra empresa,
cuando se intenta realizar una operación sobre dicho usuario,
entonces el sistema no debe modificar usuarios ajenos a la empresa actual.

### CB-11 — Administrador modifica su propia cuenta

Dado que el usuario que administra Gestión de Usuarios también pertenece a la empresa actual,
cuando selecciona su propia cuenta para editarla,
entonces el sistema existente no aplica una restricción específica que impida la operación si posee los permisos correspondientes.

### CB-12 — Administrador cambia el estado de su propia cuenta

Dado que un administrador posee permiso para editar usuarios,
cuando ejecuta el cambio de estado sobre su propia cuenta,
entonces el sistema existente no aplica una restricción especial por tratarse de su propia cuenta.

Este comportamiento debe considerarse durante la revisión del mentor antes de decidir si se conserva en la nueva versión.

---

## 6. FUERA DE ALCANCE

Para esta especificación de Gestión de Usuarios quedan fuera:

- recuperación de contraseña;
- proceso general de autenticación;
- verificación de correo electrónico;
- gestión completa del catálogo de roles;
- definición interna de cada permiso individual;
- configuración general de la empresa;
- administración de sucursales;
- auditoría avanzada;
- administración de usuarios pertenecientes a otras empresas;
- funcionalidades pertenecientes a otros módulos de Kryon.

La Gestión de Usuarios permite acceder a la configuración de permisos de un usuario, pero el detalle completo de la matriz de permisos puede especificarse separadamente.

---

## 7. HALLAZGOS CONFIRMADOS DE INGENIERÍA INVERSA

### 7.1 Campos utilizados

En la Gestión de Usuarios existente se identificaron los siguientes datos:

- nombre completo;
- nombre de usuario;
- correo electrónico;
- contraseña durante el registro;
- rol;
- sucursal asociada;
- estado;
- último acceso.

El listado muestra:

- nombre completo y nombre de usuario;
- correo electrónico;
- rol;
- sucursal;
- estado;
- último acceso.

### 7.2 Campos obligatorios de creación

El formulario de creación requiere:

- nombre completo;
- nombre de usuario;
- correo electrónico;
- contraseña.

Cuando existen sucursales activas, la interfaz también requiere seleccionar una sucursal.

### 7.3 Unicidad del nombre de usuario

La funcionalidad comprueba si el nombre de usuario ya existe antes de crear una cuenta.

La comprobación existente se realiza independientemente de la empresa, por lo que dos empresas no pueden registrar mediante este flujo el mismo nombre de usuario.

No se encontró en este flujo una comprobación equivalente que exija que el correo electrónico sea único.

### 7.4 Roles

Cada usuario utiliza un único valor de rol.

La pantalla de Gestión de Usuarios ofrece actualmente:

- VENDEDOR;
- ALMACEN;
- CAJERO;
- ADMINISTRADOR.

El rol y los permisos individuales se manejan como conceptos separados.

### 7.5 Estados

Los estados utilizados por Gestión de Usuarios son:

- ACTIVO;
- INACTIVO.

El cambio de estado alterna entre ambos valores.

### 7.6 Eliminación

No se encontró una acción de eliminación definitiva de usuarios dentro de la Gestión de Usuarios revisada.

La forma existente de impedir que una cuenta continúe utilizándose es cambiar su estado a INACTIVO.

Aunque existe un permiso denominado `usuarios_eliminar`, no se encontró una acción visible de eliminación de usuarios en esta funcionalidad.

### 7.7 Usuario inactivo

Cuando una cuenta INACTIVA intenta iniciar sesión, Kryon impide el acceso e informa:

"La cuenta se encuentra inactiva."

### 7.8 Administración de la propia cuenta

No se encontró una restricción específica que excluya al usuario actual de las operaciones de edición o cambio de estado.

Las operaciones verifican principalmente que el usuario objetivo pertenezca a la empresa actual y que quien ejecuta la operación tenga el permiso correspondiente.

### 7.9 Búsqueda, filtros y paginación

No se encontraron controles de búsqueda, filtros ni paginación dentro del listado de Gestión de Usuarios revisado.

El sistema muestra directamente los usuarios correspondientes a la empresa actual.

### 7.10 Acciones disponibles

Las principales acciones visibles encontradas son:

- crear usuario;
- editar usuario;
- cambiar estado;
- configurar permisos.

Cuando el usuario puede visualizar el listado pero no posee permiso de edición, la interfaz muestra la información en modo de solo lectura.

### 7.11 Aislamiento entre empresas

La carga del listado está limitada a la empresa actual.

Las operaciones de edición, cambio de estado y configuración de permisos también comprueban que el usuario objetivo pertenezca a la empresa actual.

### 7.12 Sucursales

Un usuario puede encontrarse asociado a una sucursal.

Cuando se proporciona una sucursal durante el registro o edición, esta debe:

- existir;
- pertenecer a la empresa actual;
- encontrarse ACTIVA.

En la edición es posible seleccionar la opción "Sin sucursal".

### 7.13 Límite de usuarios

La versión revisada de Kryon establece un máximo de cuatro usuarios registrados por empresa.

Al alcanzar cuatro usuarios, el sistema impide registrar uno adicional.

### 7.14 Permisos individuales

La Gestión de Usuarios utiliza permisos individuales independientes del rol.

Entre los permisos relacionados directamente con esta funcionalidad se encontraron:

- `usuarios_ver`;
- `usuarios_crear`;
- `usuarios_editar`;
- `usuarios_eliminar`.

El permiso de edición se utiliza también para acceder a la configuración de permisos y para cambiar el estado de un usuario.

---

## 8. RESULTADO DEL DÍA 2

Durante la ingeniería inversa de Gestión de Usuarios se realizó lo siguiente:

1. Se elaboró una primera spec de la funcionalidad.
2. Se revisó posteriormente el código existente de Kryon.
3. Se identificaron los campos y acciones reales de Gestión de Usuarios.
4. Se confirmó el aislamiento de usuarios por empresa.
5. Se identificaron los roles ofrecidos por la interfaz.
6. Se confirmaron los estados ACTIVO e INACTIVO.
7. Se identificó la asociación de usuarios con sucursales.
8. Se confirmó el límite existente de cuatro usuarios por empresa.
9. Se confirmó la validación del nombre de usuario existente.
10. Se identificó la separación entre rol y permisos individuales.
11. Se agregaron casos borde descubiertos durante la revisión.
12. Se actualizó la spec de acuerdo con el comportamiento encontrado.

### Cierre formal del Día 2

- Se realizó la validación final de la spec contra el checklist.
- La spec fue revisada por el mentor.
- Se incorporaron las correcciones solicitadas durante la revisión.
- Se realizó la kata sin IA indicada por el plan.

Esta spec corresponde a la práctica manual de ingeniería inversa del Día 2 y no utiliza `/speckit-specify`.
