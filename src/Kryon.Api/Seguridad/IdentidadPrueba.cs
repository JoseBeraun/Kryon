using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Kryon.Api.Seguridad;

/// <summary>
/// Esquema de autenticación <b>exclusivo para Development y pruebas automatizadas</b>.
/// No es la autenticación real de Kryon (la definirá su propia spec). Su registro condicional
/// por entorno lo hace T028: fuera de Development y Test no se registra y estos encabezados se ignoran.
/// </summary>
public static class IdentidadPrueba
{
    public const string Esquema = "IdentidadPrueba";

    public const string EncabezadoUsuarioId = "X-Kryon-Prueba-UsuarioId";
    public const string EncabezadoEmpresaId = "X-Kryon-Prueba-EmpresaId";

    /// <summary>Lista de capacidades separadas por comas.</summary>
    public const string EncabezadoCapacidades = "X-Kryon-Prueba-Capacidades";
}

/// <summary>
/// Traduce los encabezados <c>X-Kryon-Prueba-*</c> a los mismos claims que consume
/// <see cref="ContextoSolicitudDesdeClaims"/>. Falla de forma cerrada: si falta el usuario o la empresa,
/// si no son Guid válidos, si son <see cref="Guid.Empty"/> o si vienen repetidos, no produce ninguna identidad.
/// </summary>
public sealed class ManejadorIdentidadPrueba(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var encabezados = Request.Headers;
        if (!encabezados.ContainsKey(IdentidadPrueba.EncabezadoUsuarioId)
            && !encabezados.ContainsKey(IdentidadPrueba.EncabezadoEmpresaId)
            && !encabezados.ContainsKey(IdentidadPrueba.EncabezadoCapacidades))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!TryLeerIdentificador(encabezados[IdentidadPrueba.EncabezadoUsuarioId], out var usuarioId)
            || !TryLeerIdentificador(encabezados[IdentidadPrueba.EncabezadoEmpresaId], out var empresaId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Identidad de prueba incompleta o inválida."));
        }

        var claims = new List<Claim>
        {
            new(ClaimsContextoSolicitud.UsuarioId, usuarioId.ToString()),
            new(ClaimsContextoSolicitud.EmpresaId, empresaId.ToString()),
        };

        var capacidades = encabezados[IdentidadPrueba.EncabezadoCapacidades]
            .SelectMany(valor => (valor ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal);
        claims.AddRange(capacidades.Select(capacidad => new Claim(ClaimsContextoSolicitud.Capacidad, capacidad)));

        var identidad = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool TryLeerIdentificador(StringValues valores, out Guid identificador)
    {
        identificador = Guid.Empty;
        return valores.Count == 1
            && Guid.TryParse(valores[0], out identificador)
            && identificador != Guid.Empty;
    }
}
