Spec de Ingeniería Inversa — Gestión de Usuarios



Proyecto: Kryon

Módulo: Configuración de Empresa y Aislamiento

Funcionalidad: Gestión de Usuarios

Tipo: Spec de ingeniería inversa — Día 2

Estado: En Revision



1\. PROBLEMA

Una empresa que utiliza Kryon necesita administrar qué personas pueden acceder al sistema y qué permisos poseen dentro de la organización.



Sin una gestión centralizada de usuarios, el administrador del negocio tendría dificultades para controlar quién puede ingresar al sistema, qué acciones puede realizar cada persona y a qué empresa pertenece cada usuario.



La funcionalidad de Gestión de Usuarios debe permitir administrar los usuarios pertenecientes a una empresa manteniendo el aislamiento de información entre empresas.





2\. USUARIOS INVOLUCRADOS

Administrador del negocio



Usuario responsable de administrar las cuentas de usuario pertenecientes a su empresa.



Debe poder visualizar y gestionar únicamente los usuarios correspondientes a la empresa en la que está operando.



Usuario de la empresa



Persona registrada dentro de una empresa que puede acceder a Kryon de acuerdo con el rol y los permisos que tenga asignados.





3\. HISTORIAS DE USUARIO

HU-01 — Visualizar usuarios

Como administrador del negocio,

quiero visualizar los usuarios registrados en mi empresa,

para conocer quiénes tienen acceso al sistema.



HU-02 — Registrar usuario

Como administrador del negocio,

quiero registrar un nuevo usuario en mi empresa,

para permitirle utilizar Kryon según el rol que se le asigne.



HU-03 — Consultar información de un usuario

Como administrador del negocio,

quiero consultar la información de un usuario,

para revisar sus datos y configuración dentro de la empresa.



HU-04 — Editar usuario

Como administrador del negocio,

quiero modificar los datos permitidos de un usuario,

para mantener su información actualizada.



HU-05 — Asignar rol

Como administrador del negocio,

quiero asignar un rol a un usuario,

para determinar las funciones del sistema a las que puede acceder.



HU-06 — Cambiar estado de usuario

Como administrador del negocio,

quiero poder cambiar el estado de un usuario,

para controlar si puede continuar utilizando el sistema.





4\. CRITERIOS DE ACEPTACIÓN

CA-01 — Listado de usuarios de la empresa



Dado que un administrador del negocio ha ingresado a Gestión de Usuarios,

cuando el sistema muestre el listado de usuarios,

entonces solo debe mostrar usuarios pertenecientes a la empresa actual.



CA-02 — Registrar un usuario



Dado que el administrador se encuentra en Gestión de Usuarios,

cuando registre un usuario proporcionando todos los datos obligatorios válidos,

entonces el usuario debe quedar registrado dentro de la empresa actual.



CA-03 — Validación de datos obligatorios



Dado que el administrador intenta registrar un usuario,

cuando uno o más datos obligatorios estén incompletos,

entonces el sistema debe impedir el registro e indicar qué información debe corregirse.



CA-04 — Consultar usuario



Dado que existe un usuario perteneciente a la empresa,

cuando el administrador seleccione dicho usuario,

entonces debe poder visualizar la información disponible de ese usuario.



CA-05 — Editar usuario



Dado que el administrador seleccionó un usuario existente,

cuando modifique información válida y confirme los cambios,

entonces el sistema debe guardar la nueva información del usuario.



CA-06 — Asignación de rol



Dado que el administrador está registrando o modificando un usuario,

cuando asigne un rol válido,

entonces el usuario debe quedar asociado a dicho rol dentro de la empresa.



CA-07 — Desactivar usuario



Dado que existe un usuario activo,

cuando el administrador lo desactive,

entonces el usuario debe quedar identificado como inactivo y dejar de disponer del acceso correspondiente.



CA-08 — Reactivar usuario



Dado que existe un usuario inactivo,

