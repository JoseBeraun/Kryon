# ADR-001 — Reconstrucción de Kryon desde cero

## Estado

Aceptado

## Contexto

Existe una versión anterior de Kryon.

Sin embargo, el proyecto actual no continúa desarrollándose directamente sobre ese código.

## Decisión

Kryon se reconstruye desde cero.

La versión anterior se utiliza únicamente como referencia para:

- entender comportamiento;
- revisar flujos existentes;
- identificar reglas;
- hacer ingeniería inversa;
- recuperar conocimiento funcional.

## Consecuencia

No se copian automáticamente:

- decisiones antiguas;
- inconsistencias;
- límites técnicos;
- deuda técnica;
- reglas no verificadas.

Cada comportamiento debe justificarse dentro del nuevo diseño.

## Relacionado

[[../01-Arquitectura-Kryon]]
[[../04-Roadmap-Kryon]]
