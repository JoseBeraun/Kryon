using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios.Eventos;

namespace Kryon.Core.Usuarios;

/// <summary>
/// Caso de uso de desactivar y activar un usuario (US2; FR-032 a FR-038, FR-043; data-model.md → <i>Transiciones de
/// estado</i>). Todo ocurre en una transacción: bloqueo por empresa (solo al desactivar) → cargar → verificar la versión
/// → aplicar la regla → guardar → auditar → confirmar; después de confirmar, la desactivación publica
/// <see cref="UsuarioDesactivado"/> (DEP-1).
/// </summary>
/// <remarks>
/// La empresa y el actor salen solo del <see cref="IContextoSolicitud"/> verificado. El permiso lo comprueba la política
/// del endpoint. Si cualquier paso falla, la transacción se revierte y no queda ni el cambio ni su auditoría.
/// </remarks>
public sealed class CambiarEstadoUsuario(
    IUnidadDeTrabajo unidadDeTrabajo,
    IBloqueoAdministradoresEmpresa bloqueo,
    IRepositorioUsuarios repositorio,
    IRegistroAuditoria auditoria,
    IPublicadorEventos publicador,
    IContextoSolicitud contexto,
    TimeProvider reloj)
{
    /// <summary>Desactiva un usuario Activo de la empresa actual.</summary>
    /// <param name="usuarioId">Usuario solicitado.</param>
    /// <param name="version">Versión con la que se cargó (de <c>If-Match</c>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario ya desactivado, con su nueva versión.</returns>
    /// <exception cref="ErrorUsuarios">
    /// <c>usuario-no-encontrado</c>, <c>usuario-modificado</c>, <c>no-puede-desactivarse-a-si-mismo</c> o
    /// <c>ultimo-administrador</c>.
    /// </exception>
    public async Task<Usuario> DesactivarAsync(Guid usuarioId, byte[] version, CancellationToken cancellationToken)
    {
        var usuario = await EnTransaccionAsync(
            usuarioId,
            version,
            TipoOperacion.Desactivacion,
            async (cargado, ct) =>
            {
                // Con el bloqueo tomado, nadie más puede reducir los administradores de esta empresa a la vez (research §R5).
                var antes = await repositorio.ContarAdministradoresActivosAsync(ct);
                cargado.Desactivar(contexto.UsuarioId);
                await repositorio.ActualizarAsync(cargado, ct);

                // El recuento ya refleja el cambio guardado dentro de la transacción. Solo se rechaza si este cambio es
                // el que deja a la empresa sin administradores (FR-036).
                var despues = await repositorio.ContarAdministradoresActivosAsync(ct);
                if (despues < antes)
                {
                    ReglaUltimoAdministrador.Verificar(despues);
                }
            },
            bloquear: true,
            cancellationToken);

        // Solo una desactivación ya confirmada se publica (DEP-1: sin suscriptores en esta feature).
        await publicador.PublicarAsync(new UsuarioDesactivado(usuario.Id, contexto.EmpresaId, reloj.GetUtcNow()), cancellationToken);
        return usuario;
    }

    /// <summary>Activa un usuario Inactivo de la empresa actual.</summary>
    /// <param name="usuarioId">Usuario solicitado.</param>
    /// <param name="version">Versión con la que se cargó (de <c>If-Match</c>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario ya activado, con su nueva versión.</returns>
    /// <exception cref="ErrorUsuarios"><c>usuario-no-encontrado</c> o <c>usuario-modificado</c>.</exception>
    public Task<Usuario> ActivarAsync(Guid usuarioId, byte[] version, CancellationToken cancellationToken) =>
        EnTransaccionAsync(
            usuarioId,
            version,
            TipoOperacion.Activacion,
            async (cargado, ct) =>
            {
                cargado.Activar();
                await repositorio.ActualizarAsync(cargado, ct);
            },
            bloquear: false,
            cancellationToken);

    private async Task<Usuario> EnTransaccionAsync(
        Guid usuarioId,
        byte[] version,
        TipoOperacion operacion,
        Func<Usuario, CancellationToken, Task> cambiar,
        bool bloquear,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        await unidadDeTrabajo.IniciarAsync(cancellationToken);
        var confirmando = false;
        try
        {
            if (bloquear)
            {
                await bloqueo.AdquirirAsync(cancellationToken);
            }

            // Un usuario de otra empresa y uno inexistente son indistinguibles (FR-003).
            var usuario = await repositorio.ObtenerAsync(usuarioId, cancellationToken)
                ?? throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioNoEncontrado, "El usuario no existe o no es accesible.");

            if (!version.AsSpan().SequenceEqual(usuario.Version))
            {
                throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioModificado, "La versión no coincide con la vigente.");
            }

            await cambiar(usuario, cancellationToken);

            await auditoria.RegistrarAsync(
                new AuditoriaUsuario(
                    empresaId: contexto.EmpresaId,
                    actorUsuarioId: contexto.UsuarioId,
                    ocurridoEn: reloj.GetUtcNow(),
                    tipoOperacion: operacion,
                    usuarioAfectadoId: usuario.Id,
                    operacionSolicitada: null,
                    cambiosJson: null),
                cancellationToken);

            // Si la confirmación falla, la transacción ya no existe: no se revierte otra vez y el error sale tal cual.
            confirmando = true;
            await unidadDeTrabajo.ConfirmarAsync(cancellationToken);
            return usuario;
        }
        finally
        {
            if (!confirmando)
            {
                await unidadDeTrabajo.RevertirAsync(CancellationToken.None);
            }
        }
    }
}
