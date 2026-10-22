namespace Kryon.Core.Usuarios.Integraciones;

/// <summary>
/// Punto de integración de DEP-2 (futura spec de autenticación). El registro de usuarios le pide el estado inicial
/// y le delega la credencial inicial, dentro de la transacción del alta.
/// </summary>
/// <remarks>
/// Esta feature no decide el estado inicial, el mecanismo de credencial ni si existen estados adicionales, y no
/// tiene ningún valor sustituto: sin una implementación registrada, el registro de usuarios no se entrega.
/// </remarks>
public interface IPoliticaAltaUsuario
{
    /// <summary>Devuelve el estado con el que se crea el usuario.</summary>
    /// <param name="datos">Datos del alta ya validados. La empresa no viaja aquí: es siempre la del contexto verificado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<EstadoUsuario> ObtenerEstadoInicialAsync(DatosAltaUsuario datos, CancellationToken cancellationToken);

    /// <summary>
    /// Gestiona la credencial inicial del usuario recién creado. Cómo se genera o se entrega lo decide DEP-2.
    /// </summary>
    /// <param name="usuario">Usuario creado en la empresa del contexto.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task GestionarCredencialInicialAsync(Usuario usuario, CancellationToken cancellationToken);
}

/// <summary>Datos de un alta de usuario, sin empresa: la empresa sale siempre del contexto verificado.</summary>
/// <param name="NombreCompleto">Nombre completo, ya normalizado.</param>
/// <param name="IdentificadorAcceso">Identificador de acceso.</param>
/// <param name="RolId">Rol de la empresa actual.</param>
public sealed record DatosAltaUsuario(string NombreCompleto, string IdentificadorAcceso, Guid RolId);
