using Kryon.Core.Usuarios.Eventos;

namespace Kryon.Infrastructure.Usuarios;

/// <summary>
/// Publicador de eventos de dominio sin suscriptores: esta feature publica <see cref="UsuarioDesactivado"/> pero no se
/// suscribe a él (DEP-1, T065). Qué ocurre con las sesiones del usuario desactivado lo decidirá la futura spec de
/// autenticación, que sustituirá este registro por su mecanismo de entrega.
/// </summary>
public sealed class PublicadorEventosSinSuscriptores : IPublicadorEventos
{
    public Task PublicarAsync(UsuarioDesactivado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return Task.CompletedTask;
    }
}
