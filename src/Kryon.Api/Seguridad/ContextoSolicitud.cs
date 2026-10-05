using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Kryon.Core.Seguridad;

namespace Kryon.Api.Seguridad;

/// <summary>
/// Tipos de claim que consume <see cref="ContextoSolicitudDesdeClaims"/>.
/// Todo adaptador de identidad que alimente este contexto debe exponer estos claims internos.
/// </summary>
public static class ClaimsContextoSolicitud
{
    /// <summary>Identificador (Guid) del usuario. Exactamente uno.</summary>
    public const string UsuarioId = "kryon:usuario_id";

    /// <summary>Identificador (Guid) de la empresa del usuario. Exactamente uno.</summary>
    public const string EmpresaId = "kryon:empresa_id";

    /// <summary>Nombre de una capacidad (permiso) del usuario. Un claim por capacidad.</summary>
    public const string Capacidad = "kryon:capacidad";
}

/// <summary>
/// Contexto de solicitud construido <b>solo</b> a partir de los claims de una identidad autenticada.
/// Nunca lee la empresa ni el usuario de la ruta, la query, el cuerpo ni otros datos del cliente.
/// </summary>
public sealed class ContextoSolicitudDesdeClaims : IContextoSolicitud
{
    private ContextoSolicitudDesdeClaims(Guid usuarioId, Guid empresaId, IReadOnlyCollection<string> capacidades)
    {
        UsuarioId = usuarioId;
        EmpresaId = empresaId;
        Capacidades = capacidades;
    }

    /// <inheritdoc />
    public Guid UsuarioId { get; }

    /// <inheritdoc />
    public Guid EmpresaId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Capacidades { get; }

    /// <summary>
    /// Intenta construir el contexto a partir de la identidad verificada de la solicitud.
    /// Falla de forma cerrada (devuelve <c>false</c>) si la identidad no está autenticada, o si falta,
    /// está repetido o no es un identificador válido el claim de usuario o el de empresa.
    /// En ese caso la solicitud debe tratarse como no autenticada.
    /// </summary>
    public static bool TryCrear(ClaimsPrincipal? principal, [NotNullWhen(true)] out ContextoSolicitudDesdeClaims? contexto)
    {
        contexto = null;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!TryLeerIdentificadorUnico(principal, ClaimsContextoSolicitud.UsuarioId, out var usuarioId)
            || !TryLeerIdentificadorUnico(principal, ClaimsContextoSolicitud.EmpresaId, out var empresaId))
        {
            return false;
        }

        var capacidades = principal.FindAll(ClaimsContextoSolicitud.Capacidad)
            .Select(claim => claim.Value)
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        contexto = new ContextoSolicitudDesdeClaims(usuarioId, empresaId, capacidades);
        return true;
    }

    private static bool TryLeerIdentificadorUnico(ClaimsPrincipal principal, string tipoClaim, out Guid identificador)
    {
        identificador = Guid.Empty;

        var claims = principal.FindAll(tipoClaim).ToArray();
        if (claims.Length != 1)
        {
            return false;
        }

        return Guid.TryParse(claims[0].Value, out identificador) && identificador != Guid.Empty;
    }
}
