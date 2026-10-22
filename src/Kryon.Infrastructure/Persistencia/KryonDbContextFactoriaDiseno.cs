using Kryon.Core.Seguridad;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kryon.Infrastructure.Persistencia;

/// <summary>
/// Crea <see cref="KryonDbContext"/> <b>solo para el tooling de EF Core</b> (crear migraciones y generar el SQL).
/// No se registra en DI, no depende de <c>Kryon.Api</c>, no abre conexiones ni ejecuta operaciones de empresa.
/// </summary>
/// <remarks>
/// El proveedor se configura sin cadena de conexión: crear migraciones y generar el script se basan solo en el
/// modelo. El contexto de solicitud es una implementación de diseño privada, inaccesible en ejecución, que existe
/// únicamente porque el constructor lo exige para construir el modelo; no representa ninguna solicitud ni empresa y
/// su valor no forma parte del esquema.
/// </remarks>
public sealed class KryonDbContextFactoriaDiseno : IDesignTimeDbContextFactory<KryonDbContext>
{
    public KryonDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<KryonDbContext>()
            .UseSqlServer()
            .Options;

        return new KryonDbContext(opciones, new ContextoSolicitudDiseno());
    }

    private sealed class ContextoSolicitudDiseno : IContextoSolicitud
    {
        public Guid UsuarioId => Guid.Empty;

        public Guid EmpresaId => Guid.Empty;

        public IReadOnlyCollection<string> Capacidades => [];
    }
}
