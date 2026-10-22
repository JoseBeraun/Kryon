# ADR-004 — Gate de RegistroUsuarios

## Estado

Aceptado

## Contexto

La capacidad de registrar usuarios no depende únicamente de que el usuario tenga permiso.

También existen dependencias externas necesarias para completar correctamente el alta.

## Decisión

El backend utiliza GateRegistroUsuarios.

El gate comprueba la disponibilidad registrada de:

- IPoliticaAltaUsuario
- IPoliticaIdentificadorAcceso
- IFuenteLimiteUsuarios

La comprobación determina si las dependencias existen.

No las instancia ni ejecuta para realizar el chequeo.

## Regla

puedeRegistrar se determina conceptualmente como:

capacidad de registrar
+
gate disponible

Ambas condiciones deben cumplirse.

## Motivo

Evitar mostrar una acción que el sistema todavía no puede ejecutar correctamente.

## Consecuencia

El frontend no decide por sí mismo si se puede registrar.

Consume el valor entregado por backend.

Las dependencias faltantes no deben reemplazarse con implementaciones ficticias solo para habilitar la interfaz.

## Relacionado

[[../features/001-Gestion-Usuarios]]
[[../bloques/T050]]
[[../01-Arquitectura-Kryon]]
