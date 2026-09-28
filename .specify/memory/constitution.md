# Kryon Constitution

Kryon es un ERP SaaS multiempresa orientado a la gestión de negocios. Esta constitución
define las reglas no negociables que gobiernan cómo se especifica, construye, prueba y
revisa el producto.

## Core Principles

### I. Aislamiento Multiempresa (NO NEGOCIABLE)

Toda información perteneciente a una empresa DEBE permanecer aislada de las demás empresas
del sistema. Ningún usuario PUEDE acceder a información de otra empresa salvo que exista
una autorización explícita definida por el negocio y registrada como tal.

Reglas:

- Toda entidad que contenga datos de negocio DEBE estar asociada a una empresa identificable.
- Toda consulta, escritura o exportación de datos DEBE estar delimitada por la empresa del
  contexto de la petición; nunca por un identificador provisto libremente por el cliente.
- El acceso entre empresas SOLO es válido cuando el negocio lo ha autorizado de forma
  explícita, y esa autorización DEBE ser verificable y auditable.
- Toda feature DEBE incluir al menos una prueba que demuestre que un usuario de la empresa A
  no obtiene datos de la empresa B.

Rationale: el aislamiento es la promesa central de un ERP SaaS multiempresa. Una única fuga
entre empresas es un incidente de confidencialidad, no un defecto menor.

### II. Control de Acceso por Rol

Toda funcionalidad DEBE validar los permisos del usuario según su rol y la empresa a la que
pertenece, antes de ejecutar cualquier efecto o devolver cualquier dato.

Reglas:

- La validación de permisos DEBE ocurrir en el lado servidor. Ocultar una acción en la
  interfaz NO cuenta como control de acceso.
- Cada especificación DEBE declarar qué roles pueden ejecutar cada operación que define.
- Un permiso concedido en una empresa NO se extiende a otra empresa.
- Los casos denegados DEBEN estar cubiertos por pruebas, no solo los casos permitidos.

Rationale: en un ERP, el rol determina qué puede ver y alterar una persona sobre la
operación real del negocio; el control no puede quedar implícito ni depender de la UI.

### III. Especificación Antes de Implementación

No se DEBE escribir código de una funcionalidad sin contar previamente con una especificación
clara y revisada.

Reglas:

- Cada feature DEBE tener una especificación revisada antes de que comience su implementación.
- La especificación DEBE indicar el alcance, los roles implicados, las reglas de negocio y los
  criterios de aceptación.
- Las ambigüedades detectadas DEBEN resolverse en la especificación, no durante la codificación.
- Un cambio de alcance durante la implementación DEBE reflejarse en la especificación.

Rationale: la especificación es el punto más económico para descubrir un malentendido; el
código es el más caro.

### IV. Criterios Verificables y Pruebas

Cada criterio de aceptación DEBE ser objetivo, verificable y estar cubierto por al menos una
prueba.

Reglas:

- Un criterio de aceptación DEBE poder evaluarse como cumplido o no cumplido sin juicio
  subjetivo.
- Formulaciones vagas ("rápido", "fácil de usar", "seguro") NO son aceptables como criterios;
  DEBEN expresarse como condiciones observables.
- Una feature NO se considera terminada mientras algún criterio de aceptación carezca de
  prueba que lo cubra.
- Las pruebas DEBEN cubrir tanto el comportamiento esperado como el rechazo de los casos
  no autorizados o inválidos.

Rationale: sin criterios verificables no existe forma honesta de declarar que algo funciona.

### V. Trazabilidad

Las operaciones críticas DEBEN permitir identificar quién realizó la acción, cuándo se
realizó y sobre qué empresa o entidad se ejecutó.

Reglas:

- Cada especificación DEBE declarar qué operaciones considera críticas.
- Un registro de operación crítica DEBE contener, como mínimo: actor, momento (marca temporal),
  empresa y entidad afectada, y el tipo de operación.
- Los registros de trazabilidad NO DEBEN incluir credenciales ni secretos.
- Los registros de operaciones críticas NO DEBEN poder ser alterados ni borrados por el
  usuario que las originó.

Rationale: un ERP gobierna dinero, inventario y obligaciones; ante una discrepancia el sistema
debe poder responder quién hizo qué, cuándo y sobre qué empresa.

### VI. Seguridad por Defecto

Las operaciones DEBEN partir del principio de mínimo privilegio y rechazar cualquier acceso
no autorizado.

Reglas:

- El comportamiento por defecto ante ausencia de permiso explícito DEBE ser la denegación.
- Un permiso DEBE concederse con el alcance mínimo necesario para la tarea.
- Ante un error, una configuración incompleta o un estado ambiguo, el sistema DEBE denegar el
  acceso en lugar de permitirlo.
- Los mensajes de error NO DEBEN revelar la existencia ni el contenido de datos de otra empresa.

Rationale: los sistemas que fallan hacia el acceso abierto acumulan fugas silenciosas; fallar
hacia la denegación convierte el error en un incidente visible y corregible.

### VII. Desarrollo Incremental y Revisable

Las features DEBEN dividirse en tareas pequeñas, verificables y comprensibles antes de su
implementación.

Reglas:

