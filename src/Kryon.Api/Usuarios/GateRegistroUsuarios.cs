using Kryon.Core.Usuarios.Integraciones;

namespace Kryon.Api.Usuarios;

/// <summary>
/// Gate de entrega <c>RegistroUsuarios</c> (tasks.md → <i>Feature / release gating</i>): está abierto solo si hay
/// implementaciones registradas de <see cref="IPoliticaAltaUsuario"/> (DEP-2), <see cref="IPoliticaIdentificadorAcceso"/>
/// (DEP-3) e <see cref="IFuenteLimiteUsuarios"/> (DEP-5).
/// </summary>
/// <remarks>
/// Es un mecanismo de entrega, no una regla de negocio. Solo pregunta al contenedor si esos servicios están
/// registrados: no los crea ni llama a ninguno de sus métodos, y no resuelve ninguna de esas dependencias.
/// </remarks>
public static class GateRegistroUsuarios
{
    public static bool EstaAbierto(IServiceProviderIsService servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        return servicios.IsService(typeof(IPoliticaAltaUsuario))
            && servicios.IsService(typeof(IPoliticaIdentificadorAcceso))
            && servicios.IsService(typeof(IFuenteLimiteUsuarios));
    }
}
