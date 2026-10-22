# Plantilla — Bloque Turbo SDD

## Uso

Esta plantilla se utiliza para implementar bloques pequeños y relacionados de tareas SpecKit+.

Antes de utilizarla:

1. Leer tasks.md.
2. Confirmar exactamente qué tareas pertenecen al bloque.
3. Revisar dependencias.
4. No asumir el contenido de tareas futuras.

---

# PROMPT

/speckit-implement

MODO TURBO SDD — KRYON

Feature activa:

`001-gestion-usuarios`

Bloque a ejecutar:

`{{TAREA_INICIAL}}-{{TAREA_FINAL}}`

## FUENTE DE VERDAD

Antes de modificar código, lee obligatoriamente:

- specs/001-gestion-usuarios/tasks.md
- specs/001-gestion-usuarios/spec.md
- specs/001-gestion-usuarios/plan.md
- specs/001-gestion-usuarios/research.md
- specs/001-gestion-usuarios/data-model.md
- contratos relacionados
- tests relacionados

La definición exacta de las tareas en tasks.md es la autoridad.

No inventes requisitos basándote únicamente en el número de tarea.

## CONTEXTO ARQUITECTÓNICO

Kryon es un ERP SaaS multiempresa reconstruido desde cero.

El código legado solo sirve como referencia.

Reglas:

- respetar separación de capas;
- Core no depende de Infrastructure, Web, API o Contracts;
- Web consume API;
- Web no accede directamente a DbContext;
- Web no recalcula permisos;
- EmpresaId no se acepta como criterio arbitrario del frontend;
- respetar IContextoSolicitud;
- respetar aislamiento multiempresa;
- respetar Global Query Filters;
- respetar SESSION_CONTEXT;
- respetar RLS;
- reutilizar reglas y componentes existentes;
- no duplicar lógica existente.

## LÍMITES DEL BLOQUE

Implementa únicamente:

`{{TAREA_INICIAL}}-{{TAREA_FINAL}}`

No adelantes:

`{{SIGUIENTE_TAREA}}`

Si una tarea futura parece necesaria, documenta la dependencia y detente.

No cambies comportamiento fuera del bloque salvo que sea estrictamente necesario para completar estas tareas.

## DEPENDENCIAS

No inventes adaptadores, servicios externos ni implementaciones ficticias para desbloquear dependencias pendientes.

Si una dependencia externa está explícitamente pendiente en spec/plan/tasks, respeta ese estado.

## TESTS

No modifiques tests únicamente para hacerlos pasar.

Después de implementar:

- ejecuta los tests relacionados;
- ejecuta regresión razonable;
- informa cantidad de tests pasados/fallidos;
- distingue tests bloqueados por infraestructura externa.

## VALIDACIÓN FINAL

Ejecuta:

- dotnet build
- tests relevantes
- dotnet format o validación equivalente
- git diff --check

Revisa también:

- aislamiento multiempresa;
- autorización;
- contratos;
- accesibilidad Web cuando corresponda;
- concurrencia cuando corresponda;
- que no se haya enviado EmpresaId indebidamente;
- que la siguiente tarea permanezca intacta.

## GIT

NO ejecutar:

- git add
- git commit
- git push

Yo revisaré primero el diff.

## CAMBIOS LOCALES PREEXISTENTES

Existe una modificación local de desarrollo que puede aparecer en:

src/Kryon.Web/wwwroot/appsettings.Development.json

No mezclarla accidentalmente con el bloque funcional.

## REPORTE FINAL

Entrega un reporte breve con:

1. tareas completadas;
2. archivos creados;
3. archivos modificados;
4. decisiones relevantes;
5. tests ejecutados;
6. build;
7. format;
8. git diff --check;
9. riesgos o pendientes;
10. confirmación de que `{{SIGUIENTE_TAREA}}` no fue implementada.

Después DETENTE.
