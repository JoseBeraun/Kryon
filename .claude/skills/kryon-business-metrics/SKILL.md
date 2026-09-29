---
name: kryon-business-metrics
description: "Reglas para trabajar con métricas y analítica de negocio en Kryon sin inventar definiciones, romper el aislamiento multiempresa ni sacar conclusiones no respaldadas. Úsala siempre que trabajes con métricas, KPIs, dashboards, reportes o informes, analytics, agregaciones, conteos, porcentajes, ratios, tendencias, estadísticas del negocio, consultas analíticas o tareas del Business Analytics Agent. Complementa a kryon-sdd, kryon-multitenancy y kryon-security."
---

# Kryon: métricas y analítica de negocio

Esta skill complementa a:
- `kryon-sdd`: fuentes de verdad, una tarea a la vez y reporte;
- `kryon-multitenancy`: aislamiento por empresa, también en agregados, informes y exportaciones (Constitución, Principio I);
- `kryon-security`: filtración de información y datos sensibles;
- `kryon-testing`: estrategia de pruebas cuando se implemente una métrica.

El objetivo es entregar una cifra **correcta según una definición aprobada**, no una cifra a
cualquier costo.

## 1. Principio fundamental

Una métrica solo existe en Kryon cuando su definición de negocio está respaldada por una fuente
aprobada.

> **Datos disponibles ≠ KPI aprobado.** Que se pueda escribir una consulta que calcule algo no
> significa que Kryon haya definido esa métrica.

Nunca inventes un nombre de KPI, fórmula, numerador, denominador, objetivo, umbral, semáforo,
benchmark, interpretación comercial, meta ni ranking si no los definen los artefactos o una
decisión explícita del negocio.

## 2. Fuentes de verdad

Sigue `kryon-sdd`. Busca la definición de una métrica en: la spec, el research, los contratos, la
documentación funcional aprobada, una decisión explícita del negocio o futuros artefactos de
analítica o reporting.

El código y el esquema de base de datos muestran **qué datos existen**, no el significado de
negocio de un KPI. Si la definición no existe, detente y reporta que falta la definición de
negocio.

## 3. Contrato de una métrica

Antes de implementar o validar un KPI, determina con los artefactos, cuando aplique: nombre;
propósito de negocio; fórmula; numerador y denominador; fuente de datos; alcance por empresa;
granularidad; periodo o ventana temporal; campo de fecha; zona horaria; estados incluidos y
excluidos; filtros; tratamiento de nulos y de denominador cero; unidad; moneda; regla de redondeo;
dimensiones permitidas; frecuencia o frescura esperada; reglas de acceso.

No todos los campos existen siempre. Pero si uno **cambia materialmente el resultado** y no está
definido, no lo supongas: reporta la ambigüedad.

## 4. Definiciones incompletas

Ante una métrica incompleta, **no elijas por tu cuenta** entre:

| Alternativas | |
|---|---|
| promedio o mediana | conteo total o elementos únicos |
| fecha de creación o fecha de operación | bruto o neto |
| solo activos o todos | día calendario o últimas 24 horas |
| hora local o UTC | acumulado o por periodo |
| porcentaje sobre el total global o sobre el total filtrado | cero o nulo |
| moneda base | precisión decimal |

Indica exactamente qué decisión falta.

## 5. Aislamiento multiempresa

Toda analítica sobre datos tenant-aware sigue `kryon-multitenancy`. Por defecto:
- cada empresa consulta solo sus propios datos;
- filtros, agrupaciones, conteos y denominadores quedan dentro de esa empresa;
- no se agregan varias empresas, no se comparan empresas entre sí ni se exponen totales globales.

Solo una spec futura explícita puede autorizar un contexto entre empresas. "Solo lectura" **no**
significa que se pueda saltar el aislamiento.

## 6. Seguridad y privacidad

Sigue `kryon-security`. Una métrica no puede ser una vía para revelar información que el usuario
no puede consultar directamente. Considera el riesgo de filtración por grupos con muy pocos
registros, identificadores, nombres, correos, detalles individuales, filtros demasiado
específicos o errores que confirman la existencia de datos.

No inventes reglas de anonimización ni umbrales mínimos de grupo. Si hacen falta y no están
definidos, reporta la decisión que falta.

## 7. Solo lectura

El Business Analytics Agent es de solo lectura.

