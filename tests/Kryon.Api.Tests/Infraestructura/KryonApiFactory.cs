using Kryon.Core.Seguridad;
using Kryon.Infrastructure.Persistencia;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Testcontainers.MsSql;

namespace Kryon.Api.Tests.Infraestructura;

/// <summary>
/// Levanta <c>Kryon.Api</c> real en el entorno <c>Test</c> contra un SQL Server real en un contenedor de
/// Testcontainers. Un contenedor por instancia de la factoría, compartido por todos los clientes que crea.
/// </summary>
/// <remarks>
/// Las migraciones reales (incluidas RLS y los permisos de auditoría) se aplican con <c>Database.MigrateAsync</c>
/// <b>solo dentro de este arnés</b>: la API nunca migra al arrancar (research §R12) y no hace falta <c>dotnet-ef</c>.
/// La API usa la misma composición que producción (contexto de solicitud, interceptor de empresa, filtros y RLS).
/// Limitación: la conexión es la del administrador del contenedor; los permisos efectivos del rol
/// <c>kryon_aplicacion</c> solo se comprueban donde una prueba lo haga explícitamente.
/// </remarks>
public sealed class KryonApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ImagenSqlServer = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? contenedor;

    /// <summary>Cadena de conexión al SQL Server del contenedor, para pruebas que necesiten SQL directo.</summary>
    public string CadenaConexion => (contenedor
        ?? throw new InvalidOperationException("KryonApiFactory no está inicializada: falta InitializeAsync.")).GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        // Se crea aquí y no en el constructor: Testcontainers necesita Docker para construir el contenedor.
        contenedor = new MsSqlBuilder(ImagenSqlServer).Build();
        await contenedor.StartAsync();

        var opciones = new DbContextOptionsBuilder<KryonDbContext>()
            .UseSqlServer(CadenaConexion)
            .Options;

        await using var contexto = new KryonDbContext(opciones, new ContextoMigracion());
        await contexto.Database.MigrateAsync();

        // quickstart.md: los datos semilla se cargan automáticamente en las pruebas de integración.
        await SemillaDatos.SembrarAsync(CadenaConexion);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (contenedor is not null)
        {
            await contenedor.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Kryon", CadenaConexion);
    }

    /// <summary>
    /// Solo para construir el modelo al migrar: no representa ninguna solicitud ni empresa y no se usa en la API.
    /// </summary>
    private sealed class ContextoMigracion : IContextoSolicitud
    {
        public Guid UsuarioId => Guid.Empty;

        public Guid EmpresaId => Guid.Empty;

        public IReadOnlyCollection<string> Capacidades => [];
    }
}
