# Arquitectura de Kryon

## Visión

Kryon se está reconstruyendo desde cero.

El sistema anterior se utiliza únicamente como referencia para ingeniería inversa y comprensión del comportamiento existente.

No se continúa desarrollando sobre el código legado.

## Stack actual

- .NET 10
- ASP.NET Core
- Blazor Web
- Minimal APIs
- Entity Framework Core 10
- SQL Server / LocalDB
- SpecKit+
- Claude Code
- Git

## Capas

Kryon.Web
↓
Kryon.Contracts
↓
Kryon.Api
↓
Kryon.Core
↓
Kryon.Infrastructure
↓
SQL Server

## Reglas arquitectónicas

### Core

Core no debe depender de:

- Infrastructure
- Contracts
- Web
- API

Core contiene:

- casos de uso;
- reglas;
- abstracciones;
- modelos de aplicación.

### Web

Web consume API.

Web no debe:

- acceder a DbContext;
- llamar Infrastructure;
- recalcular reglas de negocio;
- recalcular permisos;
- seleccionar EmpresaId manualmente.

### API

API actúa como frontera:

Core → Contracts → HTTP

La autorización debe utilizar las políticas existentes.

## Multiempresa

La empresa nunca se acepta desde parámetros arbitrarios enviados por el cliente.

Cadena conceptual:

Identidad autenticada
↓
IContextoSolicitud
↓
EF Global Query Filters
↓
SESSION_CONTEXT
↓
Row Level Security

## Seguridad

Actualmente existe una identidad especial solo para:

- Development
- Test

La autenticación real será una feature independiente futura.

## Gestión de Usuarios

Las acciones visibles en Web vienen de:

`AccionesPermitidas`

No se recalculan en cliente.

`puedeRegistrar` se calcula en backend.

## ETag / concurrencia

`rowversion` se representa mediante:

Base64 + strong HTTP ETag entre comillas.

La representación se centralizó para ser reutilizada por operaciones futuras.

## Gate RegistroUsuarios

El gate se considera abierto únicamente cuando existen las integraciones externas requeridas.

La detección verifica disponibilidad de servicios sin ejecutarlos.

## Dependencias externas

DEP-1 a DEP-5 permanecen como dependencias controladas según la feature.

No deben inventarse implementaciones para hacer pasar una tarea.

## Documentos oficiales

Ver:

[[../../specs/001-gestion-usuarios/spec]]
[[../../specs/001-gestion-usuarios/plan]]
[[../../specs/001-gestion-usuarios/tasks]]
[[../../specs/001-gestion-usuarios/research]]
[[../../specs/001-gestion-usuarios/data-model]]
