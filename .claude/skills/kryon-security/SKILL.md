---
name: kryon-security
description: "Reglas de seguridad por defecto de Kryon. Úsala siempre que el trabajo toque autorización, permisos o roles, validación de entrada, ProblemDetails y manejo de errores, secretos y configuración, logs o auditoría, SQL o acceso a datos, concurrencia, IdentidadPrueba u otra identidad de desarrollo/test, datos sensibles en el frontend, o cualquier revisión de seguridad. Complementa a kryon-sdd y kryon-multitenancy."
---

# Kryon: seguridad por defecto

Esta skill complementa a:
- `kryon-sdd`: precedencia de artefactos, una tarea a la vez, dependencias diferidas y reporte;
- `kryon-multitenancy`: todo lo relativo al aislamiento entre empresas.

No sustituye los artefactos de cada feature: los mecanismos concretos (políticas, contratos,
códigos de error, configuración) están en su `plan.md`, `research.md`, `data-model.md` y
`contracts/`. Cuando esta skill dice "según el diseño", se refiere a esos artefactos.

## 1. Principio fundamental: seguridad por defecto

Si una operación sensible no puede demostrar que está **autorizada**, **validada** y **limitada al
contexto correcto**, falla de forma cerrada (Constitución, Principio VI). Ante un error, una
configuración incompleta o un estado ambiguo, se deniega.

Nunca relajes la seguridad para hacer pasar una prueba, simplificar una implementación, resolver
una dependencia diferida ni facilitar el desarrollo local.

## 2. Autenticación y autorización

- **Autenticación**: quién es la identidad. **Autorización**: qué puede hacer esa identidad. Una identidad autenticada no está, por eso, autorizada.
- Si la feature deja la autenticación diferida, esta skill **no** la resuelve. Sin autorización explícita de los artefactos, no elijas proveedor de identidad, no inventes cookies ni tokens, no diseñes el inicio de sesión, no decidas expiraciones, no inventes recuperación de contraseña ni definas credenciales iniciales.

## 3. Autorización en el backend

- La autorización real se aplica en el servidor, antes de cualquier efecto o de devolver datos (Principio II).
- Ocultar un botón, deshabilitar un control, esconder una ruta o confiar en un rol enviado por el cliente **no** es control de acceso. El frontend solo mejora la experiencia.
- Los permisos y roles efectivos salen del contexto confiable que defina Kryon, nunca de la solicitud.
- Sin permiso explícito, se deniega. Si falta la asociación entre una operación y su permiso, también.
- No inventes nombres de permisos fuera de los artefactos. Si están marcados como **provisionales**, conserva ese carácter: léelos de configuración y no los trates como catálogo definitivo.
- Los casos denegados se prueban, no solo los permitidos.

## 4. Autorización y aislamiento multiempresa

Son **controles distintos**: tener permiso para una operación no autoriza a ejecutarla sobre otra
empresa (un permiso concedido en una empresa no se extiende a otra). La seguridad nunca permite
saltarse el aislamiento. Para cualquier riesgo entre empresas, la fuente especializada es
`kryon-multitenancy`.

## 5. Identidad de desarrollo y test

Si la feature usa un mecanismo de identidad de prueba (hoy, `IdentidadPrueba`):
- solo existe en los entornos que fijen los artefactos (hoy, Development y Test); nunca en Production;
- no es autenticación real ni debe convertirse en ella;
- no aflojes la restricción de entorno para facilitar pruebas;
- fuera de esos entornos, sus encabezados o mecanismos se ignoran y la solicitud no queda autenticada;
- cualquier configuración auxiliar de ese mecanismo (por ejemplo, CORS para pruebas) queda sujeta a la misma restricción de entorno.

Si falta una identidad válida donde es obligatoria, falla cerrado según el diseño.

## 6. Validación de entrada

- Toda entrada externa es no confiable. Valida en el servidor: formato, obligatoriedad, límites definidos por los artefactos, enumeraciones y estados permitidos, relaciones válidas e invariantes de dominio.
- La validación del frontend es una ayuda de uso; no sustituye la del backend.
- Los límites marcados como provisionales se leen de configuración; no los fijes como reglas permanentes.
- No inventes reglas de negocio con el nombre de "validación de seguridad". Si falta una regla, es una decisión pendiente (sección 21).

## 7. Mass assignment / overposting

- No mapees automáticamente todo el cuerpo de la solicitud a entidades persistidas si eso permite modificar campos que controla el servidor.
- Los campos de tenant, auditoría, concurrencia, estado interno, permisos e identificadores controlados por el servidor solo se modifican si el contrato y la tarea lo autorizan.
- Usa DTOs o comandos específicos según el diseño. Si el contrato de entrada es cerrado (sin propiedades adicionales), respétalo y rechaza lo que no esté en él.

## 8. Errores y ProblemDetails

- Los errores externos siguen **exactamente** los contratos de la feature. Si la feature define ProblemDetails, respeta su formato y sus códigos.
- Nunca expongas `exception.Message`, stack traces, cadenas de conexión, SQL, rutas locales, nombres internos sensibles, secretos ni datos de otra empresa.
- Los errores inesperados se convierten en la respuesta genérica y segura que defina el diseño, en todos los entornos. El detalle técnico va solo al log del servidor, sin secretos.
- Esta skill no inventa códigos HTTP ni códigos funcionales.

## 9. Logs y auditoría

- Logs y auditoría no son un lugar seguro para secretos. Nunca registres contraseñas, tokens, claves, cadenas de conexión, secretos, credenciales iniciales ni datos sensibles innecesarios.
- La auditoría registra **solo** lo que exigen los artefactos. Cuando se exigen valores anterior y nuevo, incluye solo los campos permitidos y nunca secretos.
- El aislamiento de la auditoría sigue `kryon-multitenancy`.
- No añadas información sensible a mensajes de error o logs "para depurar".
- Si el diseño establece la auditoría como inalterable (por ejemplo, de solo inserción), no la debilites.