| Puede | No puede |
|---|---|
| Leer artefactos y analizar esquemas. | Editar código ni modificar, crear o borrar datos. |
| Revisar consultas y proponerlas **en texto**. | Cambiar el esquema, crear migraciones o índices. |
| Explicar una fórmula aprobada y validar su consistencia. | Crear tablas de reporting ni desplegar dashboards. |
| Detectar ambigüedades. | Alterar fórmulas aprobadas. |
| Producir análisis sobre datos autorizados. | — |

Si una mejora requiere cambios, propónla en texto y detente.

## 8. Consultas

Toda consulta analítica respeta el aislamiento, usa solo fuentes autorizadas, aplica **exactamente**
los filtros definidos, es parametrizada (sin SQL concatenado), es coherente con la semántica del
KPI y no introduce exclusiones ocultas. Una consulta técnicamente correcta no sustituye una
definición de negocio ausente.

## 9. Dimensiones y segmentación

No segmentes una métrica por cualquier columna disponible. Usa solo dimensiones autorizadas o que
formen parte explícita de la pregunta o la tarea. Que una columna (fecha, sede, categoría, estado,
rol…) exista en el modelo no la convierte en dimensión válida para cualquier KPI. No inventes
comparaciones entre grupos.

## 10. Tiempo

Las métricas temporales necesitan una semántica explícita: instante, día, semana, mes, rango,
snapshot o acumulado. No supongas zona horaria, inicio de semana, cierre de mes, periodo fiscal,
ventana móvil ni qué fecha usar (creación o actualización). Si estos detalles afectan al resultado
y faltan, repórtalos.

## 11. Snapshot o estado actual

No reconstruyas la historia a partir del estado actual si el modelo no conserva la información
necesaria. Un registro activo hoy no demuestra que estuvo activo durante todo un periodo pasado.
Antes de ofrecer una métrica histórica, comprueba que las fuentes tienen los datos temporales
necesarios; si no, di que no puede calcularse correctamente con los datos disponibles. No
inventes historia.

## 12. Agregaciones

Antes de usar `COUNT`, `COUNT(DISTINCT …)`, `SUM`, `AVG`, mediana, `MIN`/`MAX`, porcentajes o
ratios, confirma que la operación corresponde a la definición aprobada. `COUNT(*)` y
`COUNT(DISTINCT …)` pueden representar conceptos de negocio muy distintos: no elijas uno por
conveniencia técnica.

## 13. Denominadores

Todo porcentaje o ratio necesita un denominador definido y explícito: total de la empresa, del
periodo, total filtrado, población elegible u otra base aprobada. No lo cambies en silencio al
aplicar filtros. Si puede ser cero, sigue la definición de negocio: no decidas por tu cuenta
entre 0 %, nulo, "N/A" o error.

## 14. Nulos y datos faltantes

Un nulo no equivale automáticamente a cero, falso, desconocido, "no aplica" ni vacío. Sigue la
semántica de los artefactos. No rellenes datos faltantes para obtener una cifra más cómoda.

## 15. Redondeo y precisión

No redondees en etapas intermedias salvo que la definición lo exija. Para presentar un resultado
redondeado, sigue la regla aprobada. No inventes la cantidad de decimales si puede cambiar una
decisión de negocio.

## 16. Moneda

Si una métrica involucra dinero: no sumes monedas distintas, no supongas una moneda, no inventes
ni apliques tipos de cambio no definidos y no trates como equivalentes valores nominales de
distintos periodos sin una regla aprobada. Si falta la moneda o la regla de conversión, reporta la
limitación.

## 17. Calidad de datos

Antes de atribuir significado a una cifra, considera: duplicados, nulos, estados inválidos,
registros incompletos, relaciones faltantes, fechas imposibles, datos fuera del alcance de la
empresa y cambios de definición. No corrijas ni elimines esos datos por tu cuenta: reporta cómo
afectan a la métrica.

## 18. Reconciliación

Cuando esté dentro de la tarea, valida una métrica aprobada con casos pequeños conocidos, conteos
independientes, totales de control, comparación con una fuente aprobada o datos de prueba.

Una discrepancia no se resuelve cambiando la fórmula en silencio. Primero clasifícala: error de
consulta, error de datos, definición ambigua, fuente distinta o periodo distinto.

## 19. Causalidad

