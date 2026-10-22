namespace Kryon.Core.Usuarios.Eventos;

/// <summary>
/// Evento de dominio: el usuario <see cref="UsuarioId"/> de la empresa <see cref="EmpresaId"/> ya fue desactivado
/// en <see cref="OcurridoEn"/>. Es el punto de integración de DEP-1: qué ocurre con sus sesiones lo decidirá la
/// futura spec de autenticación; esta feature no se suscribe al evento.
/// </summary>
public sealed record UsuarioDesactivado
{
    /// <param name="usuarioId">Usuario desactivado.</param>
    /// <param name="empresaId">Empresa del usuario, ya validada por el caso de uso.</param>
    /// <param name="ocurridoEn">Instante de la desactivación, en UTC.</param>
    public UsuarioDesactivado(Guid usuarioId, Guid empresaId, DateTimeOffset ocurridoEn)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(usuarioId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(empresaId, Guid.Empty);
        if (ocurridoEn.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar en UTC.", nameof(ocurridoEn));
        }

        UsuarioId = usuarioId;
        EmpresaId = empresaId;
        OcurridoEn = ocurridoEn;
    }

    public Guid UsuarioId { get; }

    /// <summary>Empresa del usuario desactivado.</summary>
    public Guid EmpresaId { get; }

    /// <summary>UTC.</summary>
    public DateTimeOffset OcurridoEn { get; }
}

/// <summary>
/// Salida de los eventos de dominio de Core. Esta feature solo publica: no define consumidores ni efectos sobre las
/// sesiones (DEP-1 sigue abierta), ni el mecanismo de entrega.
/// </summary>
public interface IPublicadorEventos
{
    /// <summary>Publica que un usuario fue desactivado.</summary>
    /// <param name="evento">Evento de una desactivación ya confirmada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task PublicarAsync(UsuarioDesactivado evento, CancellationToken cancellationToken);
}
