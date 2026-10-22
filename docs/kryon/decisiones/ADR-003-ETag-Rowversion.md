# ADR-003 — Rowversion y Strong ETag

## Estado

Aceptado

## Contexto

Las operaciones de edición futura necesitan detectar cambios concurrentes.

SQL Server utiliza rowversion como token de concurrencia.

La API necesita una representación HTTP consistente.

## Decisión

rowversion se transforma a Base64.

La representación HTTP utiliza un strong ETag entre comillas.

Ejemplo conceptual:

"BASE64..."

La generación se centraliza mediante VersionEtag.

## Objetivo

Evitar que cada endpoint implemente su propia representación de concurrencia.

## Uso esperado

Lectura:
- backend devuelve ETag.

Modificación:
- cliente envía If-Match.

Backend:
- valida concurrencia antes de confirmar cambios.

## Consecuencia

Las operaciones futuras de edición, activación o desactivación deben reutilizar esta representación cuando corresponda.

No duplicar lógica de ETag en distintos endpoints.

## Relacionado

[[../features/001-Gestion-Usuarios]]
[[../bloques/T050]]
