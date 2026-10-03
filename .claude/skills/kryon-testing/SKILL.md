---
name: kryon-testing
description: "Reglas de pruebas de Kryon: de dónde sale el comportamiento esperado, qué nivel de prueba elegir (unitaria, integración, componente, E2E, accesibilidad) y cuándo una prueba es válida. Úsala siempre que trabajes con tests o pruebas, xUnit, bUnit, Playwright, Testcontainers, pruebas de integración o E2E, cobertura, aserciones, concurrencia, RLS, análisis de huecos de pruebas o tareas de QA. Complementa a kryon-sdd, kryon-multitenancy y kryon-security."
---

# Kryon: pruebas

Esta skill decide **qué** probar y **de dónde** sale el comportamiento esperado. Complementa a:
- `kryon-sdd`: precedencia de artefactos, una tarea a la vez y formato de reporte;
- `kryon-multitenancy`: qué demostrar en pruebas de aislamiento entre empresas;
- `kryon-security`: qué demostrar en pruebas de autorización, validación, errores y secretos;
- las skills oficiales de pruebas (sección 23), para el **cómo** técnico.

## 1. Principio fundamental

El comportamiento esperado sale de:

```text
Constitución + spec + escenarios de aceptación + FR + SC + EC + contratos + tasks
```

**Nunca** del comportamiento accidental del código existente. Una prueba no convierte un bug actual
en comportamiento esperado.

La Constitución (Principio IV) exige que cada criterio de aceptación tenga al menos una prueba y
que se prueben tanto el comportamiento esperado como el rechazo de los casos no autorizados o
inválidos. Una feature no está terminada mientras falte alguna.

## 2. Pruebas antes de la implementación

Cuando `tasks.md` pide escribir pruebas primero:
1. escribe solo la prueba de la tarea asignada;
2. si es técnicamente posible, comprueba que falla **por la razón esperada** (no por un error de compilación o de configuración ajeno);
3. la implementación es otra tarea, normalmente de otro agente;
4. cuando esa tarea esté hecha, vuelve a ejecutar la prueba.

No avances a tareas vecinas para conseguir que pase. `qa-agent` no escribe código de producción.

## 3. Framework

El framework, el runner y las herramientas los fija el `plan.md` y el `research.md` **de cada
feature**. Para la feature actual (Gestión de Usuarios) son: .NET 10, xUnit v3, runner VSTest con
`dotnet test`, sin MSTest, sin xUnit v2 y sin configuración MTP ni `global.json` para pruebas.

No son reglas eternas de Kryon: si una feature futura decide otra cosa en sus artefactos, mandan
sus artefactos. No fijes versiones de paquetes desde esta skill.

## 4. Niveles de prueba

Elige el **nivel más pequeño** que pruebe correctamente el comportamiento.

| Nivel | Para qué | Cuidado con |
|---|---|---|
| **Unitaria** | Reglas de dominio, validaciones, servicios aislables, transformaciones, lógica sin infraestructura real. | No simules media aplicación para llamar "unitaria" a una prueba. |
| **Integración** | Lo que depende de verdad de ASP.NET Core, DI, middleware y manejadores, EF Core, SQL Server, RLS, transacciones, concurrencia, auditoría, serialización o ProblemDetails. | Si la regla depende de SQL Server, no la pruebes con otro proveedor que cambie ese comportamiento. |
| **Componente** | Blazor: renderizado, estados, interacción, validación visual, habilitación, accesibilidad del componente, llamadas simuladas al cliente de API. Herramienta según el plan (hoy, bUnit). | No compruebes solo que "renderiza". |
| **E2E** | Flujos que necesitan de verdad navegador, frontend, API e infraestructura de prueba. Herramienta según el plan (hoy, Playwright). | No uses E2E si una prueba más pequeña cubre el requisito. |
| **Accesibilidad** | Requisitos de accesibilidad de la Constitución, la spec y el contrato de interfaz, en el nivel adecuado (componente o E2E; hoy, bUnit y Playwright con axe). | No te limites a comprobar que la pantalla se muestra. |

## 5. Testcontainers y base de datos real

