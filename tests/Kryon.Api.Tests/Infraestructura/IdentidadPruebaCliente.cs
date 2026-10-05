using Kryon.Api.Seguridad;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Kryon.Api.Tests.Infraestructura;

/// <summary>Usuario de los datos semilla de las pruebas: su id, su empresa y las capacidades de su rol.</summary>
public sealed record UsuarioSemilla(Guid UsuarioId, Guid EmpresaId, IReadOnlyCollection<string> Capacidades);

/// <summary>
/// Clientes autenticados con el esquema <c>IdentidadPrueba</c> (T009), que solo existe en Development y Test.
/// <b>Todas</b> las pruebas de API se autentican solo mediante este ayudante.
/// </summary>
public static class IdentidadPruebaCliente
{
    /// <summary>
    /// <see cref="HttpClient"/> que se presenta como <paramref name="usuarioSemilla"/>, con la empresa de ese usuario
    /// y sus capacidades, o con <paramref name="capacidades"/> si se indican.
    /// </summary>
    public static HttpClient ClienteComo(
        this WebApplicationFactory<Program> fabrica,
        UsuarioSemilla usuarioSemilla,
        IEnumerable<string>? capacidades = null)
    {
        ArgumentNullException.ThrowIfNull(fabrica);
        ArgumentNullException.ThrowIfNull(usuarioSemilla);

        var listaCapacidades = (capacidades ?? usuarioSemilla.Capacidades).ToArray();
        if (listaCapacidades.Any(c => c.Contains(',', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Una capacidad no puede contener comas: el encabezado las separa con comas.", nameof(capacidades));
        }

        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add(IdentidadPrueba.EncabezadoUsuarioId, usuarioSemilla.UsuarioId.ToString());
        cliente.DefaultRequestHeaders.Add(IdentidadPrueba.EncabezadoEmpresaId, usuarioSemilla.EmpresaId.ToString());
        if (listaCapacidades.Length > 0)
        {
            cliente.DefaultRequestHeaders.Add(IdentidadPrueba.EncabezadoCapacidades, string.Join(',', listaCapacidades));
        }

        return cliente;
    }
}
