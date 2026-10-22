using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios.Integraciones;

namespace Kryon.Core.Usuarios;

/// <summary>
/// Caso de uso de registrar un usuario en la empresa actual (US4; FR-023 a FR-028, FR-043). Todo ocurre en una
/// transacción: validar los datos (T078) → formato y unicidad del identificador (DEP-3) → límite de usuarios (DEP-5,
/// FR-028) → estado inicial (DEP-2) → crear y guardar con la empresa del contexto → credencial inicial (DEP-2) →
/// auditoría <c>Alta</c> → confirmar.
/// </summary>
/// <remarks>
/// Las decisiones externas (estado inicial, credencial, formato y ámbito de la unicidad del identificador, existencia y
/// cifra del límite) las toman los puntos de integración; este caso de uso no tiene ningún valor sustituto para ellas.
/// La empresa y el actor salen solo del <see cref="IContextoSolicitud"/> verificado; el permiso lo comprueba la política
/// del endpoint. Si cualquier paso falla, la transacción se revierte y no queda ni el usuario ni su auditoría.
/// </remarks>
public sealed class RegistrarUsuario(
    IUnidadDeTrabajo unidadDeTrabajo,
    IRepositorioUsuarios repositorio,
    IRegistroAuditoria auditoria,
    ValidadorUsuario validador,
    IPoliticaAltaUsuario politicaAlta,
    IPoliticaIdentificadorAcceso politicaIdentificador,
    IFuenteLimiteUsuarios fuenteLimite,
    IContextoSolicitud contexto,
    TimeProvider reloj)
{
    /// <summary>Registra un usuario en la empresa actual.</summary>
    /// <param name="nombreCompleto">Nombre completo; se recortan los espacios.</param>
    /// <param name="identificadorAcceso">Identificador de acceso; se guarda tal cual lo acepte DEP-3.</param>
    /// <param name="rolId">Rol; debe ser de la empresa actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario creado, con su versión.</returns>
    /// <exception cref="ErrorUsuarios">
    /// <c>validacion</c>, <c>rol-no-valido</c>, <c>identificador-en-uso</c> o <c>limite-usuarios-alcanzado</c>.
    /// </exception>
    public async Task<Usuario> RegistrarAsync(
        string nombreCompleto,
        string identificadorAcceso,
        Guid rolId,
        CancellationToken cancellationToken)
    {
        await unidadDeTrabajo.IniciarAsync(cancellationToken);
        var confirmando = false;
        try
        {
            var datos = await validador.ValidarAsync(nombreCompleto, identificadorAcceso, incluyeIdentificador: true, rolId, cancellationToken);
            var identificador = datos.IdentificadorAcceso!;

            // Formato (DEP-3): los mensajes de la política se muestran tal cual junto al campo.
            var problemasDeFormato = politicaIdentificador.ValidarFormato(identificador);
            if (problemasDeFormato.Count > 0)
            {
                throw new ErrorUsuarios(
                    CodigoErrorUsuarios.Validacion,
                    "El identificador de acceso no tiene un formato válido.",
                    new Dictionary<string, IReadOnlyList<string>> { [ValidadorUsuario.CampoIdentificadorAcceso] = problemasDeFormato });
            }

            // Unicidad (DEP-3): la política solo responde sí o no, así que el error no puede revelar dónde está en uso.
            if (await politicaIdentificador.EstaEnUsoAsync(identificador, cancellationToken))
            {
                throw new ErrorUsuarios(CodigoErrorUsuarios.IdentificadorEnUso, "El identificador de acceso no está disponible.");
            }

            // Límite (DEP-5): solo se rechaza si existe un límite aplicable y se alcanzó (FR-028, EC-12).
            if (await fuenteLimite.ConsultarAsync(cancellationToken) == SituacionLimiteUsuarios.LimiteAlcanzado)
            {
                throw new ErrorUsuarios(CodigoErrorUsuarios.LimiteUsuariosAlcanzado, "La empresa alcanzó su límite de usuarios.");
            }

            var estadoInicial = await politicaAlta.ObtenerEstadoInicialAsync(
                new DatosAltaUsuario(datos.NombreCompleto, identificador, datos.Rol.Id),
                cancellationToken);

            var ahora = reloj.GetUtcNow();
            var usuario = new Usuario(contexto.EmpresaId, datos.NombreCompleto, identificador, datos.Rol.Id, estadoInicial, ahora);
            await repositorio.AgregarAsync(usuario, cancellationToken);

            // La credencial inicial la gestiona DEP-2 dentro de la misma transacción del alta.
            await politicaAlta.GestionarCredencialInicialAsync(usuario, cancellationToken);

            await auditoria.RegistrarAsync(
                new AuditoriaUsuario(
                    empresaId: contexto.EmpresaId,
                    actorUsuarioId: contexto.UsuarioId,
                    ocurridoEn: ahora,
                    tipoOperacion: TipoOperacion.Alta,
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