Cuando la prueba necesita el comportamiento real de SQL Server y el plan autoriza Testcontainers:
- usa la infraestructura de prueba que define la feature;
- no la sustituyas por InMemory o SQLite si la diferencia puede ocultar errores (RLS, restricciones, concurrencia, bloqueos, intercalaciones);
- mantén las pruebas aisladas entre sí y no dependas de datos de ejecuciones anteriores.

Testcontainers no es un requisito universal: las pruebas que no necesitan base de datos no la usan.

## 6. Pruebas multiempresa

Usa `kryon-multitenancy` para decidir qué demostrar. Cuando la tarea es tenant-aware:
- evalúa si necesita al menos **dos empresas** para demostrar el aislamiento (casi siempre sí);
- no basta con probar el caso permitido;
- elige, según la tarea, entre: acceso propio, denegación cruzada, listados, conteos, joins, relaciones, auditoría y RLS.

## 7. Pruebas de seguridad

Usa `kryon-security`. No reduzcas una prueba de seguridad a comprobar un código HTTP si el
requisito exige además: ausencia de información sensible, un código funcional, el formato
ProblemDetails, aislamiento o auditoría.

## 8. Contratos

Si existe un contrato formal (OpenAPI, contrato de interfaz):
- prueba el comportamiento **contra el contrato**;
- verifica, cuando corresponda: estado HTTP, forma de la respuesta, campos obligatorios, códigos funcionales, ProblemDetails y propiedades que no deben exponerse;
- no inventes campos;
- nunca ajustes el contrato para que coincida con una implementación incorrecta durante una tarea de código.

## 9. Casos positivos, negativos y borde

Para cada regla relevante, considera el camino correcto, el camino denegado o inválido y los
casos borde, según los FR, SC y EC. No hace falta crear tres pruebas mecánicamente por requisito:
el objetivo es **demostrar la regla**, no aumentar el número de pruebas.

## 10. Concurrencia

Si la feature exige concurrencia optimista, la prueba demuestra el conflicto **de verdad**:
- no lo simules si el comportamiento depende de la base de datos;
- no elimines la versión ni hagas que ambas escrituras tengan éxito;
- no cambies el resultado esperado para que pase.

El comportamiento esperado es el que definen la spec y el contrato.

## 11. Auditoría

Cuando una regla exige auditoría, la prueba puede necesitar verificar, según el diseño: que existe
el evento esperado, la empresa correcta, el actor o contexto correcto, los campos requeridos, los
valores anterior y nuevo cuando corresponda, la ausencia de secretos y que se escribió en la misma
transacción. No exijas datos de auditoría que los artefactos no piden.

## 12. Determinismo

Evita depender sin necesidad de la hora actual, un orden no garantizado, esperas arbitrarias,
recursos externos, estado global, ejecuciones previas o datos aleatorios no controlados.

Si el determinismo exige cambiar código de producción (por ejemplo, introducir `TimeProvider` o
un wrapper), **no lo hagas por tu cuenta**: hace falta una tarea o decisión SDD que lo autorice.

## 13. Datos de prueba

Datos mínimos, que muestren claramente el escenario, aislados, sin relación con producción y sin
secretos reales. Con varias empresas, usa identificadores y nombres que hagan evidente a qué
empresa pertenece cada dato. No reutilices estado entre pruebas por accidente.

## 14. Dobles de prueba

Usa dobles solo en fronteras donde no destruyan el comportamiento que quieres demostrar. **No**
simules:
- EF Core o SQL Server si la regla depende de su comportamiento real;
- RLS si precisamente se está validando RLS;
- la serialización HTTP si se valida el contrato real;
- la concurrencia real si el requisito depende de la versión en base de datos.

Sí puedes usarlos para aislar una regla de dominio o una frontera externa cuando el plan lo
permita, incluidos los puntos de integración de dependencias diferidas. En ese caso, la prueba
**solo valida la conexión**, no una regla real que aún no está definida.

## 15. Aserciones

Las aserciones demuestran el requisito concreto. Evita:
- aserciones que siempre se cumplen;
- comprobar solo que "no lanzó excepción";
- aserciones demasiado generales o snapshots enormes sin intención clara;
- verificar detalles internos que no forman parte del contrato;
- debilitar una aserción para que pase código incorrecto.

Para revisarlas, `assertion-quality` (bajo demanda).

## 16. Pruebas frágiles

