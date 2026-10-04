using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kryon.Api.Seguridad;

/// <summary>
/// Esquema por defecto fuera de Development y Test mientras no exista la autenticación real (futura spec de
/// autenticación). <b>No autentica a nadie</b>: no lee encabezados, no crea identidades ni acepta usuario, empresa o
/// capacidades. Solo permite que una operación que exige autenticación responda <c>401</c> en lugar de fallar.
/// </summary>
public static class SinAutenticacion
{
    public const string Esquema = "SinAutenticacion";
}

/// <summary>Manejador de <see cref="SinAutenticacion"/>: ninguna solicitud queda autenticada.</summary>
public sealed class ManejadorSinAutenticacion(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