cuando el administrador lo reactive,

entonces el usuario debe volver a quedar activo de acuerdo con sus permisos vigentes.



CA-09 — Aislamiento entre empresas



Dado que existen usuarios registrados en diferentes empresas,

cuando un administrador consulte Gestión de Usuarios,

entonces no debe poder visualizar, consultar ni modificar usuarios pertenecientes a otra empresa.



CA-10 — Acciones sin autorización



Dado que un usuario no posee permisos para administrar usuarios,

cuando intente realizar una operación administrativa sobre ellos,

entonces el sistema debe rechazar la operación.





5\. CASOS BORDE



CB-01 — Usuario ya existente



¿Qué ocurre si el administrador intenta registrar un usuario utilizando información que identifica a una cuenta que ya existe?



Resultado esperado: el sistema no debe crear registros duplicados cuando exista una regla de unicidad aplicable.



Nota: este punto debe verificarse contra el Kryon existente para determinar qué campo identifica de manera única al usuario.



CB-02 — Usuario de otra empresa



¿Qué ocurre si se intenta consultar o modificar directamente un usuario perteneciente a otra empresa?



Resultado esperado: la operación debe ser rechazada.



CB-03 — Rol inexistente o no disponible



¿Qué ocurre si se intenta asignar a un usuario un rol que no existe o que no corresponde a la empresa actual?



Resultado esperado: el sistema debe rechazar la operación.



CB-04 — Usuario ya inactivo



¿Qué ocurre si se intenta desactivar nuevamente un usuario que ya está inactivo?



Resultado esperado: el sistema no debe generar un estado inconsistente.



CB-05 — Usuario inexistente



¿Qué ocurre si se intenta consultar, modificar o cambiar el estado de un usuario que ya no existe?



Resultado esperado: el sistema debe informar que el usuario solicitado no está disponible.



CB-06 — Datos inválidos



¿Qué ocurre si durante el registro o edición se proporciona información con un formato no permitido?



Resultado esperado: el sistema debe impedir guardar información inválida e indicar qué debe corregirse.





6\. FUERA DE ALCANCE



Para esta especificación inicial quedan fuera:



\- recuperación de contraseña;

\- autenticación del usuario;

\- configuración detallada de permisos individuales;

\- gestión completa de roles;

\- auditoría avanzada;

\- administración de usuarios pertenecientes a otras empresas;

\- configuración general de la empresa;

\- funcionalidades pertenecientes a otros módulos de Kryon.



Si al revisar el Kryon existente descubrimos que alguna de estas funciones forma parte realmente de Gestión de Usuarios, se corrige esta sección.





7\. PREGUNTAS PENDIENTES DE INGENIERÍA INVERSA



1\. ¿Qué campos tiene exactamente un usuario?

2\. ¿Cuál de esos campos es obligatorio?

3\. ¿Correo, documento u otro dato debe ser único?

4\. ¿Un usuario puede tener uno o varios roles?

5\. ¿Qué estados de usuario existen realmente?

6\. ¿El sistema elimina usuarios o solamente los desactiva?

7\. ¿Qué ocurre con un usuario que intenta acceder estando inactivo?

8\. ¿Un administrador puede modificar su propia cuenta o estado?

9\. ¿Existe búsqueda, filtrado o paginación en el listado?

10\. ¿Qué acciones aparecen realmente en la interfaz?





8\. RESULTADO DEL DÍA 2



1\. Usar Gestión de Usuarios como usuario.

2\. Comparar lo observado con esta spec.

3\. Corregir historias y criterios.

4\. Revisar recién después el código antiguo.

5\. Encontrar casos borde que no habíamos considerado.

6\. Actualizar la spec.

7\. Pasar el checklist de calidad.

8\. Enviar al mentor para revisión.



Nota: todavía no se usa /speckit-specify para esta práctica. Esta es la spec manual de ingeniería inversa correspondiente al Día 2.



