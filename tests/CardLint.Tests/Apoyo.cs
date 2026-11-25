using System.Diagnostics;
using System.Text;
using CardLint.Core;

namespace CardLint.Tests;

/// <summary>
/// Carpeta de trabajo para una prueba, dentro de la salida de compilación (en F:, dentro del
/// repositorio y fuera de git). Se borra al terminar.
/// </summary>
internal sealed class CarpetaTemporal : IDisposable
{
    public CarpetaTemporal()
    {
        Ruta = Path.Combine(AppContext.BaseDirectory, "tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Ruta { get; }

    public string Escribir(string relativa, string contenido)
    {
        var ruta = Path.Combine(Ruta, relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    public void Dispose() => Directory.Delete(Ruta, recursive: true);
}

/// <summary>Un analizador de mentira que devuelve los avisos que se le dan.</summary>
internal sealed class AnalizadorFijo(params Aviso[] avisos) : IAnalizador
{
    public string Nombre => "fijo";

    public IEnumerable<Aviso> Analizar(string carpeta, string trabajo) => avisos;
}

/// <summary>Las salidas reales de las herramientas guardadas en <c>datos/</c>.</summary>
internal static class Datos
{
    public static string Leer(string nombre) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "datos", nombre));
}

/// <summary>Prueba que necesita Cppcheck y PMD de verdad, además de card-code.</summary>
internal sealed class ConHerramientasFactAttribute : FactAttribute
{
    public static Herramientas Herramientas { get; } = Herramientas.Buscar();

    public ConHerramientasFactAttribute() => Skip = PorQueSeSalta();

    /// <summary>Por qué se salta una prueba con herramientas, o null si se puede ejecutar.</summary>
    public static string? PorQueSeSalta() =>
        Herramientas.Cppcheck is null || Herramientas.Pmd is null
            ? $"No se encuentran Cppcheck y PMD: ponlos en herramientas/ o define {Herramientas.Variable}."
            : CardCode.Ruta is null
                ? "No se encuentra card-code: ponlo al lado de card-lint o define CARDLINT_CARDCODE con su ruta."
                : null;
}

/// <summary>La versión con datos de <see cref="ConHerramientasFactAttribute"/>.</summary>
internal sealed class ConHerramientasTheoryAttribute : TheoryAttribute
{
    public ConHerramientasTheoryAttribute() => Skip = ConHerramientasFactAttribute.PorQueSeSalta();
}

/// <summary>
/// card-code, el repositorio de ejemplo del curso. Se busca en <c>CARDLINT_CARDCODE</c> o al lado
/// de card-lint. Solo se lee, con <c>git show</c>.
/// </summary>
internal static class CardCode
{
    public static string? Ruta { get; } = Buscar();

    /// <summary>Saca las carpetas indicadas de una rama dentro de <paramref name="destino"/>, con <c>git archive</c>.</summary>
    public static void Extraer(string rama, string destino, params string[] carpetas)
    {
        var zip = Path.Combine(destino, "rama.zip");
        var git = new ProcessStartInfo("git", ["-C", Ruta!, "archive", "--format=zip", "-o", zip, rama, .. carpetas])
        {
            RedirectStandardError = true,
        };
        using (var proceso = Process.Start(git)!)
        {
            var error = proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();
            if (proceso.ExitCode != 0)
            {
                throw new InvalidOperationException($"git archive {rama} falló: {error}");
            }
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zip, destino);
        File.Delete(zip);
    }

    public static string Leer(string rama, string fichero)
    {
        var git = new ProcessStartInfo("git", ["-C", Ruta!, "show", $"{rama}:{fichero}"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var proceso = Process.Start(git)!;
        var salida = proceso.StandardOutput.ReadToEnd();
        var error = proceso.StandardError.ReadToEnd();
        proceso.WaitForExit();
        if (proceso.ExitCode != 0)
        {
            throw new InvalidOperationException($"git show {rama}:{fichero} falló: {error}");
        }

        return salida;
    }

    private static string? Buscar()
    {
        var variable = Environment.GetEnvironmentVariable("CARDLINT_CARDCODE");
        if (!string.IsNullOrEmpty(variable))
        {
            return Directory.Exists(Path.Combine(variable, ".git")) ? variable : null;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "card-lint.slnx")))
            {
                var hermano = Path.Combine(dir.Parent?.FullName ?? "", "card-code");
                return Directory.Exists(Path.Combine(hermano, ".git")) ? hermano : null;
            }
        }

        return null;
    }
}

/// <summary>Prueba que necesita card-code; se salta, diciendo por qué, si no lo encuentra.</summary>
internal sealed class ConCardCodeFactAttribute : FactAttribute
{
    public ConCardCodeFactAttribute()
    {
        if (CardCode.Ruta is null)
        {
            Skip = "No se encuentra card-code: ponlo al lado de card-lint o define CARDLINT_CARDCODE con su ruta.";
        }
    }
}