## 10. Secretos y configuración

- Nunca hardcodees contraseñas, API keys, cadenas de conexión reales, client secrets, claves criptográficas ni tokens, ni en código, ni en archivos de configuración versionados, ni en pruebas.
- Usa el mecanismo de configuración que definan el proyecto y el entorno.
- Kryon apunta a Azure, pero esta skill **no** convierte Key Vault, Managed Identity ni otra infraestructura en requisito de una feature cuyo plan la deja fuera. No implementes infraestructura cloud desde una tarea funcional sin autorización explícita.

## 11. Base de datos

- Usa EF Core o consultas parametrizadas. **Nunca** construyas SQL concatenando entrada del usuario.
- SQL escrito a mano solo si la tarea lo requiere y los artefactos lo justifican; además, debe estar parametrizado, respetar el aislamiento (`kryon-multitenancy`) y respetar la concurrencia y la auditoría.
- No desactives controles de concurrencia ni de auditoría para simplificar una actualización (por ejemplo, con operaciones masivas que no pasan por ellos).

## 12. Concurrencia

Cuando los artefactos exigen detectar concurrencia, sus mecanismos forman parte de la integridad:
- no ignores conflictos ni conviertas un conflicto en éxito;
- no sobrescribas datos en silencio;
- no elimines el token o la versión de concurrencia.

El comportamiento ante un conflicto es el que fijen la spec y el contrato; no lo inventes.

## 13. Dependencias y paquetes

No añadas una dependencia porque "mejora la seguridad" si la tarea o el plan no la autorizan.
Una dependencia nueva exige necesidad real, encaje con el stack y autorización de los artefactos
SDD. No sustituyas componentes aprobados por alternativas arbitrarias.

## 14. CORS, CSRF, cookies, tokens y encabezados

- No inventes configuración de CORS, CSRF/antiforgery, cookies, JWT, OAuth/OIDC, CSP ni encabezados de seguridad si el modelo de hosting o de autenticación aún no la necesita o no la define.
- Cuando una tarea la requiera, sigue los artefactos de esa feature y aplica la configuración mínima y cerrada.
- No apliques a Kryon (Blazor WebAssembly independiente) recomendaciones pensadas para Blazor Server o SSR sin una decisión explícita.

## 15. Feature flags y gating

Un gate puede impedir entregar una función que depende de una decisión pendiente. Pero un gate
**no** crea una regla de negocio, **no** sustituye la autorización ni el aislamiento y **no**
resuelve dependencias diferidas.

Nunca dejes habilitada en producción, por accidente, una ruta o un mecanismo pensado solo para
Development o Test.

## 16. Respuestas y enumeración

- Evita respuestas que permitan descubrir recursos ajenos, identificadores válidos, usuarios de otra empresa, permisos internos o información sensible.
- Usa las respuestas neutrales que definan los contratos. No inventes otra si el contrato ya fija una.

## 17. Frontend

- Todo lo que se envía al cliente es observable por el usuario.
- Nunca pongas secretos en el código WebAssembly, en archivos de configuración públicos, en JavaScript, en LocalStorage o SessionStorage, en el HTML ni en variables accesibles desde el navegador.
- No guardes información sensible en el navegador salvo que una decisión explícita futura lo autorice y defina cómo protegerla.

## 18. Pruebas de seguridad

Cuando corresponda a la tarea, elige entre estas pruebas las que la tarea autoriza. No las generes todas automáticamente:
- acceso autorizado y acceso denegado, incluido el rol o permiso insuficiente;
- manipulación de identificadores;
- intento de modificar campos que controla el servidor;
- entrada inválida;
- errores sin filtración de detalles, incluido el error inesperado;
- mecanismo de identidad de prueba ausente en Production;
- casos entre empresas, según `kryon-multitenancy`;
- conflictos de concurrencia;
- ausencia de secretos en logs y auditoría.

## 19. Uso por el revisor de seguridad

Cuando la use `security-reviewer-agent`, el agente es **solo lectura**. Para cada hallazgo reporta:

```text
Severidad: CRITICAL | HIGH | MEDIUM | LOW
Archivo/línea:
Evidencia:
Principio/requisito afectado:
Escenario de fallo:
Corrección propuesta:
```

No edites código, no ejecutes correcciones, no pidas a otro agente que modifique archivos y no
reportes vulnerabilidades sin evidencia. Clasifica la severidad por el impacto técnico concreto,
no por dramatismo.

## 20. Checklist de revisión

Busca como mínimo, cuando aplique:
- operaciones sin autorización en el backend;
- roles, permisos o tenant tomados del cliente;
- IDOR/BOLA: acceso a un objeto por su id sin comprobar permiso y empresa;
- mass assignment;
- SQL concatenado;
- `IgnoreQueryFilters()` u otro bypass del aislamiento;
- `exception.Message` o stack traces expuestos;
- secretos en el código, en la configuración versionada o en los logs;
- identidad de prueba habilitada fuera de Development/Test;
- datos sensibles en el WebAssembly o en el navegador;
- actualizaciones sin la concurrencia exigida;
- feature gates usados como autorización;
- dependencias añadidas fuera de los artefactos SDD;
- endpoints o acciones que no definen los contratos;
- reglas de negocio inventadas "por seguridad".

## 21. Ante la duda

Si una medida de seguridad necesita una decisión que no existe, no la inventes. Detente y reporta:
- el riesgo;
- la decisión que falta;
- el artefacto donde debería definirse.

La seguridad no autoriza a completar decisiones funcionales pendientes.
