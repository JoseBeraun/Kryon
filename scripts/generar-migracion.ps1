<#
.SYNOPSIS
Genera el script SQL idempotente de todas las migraciones de Kryon.

.DESCRIPTION
Ejecuta `dotnet tool restore` y `dotnet ef migrations script --idempotent` con la herramienta local y escribe el
resultado en artifacts/sql/kryon-migraciones-idempotente.sql (carpeta ignorada por git).

Solo genera el archivo: no aplica el SQL a ninguna base de datos, no arranca la API y no usa cadenas de conexión.
Termina con error si la generación falla.
#>

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$salida = Join-Path $raiz 'artifacts/sql/kryon-migraciones-idempotente.sql'

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $salida) | Out-Null

Push-Location $raiz
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore falló con código $LASTEXITCODE."
    }

    dotnet ef migrations script --idempotent `
        --project src/Kryon.Infrastructure `
        --startup-project src/Kryon.Infrastructure `
        --output $salida
    if ($LASTEXITCODE -ne 0) {
        throw "La generación del SQL idempotente falló con código $LASTEXITCODE."
    }

    Write-Host "SQL idempotente generado en $salida"
}
finally {
    Pop-Location
}