Evita acoplar pruebas sin necesidad a la estructura interna, nombres privados, un orden
irrelevante, markup incidental, esperas o una implementación concreta que el contrato no exige.
Pero no elimines una verificación importante llamándola "frágil": si el contrato fija un texto,
un orden o un atributo de accesibilidad, verificarlo es correcto.

## 17. Cobertura

Kryon mide la cobertura **por requisito**: la unidad de trazabilidad es el escenario, FR, SC o EC.
No inventes objetivos de porcentaje (80 %, 90 %, 100 %) si los artefactos no los fijan. Las
herramientas de cobertura ayudan a descubrir huecos, pero un porcentaje alto no demuestra que se
cumplan los requisitos.

## 18. Análisis de huecos

Si usas `test-gap-analysis`:
- úsalo como revisión;
- no dejes que invente comportamiento esperado;
- contrasta cada hueco con la spec, los FR, SC, EC y las tareas;
- crea pruebas adicionales solo si están dentro del alcance de la tarea asignada; si no, repórtalas.

## 19. Qué puede y qué no puede hacer `qa-agent`

| Puede | No puede |
|---|---|
| Crear o modificar los archivos de prueba que autoriza la tarea. | Modificar `src/` para que una prueba pase. |
| Ejecutar build y pruebas. | Cambiar reglas funcionales. |
| Analizar fallos y reportar huecos. | Cambiar spec, plan o contratos. |
| Verificar el comportamiento contra los contratos. | Completar tareas de implementación ni inventar comportamiento esperado. |

Si descubre que el código de producción necesita un cambio, lo reporta y se detiene.

## 20. Cuando una prueba falla

Antes de cambiar nada, clasifica la causa:

| Causa | Qué hacer |
|---|---|
| **A.** Implementación incorrecta | Reportar el defecto con evidencia (qa-agent no lo corrige). |
| **B.** Prueba incorrecta | Corregir la prueba solo si contradice los artefactos, citándolos. |
| **C.** Contradicción entre artefactos | Detenerse y reportar. |
| **D.** Infraestructura o entorno | Reportar qué falló (Docker, SDK, red…) sin tocar la prueba ni el código. |
| **E.** Dependencia externa pendiente | Detenerse y reportar. |

No cambies el resultado esperado automáticamente.

## 21. Build y resultados

Nunca afirmes que el build o las pruebas pasan, ni que la cobertura es correcta, si no lo
ejecutaste. Reporta el comando, el resultado, los números de aprobadas, fallidas y omitidas cuando
estén disponibles y los errores relevantes. Si no pudiste ejecutar algo, dilo explícitamente.

## 22. Una tarea a la vez

Sigue `kryon-sdd`. Una tarea de pruebas **no** autoriza a arreglar producción, escribir pruebas de
la siguiente historia, refactorizar el proyecto ni aumentar la cobertura fuera de su alcance.

## 23. Skills oficiales bajo demanda

Pueden ayudar, sin precargarlas, y siempre subordinadas a los artefactos SDD:

| Skill | Para |
|---|---|
| `assertion-quality` | Revisar la calidad de las aserciones. |
| `test-anti-patterns` | Auditar pruebas que no verifican nada, inestables o duplicadas. |
| `test-gap-analysis` | Buscar comportamientos que las pruebas no detectarían (sección 18). |
| `coverage-analysis` | Interpretar un informe de cobertura ya generado. |
| `scaffold-dotnet-test-project` | Crear o reparar proyectos de prueba cuando una tarea lo pida. |
| `platform-detection` | Confirmar el framework y el runner configurados. |
| `filter-syntax` | Sintaxis de filtros de `dotnet test`. |
| `detect-static-dependencies` | Localizar dependencias de hora, archivos o entorno que impiden el determinismo. |

Sus recomendaciones no se convierten en reglas de negocio ni sustituyen las decisiones del plan.

## 24. Resumen al terminar

Usa el formato de `kryon-sdd`, añadiendo cuando aplique:

```text
Tarea:
Nivel de prueba:
Requisitos cubiertos:
Archivos de test:
Comando ejecutado:
Resultado:
Fallos/bloqueos:
Producción modificada: No
```

## 25. Ante la duda

Si no está claro qué comportamiento hay que probar, **no** lo infieras del código. Búscalo en los
artefactos SDD. Si sigue sin estar definido, detente y reporta la decisión que falta.
