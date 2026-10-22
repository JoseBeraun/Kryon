using System.Globalization;
using System.Text.Json;

using Kryon.Core.Seguridad;

namespace Kryon.Core.Usuarios;

/// <summary>
/// Caso de uso de editar el nombre completo y el rol de un usuario (US4; FR-024 a FR-026, FR-035, FR-036, FR-038,
/// FR-044). Todo ocurre en una transacción: cargar → verificar la versión → validar → regla del propio rol → (si cambia
/// el rol) bloqueo por empresa y regla del último administrador → guardar → auditar <c>Modificacion</c> solo con los
/// campos cambiados → confirmar.
/// </summary>
/// <remarks>
/// El identificador de acceso no se edita aquí: si es editable y con qué reglas lo deciden DEP-3 y DEP-4 (T079, T080).
/// La empresa y el actor salen solo del <see cref="IContextoSolicitud"/> verificado; el permiso lo comprueba la política
/// del endpoint. Si cualquier paso falla, la transacción se revierte y no queda ni el cambio ni su auditoría.
/// </remarks>
public sealed class EditarUsuario(
    IUnidadDeTrabajo unidadDeTrabajo,
    IBloqueoAdministradoresEmpresa bloqueo,
    IRepositorioUsuarios repositorio,
    IRegistroAuditoria auditoria,
    ValidadorUsuario validador,
    IContextoSolicitud contexto,
    TimeProvider reloj)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    /// <summary>Edita un usuario de la empresa actual.</summary>
    /// <param name="usuarioId">Usuario solicitado.</param>
    /// <param name="version">Versión con la que se cargó (de <c>If-Match</c>).</param>
    /// <param name="nombreCompleto">Nombre completo nuevo; se recortan los espacios.</param>
    /// <param name="rolId">Rol nuevo; debe ser de la empresa actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario editado, con su nueva versión si algo cambió.</returns>
    /// <exception cref="ErrorUsuarios">
    /// <c>usuario-no-encontrado</c>, <c>usuario-modificado</c>, <c>validacion</c>, <c>rol-no-valido</c>,
    /// <c>no-puede-cambiar-su-propio-rol</c> o <c>ultimo-administrador</c>.
    /// </exception>
    public async Task<Usuario> EditarAsync(
        Guid usuarioId,
        byte[] version,
        string nombreCompleto,
        Guid rolId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        await unidadDeTrabajo.IniciarAsync(cancellationToken);
        var confirmando = false;
        try
        {
            // Un usuario de otra empresa y uno inexistente son indistinguibles (FR-003).
            var usuario = await repositorio.ObtenerAsync(usuarioId, cancellationToken)
                ?? throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioNoEncontrado, "El usuario no existe o no es accesible.");

            if (!version.AsSpan().SequenceEqual(usuario.Version))
            {
                throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioModificado, "La versión no coincide con la vigente.");
            }

            var datos = await validador.ValidarAsync(nombreCompleto, null, incluyeIdentificador: false, rolId, cancellationToken);

            var cambios = new List<CambioCampo>();
            if (datos.NombreCompleto != usuario.NombreCompleto)
            {
                cambios.Add(new CambioCampo(ValidadorUsuario.CampoNombreCompleto, usuario.NombreCompleto, datos.NombreCompleto));
            }

            var cambiaRol = datos.Rol.Id != usuario.RolId;
            if (cambiaRol)
            {
                // Se permite editar el propio nombre, no el propio rol (FR-035).
                if (usuario.Id == contexto.UsuarioId)
                {
                    throw new ErrorUsuarios(CodigoErrorUsuarios.NoPuedeCambiarSuPropioRol, "No se puede cambiar el propio rol.");
                }

                cambios.Add(new CambioCampo(ValidadorUsuario.CampoRolId, Texto(usuario.RolId), Texto(datos.Rol.Id)));
            }

            if (cambios.Count > 0)
            {
                await GuardarAsync(usuario, datos, cambiaRol, cancellationToken);

                await auditoria.RegistrarAsync(
                    new AuditoriaUsuario(
                        empresaId: contexto.EmpresaId,
                        actorUsuarioId: contexto.UsuarioId,
                        ocurridoEn: reloj.GetUtcNow(),
                        tipoOperacion: TipoOperacion.Modificacion,
                        usuarioAfectadoId: usuario.Id,
                        operacionSolicitada: null,
                        cambiosJson: JsonSerializer.Serialize(cambios, OpcionesJson)),
                    cancellationToken);
            }

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

    /// <summary>
    /// Aplica y guarda los cambios. Si cambia el rol, con el bloqueo por empresa tomado nadie más puede reducir los
    /// administradores a la vez (research §R5), y se rechaza solo si este cambio deja a la empresa sin administradores
    /// activos (FR-036). Un cambio concurrente del mismo usuario lo detecta su <c>rowversion</c> al guardar.
    /// </summary>
    /// <remarks>
    /// Decisión técnica, no regla de negocio: se bloquea ante cualquier cambio de rol, no solo el de un administrador,
    /// porque Core no conoce qué capacidad define a un administrador y el usuario solo se sabe si cambia de rol después
    /// de cargarlo. El bloqueo se toma antes de los recuentos y dura hasta confirmar o revertir la transacción, así que
    /// los cambios de rol de una misma empresa quedan serializados.
    /// </remarks>
    private async Task GuardarAsync(Usuario usuario, DatosUsuarioValidados datos, bool cambiaRol, CancellationToken cancellationToken)
    {
        var antes = 0;
        if (cambiaRol)
        {
            await bloqueo.AdquirirAsync(cancellationToken);
            antes = await repositorio.ContarAdministradoresActivosAsync(cancellationToken);
        }

        usuario.CambiarNombreCompleto(datos.NombreCompleto);
        usuario.CambiarRol(datos.Rol.Id);
        await repositorio.ActualizarAsync(usuario, cancellationToken);

        if (cambiaRol)
        {
            var despues = await repositorio.ContarAdministradoresActivosAsync(cancellationToken);
            if (despues < antes)
            {
                ReglaUltimoAdministrador.Verificar(despues);
            }
        }
    }

    private static string Texto(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>Un elemento de <c>CambiosJson</c> (T027): <c>{"campo":…,"anterior":…,"nuevo":…}</c>.</summary>
    private sealed record CambioCampo(string Campo, string Anterior, string Nuevo);
}
