# Entorno local de Kryon

## Proyecto

Ruta:

C:\Users\HP\Documents\kryon

## SQL Server LocalDB

Instancia:

MSSQLLocalDB

Ejecutable:

C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe

Comando para iniciar LocalDB:

& "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe" start MSSQLLocalDB

## Base de datos

Base:

Kryon

Cadena local:

Server=(localdb)\MSSQLLocalDB;Database=Kryon;Trusted_Connection=True;TrustServerCertificate=True;

La cadena está guardada mediante User Secrets de Kryon.Api.

## Migraciones conocidas

- 20261003173642_Inicial
- 20261003182415_SeguridadFilas
- 20261003183555_PermisosAuditoria

## API

Ejecutar:

dotnet run --project src/Kryon.Api/Kryon.Api.csproj

URL local:

http://localhost:5292

## Web

Ejecutar:

dotnet run --project src/Kryon.Web/Kryon.Web.csproj

URL local:

http://localhost:5106

Gestión de Usuarios:

http://localhost:5106/usuarios

## Configuración Development

Archivo:

src/Kryon.Web/wwwroot/appsettings.Development.json

Actualmente Api:UrlBase debe apuntar localmente a:

http://localhost:5292/

Este cambio local debe revisarse antes de incluirlo en commits.

## Identidad de desarrollo

Web utiliza IdentidadPruebaHandler.

API utiliza IdentidadPrueba.

Headers:

- X-Kryon-Prueba-UsuarioId
- X-Kryon-Prueba-EmpresaId
- X-Kryon-Prueba-Capacidades

Este mecanismo es solo para Development/Test.

La autenticación real será una feature futura.