- Cada tarea DEBE tener un resultado observable y un criterio explícito de finalización.
- Una tarea cuyo alcance no pueda ser comprendido ni revisado en una sola lectura DEBE
  dividirse antes de empezarla.
- Las tareas DEBEN declarar sus dependencias, de modo que su orden de ejecución sea deducible.
- El avance se mide por tareas completadas y verificadas, no por trabajo iniciado.

Rationale: el trabajo pequeño se revisa de verdad; el trabajo grande se aprueba por cansancio.

### VIII. Uso Responsable de IA

Todo código y documentación generado por IA DEBE ser revisado, comprendido y validado antes
de considerarse terminado.

Reglas:

- La responsabilidad del resultado recae siempre en la persona que lo incorpora, nunca en la
  herramienta que lo generó.
- Código que la persona revisora no comprende NO DEBE integrarse; DEBE simplificarse,
  explicarse o descartarse.
- El contenido generado DEBE verificarse contra esta constitución y contra la especificación
  de la feature, no únicamente contra la ausencia de errores de ejecución.
- Las afirmaciones generadas sobre el comportamiento del sistema DEBEN confirmarse mediante
  pruebas u observación directa antes de darse por ciertas.

Rationale: la IA acelera la producción de texto plausible; solo la revisión humana convierte
ese texto en software del que el equipo puede responder.

## Restricciones de Seguridad y Datos

- Toda petición autenticada DEBE resolverse a un contexto que identifique usuario, empresa y rol.
- La empresa del contexto DEBE derivarse de la sesión o credencial verificada, nunca de un
  parámetro controlado por el cliente.
- Los datos de una empresa NO DEBEN aparecer en listados, agregados, informes, exportaciones,
  búsquedas, notificaciones ni mensajes de error de otra empresa.
- Los secretos y credenciales NO DEBEN almacenarse en el repositorio ni escribirse en registros.
- Las operaciones sobre datos de producción DEBEN preservar la información de trazabilidad
  exigida por el Principio V.
- La eliminación de datos DEBE preservar el rastro de la operación y su autoría.

## Estándares de Calidad y Accesibilidad

- Cada criterio de aceptación DEBE estar cubierto por al menos una prueba verificable.
- El código DEBE superar las validaciones de calidad definidas por el proyecto antes de integrarse.
- Las funcionalidades de interfaz DEBEN permitir ejecutar sus acciones principales mediante teclado.
- Todo control interactivo DEBE contar con una etiqueta o nombre accesible que describa su función.
- Los mensajes de error DEBEN indicar claramente qué acción o dato debe corregirse y NO DEBEN depender únicamente del color para comunicar su significado.
- Los requisitos de accesibilidad aplicables a cada feature DEBEN ser verificables durante su revisión.

## Flujo de Desarrollo y Puertas de Calidad

Orden de trabajo obligatorio para cada feature:

1. Especificación revisada (Principio III), con roles y criterios de aceptación declarados.
2. Plan de implementación derivado de la especificación.
3. Desglose en tareas pequeñas y verificables (Principio VII).
4. Implementación.
5. Verificación contra cada criterio de aceptación (Principio IV).
6. Revisión humana del resultado, incluido todo lo generado por IA (Principio VIII).

Puertas de calidad. Una feature NO puede declararse terminada si:

- Algún criterio de aceptación carece de prueba que lo cubra.
- Falta la prueba de aislamiento entre empresas exigida por el Principio I.
- Alguna operación que define no valida rol y empresa en el servidor (Principio II).
- Alguna operación crítica que define no queda registrada según el Principio V.
- Existe código integrado que la persona revisora no comprende (Principio VIII).

## Governance

Autoridad. Esta constitución prevalece sobre cualquier otra práctica, convención o preferencia
del proyecto. Ante un conflicto entre esta constitución y otro documento, prevalece esta
constitución hasta que sea enmendada.

Procedimiento de enmienda:

1. La enmienda propuesta DEBE presentarse por escrito, indicando el principio o sección
   afectada y el motivo del cambio.
2. La enmienda DEBE ser revisada y aprobada por la persona responsable del proyecto.
3. La enmienda aprobada DEBE registrarse en este archivo junto con su nueva versión y la
   fecha de última modificación.
4. Si la enmienda invalida trabajo ya especificado o implementado, DEBE indicarse cómo se
   adapta ese trabajo.

Política de versionado (semántica):

- MAJOR: se elimina o redefine un principio de forma incompatible con el anterior.
- MINOR: se añade un principio o sección, o se amplía materialmente una guía existente.
- PATCH: aclaraciones, redacción o correcciones sin cambio de significado.

Cumplimiento:

- Toda revisión de cambios DEBE verificar el cumplimiento de los principios aplicables.
- Cualquier desviación DEBE justificarse de forma explícita y por escrito en la
  especificación de la feature; una desviación no documentada bloquea la integración.
- La complejidad añadida DEBE justificarse; a igualdad de resultado se prefiere la solución
  más simple.
- Esta constitución se revisa cuando una enmienda lo requiera o cuando una desviación
  recurrente indique que una regla ya no refleja la práctica deseada.

**Version**: 1.0.0 | **Ratified**: 2026-09-27 | **Last Amended**: 2026-09-27
