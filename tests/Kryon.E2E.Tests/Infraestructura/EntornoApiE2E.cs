using System.Data.Common;
using System.Net;
using System.Net.Sockets;

using Testcontainers.MsSql;

namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>
/// Entorno E2E de la API (T100): un SQL Server temporal en un contenedor propio, con el esquema aplicado desde el SQL
/// idempotente generado <b>antes</b> por <c>scripts/generar-migracion.ps1</c> y el dataset de <see cref="DatasetE2E"/>,
/// y <c>Kryon.Api</c> como <b>proceso real</b> en el entorno <c>Test</c> (esquema <c>IdentidadPrueba</c> y su política
/// CORS de pruebas).
/// </summary>
/// <remarks>
/// <para>
/// Caja negra (research §R11): no referencia <c>Kryon.Api</c> ni <c>Kryon.Api.Tests</c>; se comunica con la API solo por
/// HTTP y con la base de datos solo mediante SQL. No ejecuta <c>dotnet ef</c> ni <c>Database.Migrate</c>: si falta el
/// SQL, la preparación falla e indica cómo generarlo. El SQL se aplica con <c>sqlcmd</c> dentro del propio contenedor
/// (<see cref="MsSqlContainer.ExecScriptAsync"/>), sin dependencias nuevas.
/// </para>
/// <para>
/// La API se ejecuta desde la salida ya compilada de la solución (misma configuración que estas pruebas), en un puerto
/// libre de <c>127.0.0.1</c>. Su salida estándar y de error se captura para explicar cualquier fallo de arranque.
/// </para>
/// </remarks>
public sealed class EntornoApiE2E : IAsyncLifetime
{
    private const string ImagenSqlServer = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string BaseDatos = "KryonE2E";
    private const string RutaSqlMigraciones = "artifacts/sql/kryon-migraciones-idempotente.sql";
    private static readonly TimeSpan EsperaArranque = TimeSpan.FromSeconds(90);

    private MsSqlContainer? contenedor;
    private ProcesoE2E? api;

    /// <summary>URL base de la API real (con barra final), para las pruebas y para <see cref="EntornoWebE2E"/>.</summary>
    public Uri BaseAddress { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cancelacion.Token;

        // Antes que nada: sin el SQL generado no hay nada que preparar (y no se arranca ningún contenedor).
        var sqlMigraciones = LeerSqlMigraciones();

        // Se crea aquí y no en el constructor: Testcontainers necesita Docker para construir el contenedor.
        contenedor = new MsSqlBuilder(ImagenSqlServer).Build();
        await contenedor.StartAsync(ct);

        await EjecutarSqlAsync($"IF DB_ID(N'{BaseDatos}') IS NULL CREATE DATABASE [{BaseDatos}];", "crear la base de datos E2E", ct);
        await EjecutarSqlAsync($"USE [{BaseDatos}];\nGO\n{sqlMigraciones}", "aplicar el SQL idempotente de migraciones", ct);
        foreach (var (empresa, script) in DatasetE2E.ScriptsPorEmpresa(BaseDatos))
        {
            await EjecutarSqlAsync(script, $"sembrar el dataset E2E de la empresa {empresa}", ct);
        }

        BaseAddress = new Uri($"http://127.0.0.1:{PuertoLibre()}/");
        var salida = Rutas.SalidaCompilada("src/Kryon.Api", "Kryon.Api.dll");
        api = ProcesoE2E.Iniciar(
            "Kryon.Api",
            "dotnet",
            [salida],
            Path.GetDirectoryName(salida)!,
            new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Test",
                ["ASPNETCORE_URLS"] = BaseAddress.GetLeftPart(UriPartial.Authority),
                ["ConnectionStrings__Kryon"] = CadenaConexionE2E(contenedor.GetConnectionString()),
            });

        // Disponible = responde HTTP. Sin identidad, la API responde 401 a /api/usuarios: eso basta.
        await api.EsperarDisponibleAsync(new Uri(BaseAddress, "api/usuarios"), EsperaArranque, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (api is not null)
        {
            await api.DisposeAsync();
        }

        if (contenedor is not null)
        {
            await contenedor.DisposeAsync();
        }
    }

    /// <summary>Puerto TCP libre en el bucle local, elegido por el sistema operativo.</summary>
    internal static int PuertoLibre()
    {
        using var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        return ((IPEndPoint)escucha.LocalEndpoint).Port;
    }

    private static string LeerSqlMigraciones()
    {
        var ruta = Path.Combine(Rutas.Raiz, RutaSqlMigraciones);
        if (!File.Exists(ruta))
        {
            throw new InvalidOperationException(
                $"No existe '{RutaSqlMigraciones}'. Genera el SQL idempotente antes de ejecutar las pruebas E2E con " +
                "'pwsh scripts/generar-migracion.ps1' (el entorno E2E no genera ni aplica migraciones con dotnet ef).");
        }

        // sqlcmd no necesita la marca de orden de bytes que deja dotnet ef al principio del archivo.
        return File.ReadAllText(ruta).TrimStart('﻿');
    }

    /// <summary>La cadena del contenedor apuntando a la base de datos E2E en lugar de <c>master</c>.</summary>
    private static string CadenaConexionE2E(string cadenaContenedor)
    {
        var constructor = new DbConnectionStringBuilder { ConnectionString = cadenaContenedor };
        constructor["Database"] = BaseDatos;
        return constructor.ConnectionString;
    }

    private async Task EjecutarSqlAsync(string script, string paso, CancellationToken ct)
    {
        var resultado = await contenedor!.ExecScriptAsync(script, ct);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"No se pudo {paso} (sqlcmd terminó con {resultado.ExitCode}).\n{resultado.Stdout}\n{resultado.Stderr}");
        }
    }
}
