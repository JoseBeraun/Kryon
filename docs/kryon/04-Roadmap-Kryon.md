# Roadmap general de Kryon

## Plataforma

- [/] Gestión de Usuarios
- [ ] Autenticación
- [ ] Roles y Permisos
- [ ] Empresas
- [ ] Sucursales
- [ ] Alcance local por tienda
- [ ] Alcance global / multi-sede
- [ ] Monitor de sesiones
- [ ] Auditoría
- [ ] Configuración general

## Gestión de Usuarios

Estado actual:

T001-T054 completadas.

- [x] Listado
- [x] Búsqueda
- [x] Filtros
- [x] Roles asignables
- [x] Paginación
- [x] Estado vacío
- [x] Sin resultados
- [x] Sin permiso
- [x] Error de red
- [ ] Activar / desactivar
- [ ] Detalle
- [ ] Registro
- [ ] Edición
- [ ] Concurrencia completa
- [ ] QA / E2E final

Siguiente bloque:

T055 en adelante.

## Seguridad

- [ ] Autenticación real
- [ ] Recuperación de contraseña
- [ ] Cambio inicial de contraseña
- [ ] Cierre por inactividad
- [ ] Historial de accesos
- [ ] Sesiones activas
- [ ] Protección de accesos
- [ ] Matriz de permisos

## Estructura organizacional

- [ ] Empresas
- [ ] Sucursales
- [ ] Tiendas
- [ ] Alcance local
- [ ] Alcance global

## Comercial

- [ ] Clientes
- [ ] Productos
- [ ] Servicios
- [ ] Ventas
- [ ] Facturación

## Operaciones

- [ ] Inventario
- [ ] Almacenes
- [ ] Compras
- [ ] Proveedores
- [ ] Movimientos de inventario

## Finanzas

- [ ] Caja
- [ ] Cuentas por cobrar
- [ ] Cuentas por pagar
- [ ] Reportes financieros

## SUNAT

- [ ] Facturación electrónica
- [ ] Factura
- [ ] Boleta
- [ ] Nota de crédito
- [ ] Nota de débito
- [ ] Certificados digitales
- [ ] XML UBL
- [ ] Firma digital
- [ ] Envío CPE
- [ ] CDR
- [ ] Reintentos
- [ ] Outbox
- [ ] Consulta de estado
- [ ] Auditoría tributaria

## Analítica

- [ ] Dashboard gerencial
- [ ] KPIs
- [ ] Reportes
- [ ] Business Analytics Agent

## Agentes previstos

- Frontend Agent
- Backend Agent
- Database Agent
- QA Agent
- Security / Reviewer Agent
- Business Analytics Agent

## Flujo SDD

Cada feature grande seguirá:

Specify
↓
Clarify
↓
Plan
↓
Tasks
↓
Analyze
↓
Implement
↓
Validate
↓
Release

## Modo Turbo

Cuando las dependencias lo permitan:

- agrupar 3 a 6 tareas;
- ejecutar tareas [P] de forma paralela o coordinada;
- validar el bloque completo;
- ejecutar build;
- ejecutar tests;
- ejecutar format;
- ejecutar git diff --check;
- revisar antes del commit.

No agrupar demasiadas tareas si una decisión incorrecta podría afectar muchas tareas posteriores.

## Navegación

- [[00-Control-Center]]
- [[01-Arquitectura-Kryon]]
- [[02-Estado-Actual]]
- [[03-Entorno-Local]]
- [[features/001-Gestion-Usuarios]]
