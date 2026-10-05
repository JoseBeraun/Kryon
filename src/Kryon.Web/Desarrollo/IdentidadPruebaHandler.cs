using Microsoft.Extensions.Configuration;

namespace Kryon.Web.Desarrollo;

/// <summary>
/// <b>Solo Development.</b> Añade a cada solicitud los encabezados <c>X-Kryon-Prueba-*</c> que entiende el esquema
/// <c>IdentidadPrueba</c> de la API (T009), con la identidad de <c>wwwroot/appsettings.Development.json</c>. No
/// representa la autenticación real de Kryon, que definirá su propia spec. <c>Program.cs</c> solo lo pone en la cadena
/// del <see cref="HttpClient"/> en Development; en otros entornos no existe.
/// </summary>
/// <remarks>
/// Solo añade encabezados: no toca el método, la URI ni el cuerpo, y no conoce ninguna operación ni regla de
/// autorización. Si la solicitud ya trae alguno de estos encabezados (por ejemplo, porque se reenvía), se reemplaza:
/// nunca se duplican ni se mezclan con otra identidad.
/// </remarks>
public sealed class IdentidadPruebaHandler : DelegatingHandler
{
    /// <summary>Sección de configuración, solo en <c>wwwroot/appsettings.Development.json</c>.</summary>
    public const string Seccion = "IdentidadPrueba";

    // Mismos nombres que Kryon.Api.Seguridad.IdentidadPrueba (T009); Kryon.Web no referencia Kryon.Api.
    public const string EncabezadoUsuarioId = "X-Kryon-Prueba-UsuarioId";
    public const string EncabezadoEmpresaId = "X-Kryon-Prueba-EmpresaId";
    public const string EncabezadoCapacidades = "X-Kryon-Prueba-Capacidades";

    private readonly string _usuarioId;
    private readonly string _empresaId;
    private readonly string? _capacidades;

    public IdentidadPruebaHandler(Guid usuarioId, Guid empresaId, IReadOnlyCollection<string> capacidades)
    {
        ArgumentNullException.ThrowIfNull(capacidades);
        if (usuarioId == Guid.Empty || empresaId == Guid.Empty)
        {
            throw new ArgumentException("La identidad de prueba necesita un usuario y una empresa distintos de Guid.Empty.");
        }

        if (capacidades.Any(c => string.IsNullOrWhiteSpace(c) || c.Contains(',', StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Una capacidad de prueba no puede estar vacía ni contener comas: el encabezado las separa con comas.",
                nameof(capacidades));
        }

        _usuarioId = usuarioId.ToString();
        _empresaId = empresaId.ToString();

        // Formato que consume la API: una sola lista separada por comas. Sin capacidades, el encabezado no se envía.
        _capacidades = capacidades.Count == 0 ? null : string.Join(',', capacidades);
    }

    /// <summary>
    /// Crea el handler con <c>IdentidadPrueba:UsuarioId</c>, <c>IdentidadPrueba:EmpresaId</c> y la lista
    /// <c>IdentidadPrueba:Capacidades</c>. Si falta el usuario o la empresa, falla al arrancar en vez de enviar una
    /// identidad incompleta.
    /// </summary>
    public static IdentidadPruebaHandler DesdeConfiguracion(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        var seccion = configuracion.GetSection(Seccion);

        if (!Guid.TryParse(seccion["UsuarioId"], out var usuarioId) || !Guid.TryParse(seccion["EmpresaId"], out var empresaId))
        {
            throw new InvalidOperationException(
                $"Falta '{Seccion}:UsuarioId' o '{Seccion}:EmpresaId' en la configuración de Development, o no son Guid válidos.");
        }

        var capacidades = seccion.GetSection("Capacidades").GetChildren()
            .Select(c => c.Value ?? string.Empty)
            .ToArray();
        return new IdentidadPruebaHandler(usuarioId, empresaId, capacidades);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Reemplazar(request, EncabezadoUsuarioId, _usuarioId);
        Reemplazar(request, EncabezadoEmpresaId, _empresaId);
        Reemplazar(request, EncabezadoCapacidades, _capacidades);

        return base.SendAsync(request, cancellationToken);
    }

    private static void Reemplazar(HttpRequestMessage request, string encabezado, string? valor)
    {
        request.Headers.Remove(encabezado);
        if (valor is not null)
        {
            request.Headers.Add(encabezado, valor);
        }
    }
}
