namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>Rutas del repositorio y de la salida compilada de las aplicaciones que el entorno E2E ejecuta como procesos.</summary>
internal static class Rutas
{
    /// <summary>Raíz del repositorio: la carpeta que contiene <c>Kryon.sln</c>, buscada hacia arriba desde estas pruebas.</summary>
    public static string Raiz { get; } = BuscarRaiz();

    /// <summary>
    /// Configuración con la que se compilaron estas pruebas (<c>bin/&lt;configuración&gt;/net10.0/</c>); las aplicaciones
    /// se ejecutan desde la salida de esa misma configuración.
    /// </summary>
    public static string Configuracion { get; } =
        new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Parent!.Name;

    /// <summary>Archivo compilado de un proyecto de la solución; falla con un mensaje claro si no se compiló.</summary>
    /// <param name="proyecto">Carpeta del proyecto relativa a la raíz, por ejemplo <c>src/Kryon.Api</c>.</param>
    /// <param name="archivo">Archivo dentro de su salida, por ejemplo <c>Kryon.Api.dll</c>.</param>
    /// <param name="configuracion">Configuración de compilación; por defecto, la de estas pruebas.</param>
    /// <param name="comoCompilar">Comando que genera esa salida, para el mensaje de error.</param>
    public static string SalidaCompilada(string proyecto, string archivo, string? configuracion = null, string? comoCompilar = null)
    {
        configuracion ??= Configuracion;
        var ruta = Path.Combine(Raiz, proyecto, "bin", configuracion, "net10.0", archivo);
        if (!File.Exists(ruta))
        {
            throw new InvalidOperationException(
                $"No existe '{ruta}'. Genera esa salida antes de ejecutar las pruebas E2E " +
                $"({comoCompilar ?? $"dotnet build Kryon.sln --configuration {configuracion}"}): el entorno E2E ejecuta las " +
                "aplicaciones ya compiladas, no las compila.");
        }

        return ruta;
    }

    private static string BuscarRaiz()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "Kryon.sln")))
            {
                return carpeta.FullName;
            }
        }

        throw new InvalidOperationException($"No se encontró Kryon.sln por encima de '{AppContext.BaseDirectory}'.");
    }
}
