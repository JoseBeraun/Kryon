namespace Kryon.Core.Usuarios;

/// <summary>
/// Calcula las acciones que un actor puede ver sobre un usuario (tabla <c>accionesPermitidas</c> de data-model.md)
/// y si es su propia cuenta. Refleja las capacidades del actor para que la interfaz no muestre acciones que la API
/// denegaría; no autoriza por sí misma ninguna operación: cada endpoint aplica su política y cada caso de uso sus
/// reglas al ejecutar.
/// </summary>
public static class CalculadoraAcciones
{
    /// <summary>Calcula las acciones sobre <paramref name="usuario"/>, ya obtenido dentro de la empresa del contexto.</summary>
    /// <param name="usuario">Usuario sobre el que se calculan las acciones.</param>
    /// <param name="actorUsuarioId">Usuario que realiza la solicitud, tomado del contexto verificado.</param>
    /// <param name="capacidades">Capacidades del actor, evaluadas con las mismas políticas que protegen cada endpoint.</param>
    /// <remarks>
    /// La regla del último administrador no oculta <see cref="AccionesUsuario.Desactivar"/>: se comprueba al ejecutar
    /// y se informa del motivo (FR-036). <see cref="AccionesUsuario.EditarIdentificador"/> vale <c>false</c> mientras
    /// el gate <c>EdicionIdentificador</c> esté cerrado, porque esa función no está entregada; no significa que el
    /// identificador no sea editable ni resuelve DEP-4.
    /// </remarks>
    public static AccionesUsuario Calcular(Usuario usuario, Guid actorUsuarioId, CapacidadesActor capacidades)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUsuarioId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(capacidades);

        var esCuentaPropia = usuario.Id == actorUsuarioId;

        return new AccionesUsuario(
            VerDetalle: capacidades.Acceder,
            Editar: capacidades.Editar,
            CambiarRol: capacidades.Editar && !esCuentaPropia,
            EditarIdentificador: false,
            Activar: capacidades.Activar && usuario.Estado == EstadoUsuario.Inactivo,
            Desactivar: capacidades.Desactivar && usuario.Estado == EstadoUsuario.Activo && !esCuentaPropia,
            EsCuentaPropia: esCuentaPropia);
    }
}

/// <summary>
/// Capacidades del actor ya evaluadas por la capa que conoce los nombres de permiso configurables (research §R4).
/// </summary>
/// <param name="Acceder">Acceso a la gestión de usuarios.</param>
/// <param name="Editar">Editar nombre y rol.</param>
/// <param name="Activar">Activar usuarios.</param>
/// <param name="Desactivar">Desactivar usuarios.</param>
public sealed record CapacidadesActor(bool Acceder, bool Editar, bool Activar, bool Desactivar);

/// <summary>Acciones calculadas para un usuario y si es la cuenta del actor.</summary>
/// <param name="VerDetalle">Ver el detalle.</param>
/// <param name="Editar">Editar nombre y rol.</param>
/// <param name="CambiarRol">Cambiar el rol; nunca sobre la propia cuenta (FR-035).</param>
/// <param name="EditarIdentificador">Editar el identificador de acceso; depende de DEP-4.</param>
/// <param name="Activar">Activar un usuario Inactivo.</param>
/// <param name="Desactivar">Desactivar un usuario Activo que no es el actor (FR-034).</param>
/// <param name="EsCuentaPropia">El usuario es el actor (FR-012).</param>
public sealed record AccionesUsuario(
    bool VerDetalle,
    bool Editar,
    bool CambiarRol,
    bool EditarIdentificador,
    bool Activar,
    bool Desactivar,
    bool EsCuentaPropia);
