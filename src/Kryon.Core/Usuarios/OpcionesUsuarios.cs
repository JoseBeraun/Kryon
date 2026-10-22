namespace Kryon.Core.Usuarios;

/// <summary>
/// Opciones de la Gestión de Usuarios (sección <see cref="Seccion"/>). Sus valores son decisiones técnicas
/// <b>provisionales</b> que se cambian por configuración: no son requisitos de negocio y la spec no los fija.
/// Los valores iniciales viven solo en la configuración de la API.
/// </summary>
public sealed class OpcionesUsuarios
{
    public const string Seccion = "Usuarios";

    public OpcionesPaginacionUsuarios Paginacion { get; set; } = new();

    public OpcionesValidacionUsuarios Validacion { get; set; } = new();
}

/// <summary>Paginación del listado en el servidor (FR-018). Valores provisionales y configurables.</summary>
public sealed class OpcionesPaginacionUsuarios
{
    /// <summary>Tamaño de página cuando la solicitud no indica uno.</summary>
    public int TamanoPorDefecto { get; set; }

    /// <summary>Tamaño de página máximo que se acepta.</summary>
    public int TamanoMaximo { get; set; }
}

/// <summary>Topes técnicos de longitud (FR-024, FR-025). Valores provisionales y configurables.</summary>
public sealed class OpcionesValidacionUsuarios
{
    /// <summary>Longitud máxima del nombre completo.</summary>
    public int LongitudMaximaNombre { get; set; }

    /// <summary>Longitud máxima del identificador de acceso. Su formato definitivo depende de DEP-3.</summary>
    public int LongitudMaximaIdentificador { get; set; }
}
