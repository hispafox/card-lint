using System.Diagnostics;
using System.Text;

namespace CardLint.Core;

/// <summary>
/// Dónde están Cppcheck y PMD. Se buscan en la carpeta <c>herramientas</c>: la de la variable
/// <c>CARDLINT_HERRAMIENTAS</c> o, si no está, la primera que aparezca subiendo desde el ejecutable.
/// </summary>
public sealed record Herramientas(string? Cppcheck, string? Pmd)
{
    public const string Variable = "CARDLINT_HERRAMIENTAS";

    public static Herramientas Buscar()
    {
        var carpeta = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(carpeta))
        {
            carpeta = BuscarHaciaArriba(AppContext.BaseDirectory);
        }

        return carpeta is null || !Directory.Exists(carpeta)
            ? new Herramientas(null, null)
            : new Herramientas(BuscarCppcheck(carpeta), BuscarPmd(carpeta));
    }

    private static string? BuscarHaciaArriba(string desde)
    {
        for (var dir = new DirectoryInfo(desde); dir is not null; dir = dir.Parent)
        {
            var candidata = Path.Combine(dir.FullName, "herramientas");
            if (Directory.Exists(candidata))
            {
                return candidata;
            }
        }

        return null;
    }

    private static string? BuscarCppcheck(string carpeta)
    {
        var nombre = OperatingSystem.IsWindows() ? "cppcheck.exe" : "cppcheck";
        return Directory.EnumerateFiles(carpeta, nombre, SearchOption.AllDirectories).Order(StringComparer.Ordinal).FirstOrDefault();
    }

    private static string? BuscarPmd(string carpeta)
    {
        var nombre = OperatingSystem.IsWindows() ? "pmd.bat" : "pmd";
        return Directory.EnumerateDirectories(carpeta, "pmd-bin-*")
            .Order(StringComparer.Ordinal)
            .Select(d => Path.Combine(d, "bin", nombre))
            .LastOrDefault(File.Exists);
    }
}

/// <summary>Lanza una herramienta externa y espera a que acabe.</summary>
internal static class Proceso
{
    public static (int Codigo, string Salida, string Error) Ejecutar(string programa, IEnumerable<string> argumentos, string directorio)
    {
        var info = new ProcessStartInfo(programa)
        {
            WorkingDirectory = directorio,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (var argumento in argumentos)
        {
            info.ArgumentList.Add(argumento);
        }

        using var proceso = Process.Start(info) ?? throw new InvalidOperationException($"No se pudo lanzar {programa}");
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var error = proceso.StandardError.ReadToEndAsync();
        proceso.WaitForExit();
        return (proceso.ExitCode, salida.Result, error.Result);
    }
}
