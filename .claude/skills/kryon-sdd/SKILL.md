---
name: kryon-sdd
description: "Reglas de Spec-Driven Development de Kryon. Úsala siempre que trabajes en Kryon con una tarea de tasks.md (T###), al implementar, probar o revisar código de una feature, o al leer o interpretar specs, planes, contratos o tareas. Define qué artefactos leer, su precedencia, cómo ejecutar una sola tarea, qué hacer con tareas bloqueadas y dependencias diferidas, y cómo reportar al terminar."
---

# Kryon SDD

Kryon es un ERP SaaS multiempresa que se construye con Spec-Driven Development. Esta skill es el
contrato de trabajo para cualquier tarea: se aplica a todas las features, no solo a la actual.

**Principio fundamental: implementar lo especificado, no reinterpretar lo especificado.**

## 1. Fuentes de verdad y precedencia

### Localizar la feature activa

Resuelve la feature con la **primera** fuente de esta lista que dé un resultado inequívoco. Luego
aplica las validaciones y la comprobación cruzada. Ninguna fuente es obligatoria: el procedimiento
debe funcionar en una sesión local, un clon nuevo, CI, un worktree y en agentes con o sin Bash.

1. **Asignación explícita.** El prompt o la tarea indica `specs/NNN-nombre/`, el nombre inequívoco de una feature o la ruta de su `tasks.md`. Un ID `T###` por sí solo **no** identifica una feature (los IDs se repiten entre features).
2. **`SPECIFY_FEATURE_DIRECTORY`.** Es el override explícito de Spec Kit. Úsalo si su valor está disponible a través de tus herramientas o del contexto. No exige acceso a una shell solo para leerlo: si no puedes verlo, salta este paso.
3. **`.specify/feature.json`.** Si existe, lee `feature_directory`. Es estado **local** de Spec Kit (está en `.gitignore`, no existe en clones nuevos, CI ni worktrees): úsalo como pista, no como verdad absoluta.
4. **Rama Git actual.** Solo si tienes una herramienta de shell y puedes ejecutar el comando no mutante `git branch --show-current`. Si el resultado coincide inequívocamente con una carpeta `specs/<rama>/`, puedes usarla. Si no tienes shell, salta este paso: no es un error.
5. **Única feature disponible.** Si `specs/` contiene una sola carpeta de feature válida, úsala.
6. **Búsqueda por tarea.** Si solo recibiste un `T###` y hay varias features, busca ese ID en los `specs/*/tasks.md` disponibles. Usa la feature solo si hay **exactamente una** coincidencia. Si aparece en más de una o en ninguna, detente y pide la feature.

**Validaciones** (si alguna falla, detente y repórtalo):
- La carpeta resuelta existe y contiene `spec.md`.
- Si vas a ejecutar o revisar una tarea `T###`, la carpeta contiene `tasks.md` y el `T###` asignado existe en él.

**Comprobación cruzada.** Consulta también las demás fuentes que tengas disponibles (las de los pasos 2 a 4 que puedas leer) y verifica que sean coherentes con la feature resuelta. Si alguna apunta a otra feature (por ejemplo, el prompt dice `002` y `.specify/feature.json` dice `001`):
- no elijas en silencio;
- reporta la discrepancia indicando qué dice cada fuente;
- detente antes de modificar código.

La asignación explícita expresa mejor la intención que el estado local, pero una contradicción con otra fuente disponible se **reporta, no se oculta**.

Si ninguna fuente resuelve la feature de forma inequívoca, detente y pide que se indique.

Al comenzar una tarea o revisión, indica siempre:

```text
Feature activa: specs/NNN-nombre
Fuente usada: <asignación explícita | SPECIFY_FEATURE_DIRECTORY | .specify/feature.json | rama Git | única feature | búsqueda por tarea>
```

Después, lee la Constitución y solo los artefactos que necesita la tarea. No cargues toda la spec si no hace falta.

### Precedencia

```text
.specify/memory/constitution.md
        ↓
<feature>/spec.md
        ↓
<feature>/plan.md
        ↓
<feature>/research.md · <feature>/data-model.md · <feature>/contracts/
        ↓
<feature>/tasks.md
        ↓
código existente
```

- El código existente **nunca** prevalece sobre una spec aprobada. Si el código hace otra cosa, el código está mal o la spec debe cambiar por su propio proceso, no por tu decisión.
- `research.md`, `data-model.md` y `contracts/` están al mismo nivel: si se contradicen entre sí, es una contradicción, no una elección.
- Las skills oficiales (.NET u otras) son guías técnicas y están **por debajo** de todos los artefactos SDD.

### Contradicciones

Si dos fuentes se contradicen, o la tarea contradice un artefacto superior:

1. **Detente** antes de modificar nada.
2. Identifica la contradicción: los dos archivos, las secciones o líneas, y qué dice cada uno.
3. **No elijas** una versión de forma arbitraria ni "la más razonable".
4. Repórtala y espera instrucciones.

Lo mismo aplica si la tarea requiere una regla que ningún artefacto define: eso es una ambigüedad de especificación (Principio III) y no se resuelve en el código.

## 2. Una tarea a la vez

