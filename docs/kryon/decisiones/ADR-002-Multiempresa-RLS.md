# ADR-002 — Aislamiento multiempresa y RLS

## Estado

Aceptado

## Contexto

Kryon es un ERP SaaS multiempresa.

Los datos de una empresa no deben ser accesibles por otra empresa.

Confiar únicamente en filtros enviados desde frontend no proporciona aislamiento suficiente.

## Decisión

EmpresaId no se toma como criterio arbitrario enviado por el cliente.

El contexto de empresa se obtiene desde la identidad autenticada.

Cadena conceptual:

Identidad
↓
IContextoSolicitud
↓
EF Global Query Filters
↓
SESSION_CONTEXT
↓
Row Level Security

## Reglas

- frontend no selecciona EmpresaId para aislar datos;
- API obtiene empresa desde el contexto autenticado;
- EF aplica filtros de aplicación;
- SQL Server recibe contexto de sesión;
- RLS actúa como última barrera.

## Objetivo

Defensa en profundidad.

Si una capa falla, otra debe seguir evitando acceso cruzado entre empresas.

## Consecuencia

Las consultas y operaciones nuevas deben respetar siempre el contexto multiempresa.

No se deben introducir consultas que ignoren este aislamiento sin justificación explícita.

## Relacionado

[[../01-Arquitectura-Kryon]]
[[../features/001-Gestion-Usuarios]]
