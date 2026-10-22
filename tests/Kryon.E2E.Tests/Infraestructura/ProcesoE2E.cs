using System.Collections.Concurrent;
using System.Diagnostics;

namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>
/// Proceso real de una aplicación (API o interfaz) durante las pruebas E2E: lo inicia con su entorno, captura su salida
/// estándar y de error, espera a que responda por HTTP y, al liberarse, lo termina con todo su árbol de procesos para no
/// dejar procesos <c>dotnet</c> huérfanos.
/// </summary>
internal sealed class ProcesoE2E : IAsyncDisposable
{
    private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan EsperaCierre = TimeSpan.FromSeconds(15);

    private readonly Process proceso;
    private readonly ConcurrentQueue<string> salida = new();

    private ProcesoE2E(string nombre, Process proceso)
    {
        Nombre = nombre;
        this.proceso = proceso;
    }

    public string Nombre { get; }

    /// <summary>Salida estándar y de error capturada hasta ahora, en orden de llegada.</summary>
    public string Salida => string.Join(Environment.NewLine, salida);

    public static ProcesoE2E Iniciar(
        string nombre,
        string ejecutable,
        IEnumerable<string> argumentos,
        string carpetaTrabajo,
        IReadOnlyDictionary<string, string> entorno)
    {
        var inicio = new ProcessStartInfo(ejecutable)
        {
            WorkingDirectory = carpetaTrabajo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argumento in argumentos)
        {
            inicio.ArgumentList.Add(argumento);
        }

        foreach (var (clave, valor) in entorno)
        {
            inicio.Environment[clave] = valor;
        }

        var proceso = new Process { StartInfo = inicio, EnableRaisingEvents = true };
        var resultado = new ProcesoE2E(nombre, proceso);
        proceso.OutputDataReceived += (_, e) => resultado.Capturar(e.Data, "out");
        proceso.ErrorDataReceived += (_, e) => resultado.Capturar(e.Data, "err");

        if (!proceso.Start())
        {
            throw new InvalidOperationException($"No se pudo iniciar {nombre} ({ejecutable}).");
        }

        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();
        return resultado;
    }

    /// <summary>
    /// Sondea <paramref name="url"/> con reintentos cortos hasta recibir cualquier respuesta HTTP. Falla con la salida
    /// capturada si el proceso termina antes o si se agota <paramref name="limite"/>.
    /// </summary>
    public async Task EsperarDisponibleAsync(Uri url, TimeSpan limite, CancellationToken cancellationToken)
    {
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var plazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        plazo.CancelAfter(limite);

        while (true)
        {
            if (proceso.HasExited)
            {
                throw new InvalidOperationException(
                    $"{Nombre} terminó (código {proceso.ExitCode}) antes de estar disponible en {url}.\n{Salida}");
            }

            try
            {
                using var respuesta = await cliente.GetAsync(url, plazo.Token);
                return;
            }
            catch (HttpRequestException)
            {
                // Todavía no escucha.
            }
            catch (TaskCanceledException) when (!plazo.IsCancellationRequested)
            {
                // La solicitud superó su propio tiempo: se reintenta.
            }

            try
            {
                await Task.Delay(IntervaloSondeo, plazo.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{Nombre} no respondió en {url} tras {limite.TotalSeconds:0} s.\n{Salida}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!proceso.HasExited)
            {
                proceso.Kill(entireProcessTree: true);
                using var espera = new CancellationTokenSource(EsperaCierre);
                await proceso.WaitForExitAsync(espera.Token);
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso ya terminó.
        }
        catch (OperationCanceledException)
        {
            // No terminó a tiempo tras Kill: se libera igualmente.
        }
        finally
        {
            proceso.Dispose();
        }
    }

    private void Capturar(string? linea, string canal)
    {
        if (linea is not null)
        {
            salida.Enqueue($"[{Nombre} {canal}] {linea}");
        }
    }
}