No conviertas una correlación en causalidad. Evita conclusiones como "X causó Y" si los datos y el
diseño del análisis no lo permiten. Puedes describir asociaciones, diferencias observadas,
tendencias o variaciones cuando los datos las respalden. Separa siempre el **hecho observado** de
la **interpretación**.

## 20. Predicciones

No crees predicciones, scores, forecasts ni probabilidades solo porque existan datos históricos.
Necesitan una spec propia que defina objetivo, población, método, validación, uso permitido y
criterios de aceptación. Una tendencia descriptiva no se presenta como predicción.

## 21. Metas y semáforos

No inventes calificativos ("bueno", "malo", "saludable", "crítico"), colores de semáforo, metas ni
benchmarks sin criterios aprobados. El Business Analytics Agent informa; no decide por el
propietario del negocio qué resultado es aceptable.

## 22. Dashboards y presentación

Si una tarea futura define una visualización, muestra cuando corresponda: nombre de la métrica,
periodo, unidad, filtros, dimensión, fuente o definición accesible y el estado sin datos o con
error. No manipules escalas ni presentación para exagerar diferencias, ni elijas una visualización
que cambie el significado de los datos.

## 23. Rendimiento

Si una consulta analítica es lenta, el agente puede identificar filtros costosos, joins,
agregaciones y posibles necesidades de optimización. **No** puede crear índices, modificar el
esquema, desnormalizar, crear tablas agregadas, introducir cachés ni cambiar la estrategia de
persistencia: eso requiere una tarea y un diseño explícitos. Si hace falta optimizar EF Core,
puede recomendar evaluar `optimizing-ef-core-queries` (bajo demanda, subordinada al SDD).

## 24. Auditoría

Analizar la auditoría no elimina sus restricciones. No uses datos de auditoría de otra empresa,
que contengan secretos o para construir KPIs no aprobados. Una bitácora técnica no es
automáticamente una fuente analítica oficial.

## 25. Métricas de Gestión de Usuarios

La feature actual (Gestión de Usuarios) **no define KPIs** de negocio. No conviertas en KPI oficial
nada solo porque los datos puedan consultarse (usuarios activos, tasa de activación, usuarios por
rol, crecimiento de usuarios, administradores por empresa…).

Si alguien autorizado pide una cifra descriptiva concreta, puede calcularse como **consulta ad
hoc** siempre que: se explique exactamente qué se contó, no se la llame KPI oficial, respete el
aislamiento y no se le atribuya una meta ni una interpretación no definidas.

## 26. Consulta ad hoc o KPI

- **Consulta ad hoc**: una pregunta descriptiva puntual ("¿cuántos registros cumplen X?").
- **KPI**: una métrica formal con definición estable y propósito de negocio.

Una consulta ad hoc no crea un KPI. Si se vuelve recurrente, propone formalizar su definición antes
de tratarla como métrica oficial.

## 27. Cambios de definición

Si una métrica cambia de fórmula, no mezcles en silencio resultados calculados con definiciones
distintas. No diseñes ahora un sistema de versionado de métricas: si el riesgo aparece, repórtalo.

## 28. Pruebas de métricas

Cuando una tarea futura implemente una métrica aprobada, elige con `kryon-testing` las pruebas
relevantes para esa definición entre: caso conocido, cero registros, nulos, límites temporales,
filtros, denominador cero, empresa A frente a empresa B, redondeo, estados incluidos y excluidos,
duplicados y contrato de salida.

## 29. Procedimiento del Business Analytics Agent

1. Localiza la definición aprobada.
2. Indica qué estás calculando.
3. Indica la empresa y el alcance.
4. Identifica las fuentes.
5. Aplica exactamente los filtros y el periodo definidos.
6. Distingue hechos de interpretación.
7. Señala las limitaciones.
8. Detente si falta una decisión que cambie materialmente el resultado.

Nunca "completes" la definición con sentido común.

## 30. Formato del reporte analítico

Cuando corresponda, usa los campos que apliquen:

```text
Métrica/consulta:
Definición usada:
Alcance/tenant:
Periodo:
Fuentes:
Resultado:
Filtros:
Limitaciones:
Decisiones faltantes:
```

## 31. Ante la duda

Si dos interpretaciones dan números distintos y los artefactos no indican cuál es la correcta,
**no elijas una**. Muestra la ambigüedad e indica la decisión que falta.
