using CardLint.Core;
using CardLint.Core.Analizadores;

namespace CardLint.Cli;

/// <summary>El comando, separado de <c>Main</c> para poder probarlo.</summary>
public static class Programa
{
    public const int GatePasa = 0;
    public const int GateFalla = 1;
    public const int ErrorDeUso = 2;

    private const string Uso = "Uso: card-lint analizar <carpeta> [--salida <informe.html>]";
    private const string SalidaPorDefecto = "card-lint-informe.html";

    /// <param name="herramientas">Dónde están Cppcheck y PMD; si no se da, se buscan.</param>
    public static int Ejecutar(string[] args, TextWriter salida, TextWriter error, Herramientas? herramientas = null)
    {
        if (args.Length < 2 || args[0] != "analizar")
        {
            error.WriteLine(Uso);
            return ErrorDeUso;
        }

        var carpeta = Path.GetFullPath(args[1]);
        var informe = SalidaPorDefecto;
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--salida" && i + 1 < args.Length)
            {
                informe = args[++i];
            }
            else
            {
                error.WriteLine($"Opción desconocida: {args[i]}");
                error.WriteLine(Uso);
                return ErrorDeUso;
            }
        }

        if (!Directory.Exists(carpeta))
        {
            error.WriteLine($"No existe la carpeta: {carpeta}");
            return ErrorDeUso;
        }

        informe = Path.GetFullPath(informe);
        var trabajo = Path.Combine(Path.GetDirectoryName(informe)!, $".card-lint-trabajo-{Guid.NewGuid():N}");
        ResultadoAnalisis resultado;
        try
        {
            var configuracion = Configuracion.Leer(carpeta);
            var (analizadores, notas) = Preparar(herramientas ?? Herramientas.Buscar(), configuracion);
            Directory.CreateDirectory(trabajo);
            resultado = Analisis.Ejecutar(carpeta, configuracion, analizadores, trabajo, notas);
        }
        catch (InvalidOperationException e)
        {
            error.WriteLine(e.Message);
            return ErrorDeUso;
        }
        finally
        {
            if (Directory.Exists(trabajo))
            {
                Directory.Delete(trabajo, recursive: true);
            }
        }

        File.WriteAllText(informe, InformeHtml.Generar(resultado, DateTimeOffset.Now));

        foreach (var nota in resultado.Notas ?? [])
        {
            salida.WriteLine(nota);
        }

        salida.WriteLine($"Avisos sin justificar: {resultado.Avisos.Count} · justificados: {resultado.Justificados.Count} · " +
                         $"contramedidas: {resultado.Contramedidas.Count}");
        salida.WriteLine(resultado.GatePasa ? "Quality gate: pasa." : "Quality gate: no pasa.");
        salida.WriteLine($"Informe: {informe}");
        return resultado.GatePasa ? GatePasa : GateFalla;
    }

    private static (List<IAnalizador> Analizadores, List<string> Notas) Preparar(Herramientas herramientas, Configuracion configuracion)
    {
        var analizadores = new List<IAnalizador>();
        var notas = new List<string>();

        if (herramientas.Cppcheck is { } cppcheck)
        {
            analizadores.Add(new CppcheckAnalizador(cppcheck, configuracion.UmbralesEfectivos));
        }
        else
        {
            notas.Add("No se ha encontrado Cppcheck: el código C no se ha analizado.");
        }

        if (herramientas.Pmd is { } pmd)
        {
            analizadores.Add(new PmdAnalizador(pmd));
            analizadores.Add(new CpdAnalizador(pmd, configuracion.MinimoTokensDuplicado));
        }
        else
        {
            notas.Add("No se ha encontrado PMD: ni el Java Card ni la duplicación se han analizado.");
        }

        return (analizadores, notas);
    }
}