- Solo ejecutas el ID de tarea que te asignaron explícitamente. `T001` significa **solo** T001.
- Si no tienes un ID explícito, pídelo antes de empezar.
- No adelantes la tarea siguiente, no hagas "mejoras relacionadas", no refactorices fuera del alcance y no completes tareas vecinas "porque era fácil".
- Los archivos que puedes tocar son los que nombra la tarea. Si necesitas tocar otro, detente y explica por qué.
- No marques la tarea como completada en `tasks.md` salvo que se te pida.
- Al terminar, **detente**.

### Cómo leer una tarea

Formato: `- [ ] T### [P] [USn] Descripción con rutas`

- `[P]`: se puede hacer en paralelo con otras de su fase (archivos distintos). No te autoriza a hacer varias.
- `[USn]`: historia de usuario de la spec a la que pertenece.
- Las rutas entre comillas invertidas son los archivos autorizados.
- Los textos citados entre comillas en la tarea (restricciones de campos, mensajes, nombres) se aplican tal cual.

## 3. Tareas bloqueadas

- **Nunca** inicies una tarea marcada `⛔ BLOQUEADA (DEP-n)`, aunque te la asignen. Responde que está bloqueada y por qué dependencia.
- No memorices qué números están bloqueados: los IDs cambian al renumerar. Léelo cada vez en `tasks.md` (el marcador en la tarea y la sección "Tareas bloqueadas por dependencias externas").
- Si una tarea no bloqueada solo puede completarse usando el resultado de una bloqueada, detente y repórtalo.

## 4. Dependencias diferidas

Las dependencias externas de una feature están en la sección "Dependencias y Decisiones Diferidas" de su `spec.md` (por ejemplo, DEP-1 a DEP-5 en Gestión de Usuarios). Pertenecen a otras specs o a decisiones del negocio.

No se resuelven mediante:
- suposiciones ("seguramente el estado inicial será Activo");
- valores por defecto ("sin límite", "único global", "solo lectura");
- decisiones técnicas presentadas como neutrales;
- un comportamiento temporal que acabe funcionando como regla de negocio.

Si la tarea depende de una decisión externa pendiente: **detente y repórtalo**.

**El feature/release gating no es una regla de negocio.** Un gate cerrado significa "la función aún no está entregada", nunca "el negocio la prohíbe". Si una tarea define un gate, implementa el mecanismo técnico sin convertirlo en regla ni en texto de negocio visible.

Los valores marcados como **provisionales y configurables** (tamaños de página, longitudes máximas, nombres de permisos…) se leen de configuración: nunca como constantes ni como validaciones de negocio fijas.

## 5. Trazabilidad

### Antes de implementar

Identifica y anota:
- la historia de usuario (`USn`) y los escenarios de aceptación relacionados;
- los requisitos funcionales (`FR-###`), criterios de éxito (`SC-###`) y casos borde (`EC-n`) que cita la tarea o que cubre su contenido;
- los principios de la Constitución que aplican (aislamiento, autorización en servidor, seguridad por defecto, trazabilidad de operaciones críticas, accesibilidad);
- los archivos que la tarea autoriza a modificar.

### Después de implementar

Informa qué requisito cubre cada cambio, qué archivos cambiaste, qué pruebas ejecutaste y el **resultado real** de build y pruebas, incluidos los fallos. Nunca des por pasada una prueba que no ejecutaste.

## 6. Pruebas

- Las pruebas nacen de: spec + escenarios de aceptación + FR/SC/EC + tareas.
- **Nunca** infieras el comportamiento esperado solo del código existente.
- Prueba los casos permitidos **y** los denegados (Principio IV), incluido el aislamiento entre empresas cuando la tarea lo requiera.
- Usa el framework, el runner y las herramientas que fijan `plan.md` y `research.md`. No introduzcas otros.
- Si una prueba contradice la spec, la spec tiene prioridad: reporta el conflicto.
- Si una prueba falla por un defecto del código, no debilites la aserción, no la omitas y no cambies una regla para que pase. Repórtalo.

## 7. Cambios de alcance

Sin autorización explícita de la tarea o de los artefactos SDD, **no**:
- añadas dependencias NuGet (u otras) no previstas;
- cambies la arquitectura, la estructura de proyectos o el stack;
- introduzcas librerías de UI o de componentes;
- decidas nada sobre autenticación;
- cambies la base de datos, el esquema fuera de `data-model.md` o la estrategia de migraciones;
- cambies contratos (OpenAPI, contratos de interfaz, códigos de error, textos);
- cambies reglas multiempresa, de permisos o de trazabilidad;
- inventes KPIs, métricas o fórmulas de negocio.

Si crees que hace falta un cambio de alcance, detente y propónlo. No lo apliques.

## 8. Git

Los agentes **no** hacen `commit`, `push`, `merge`, `rebase`, ni `checkout`/`switch` de ramas, ni ningún otro comando que cambie el historial o la rama. La sesión principal controla Git.

## 9. Fin de tarea

Al terminar, devuelve este resumen breve y **detente**:

```text
Tarea: T### — <título corto>
Estado: completada | bloqueada | detenida por contradicción | fallida
Archivos modificados: <rutas>
Requisitos cubiertos: <USn-k, FR-###, SC-###, EC-n>
Build: <comando y resultado real>
Tests: <comando y resultado real: aprobadas / fallidas / omitidas>
Observaciones/bloqueos: <contradicciones, dependencias, riesgos o "ninguna">
```
