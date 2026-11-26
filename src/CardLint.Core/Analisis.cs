namespace CardLint.Core;

/// <summary>Una fuente de avisos: Cppcheck, PMD, CPD o las reglas propias.</summary>
public interface IAnalizador
{
    string Nombre { get; }

    /// <param name="carpeta">La carpeta analizada. No se escribe nada en ella.</param>
    /// <param name="trabajo">Una carpeta vacía para los ficheros intermedios del analizador.</param>
    IEnumerable<Aviso> Analizar(string carpeta, string trabajo);

    /// <summary>Las métricas de todas las funciones que ha visto el último <see cref="Analizar"/>; vacío si no mide.</summary>
    IReadOnlyList<MetricasFuncion> Funciones => [];
}

public sealed record ResultadoAnalisis(
    string Carpeta,
    IReadOnlyList<string> Analizadores,
    IReadOnlyList<Aviso> Avisos,
    IReadOnlyList<AvisoJustificado> Justificados,
    IReadOnlyList<Contramedida> Contramedidas,
    Gravedad GravedadQueFalla,
    IReadOnlyList<string>? Notas = null,
    IReadOnlyList<MetricasFuncion>? Funciones = null)
{
    /// <summary>Pasa si ningún aviso sin justificar llega a la gravedad que falla.</summary>
    public bool GatePasa => !Avisos.Any(a => a.Gravedad >= GravedadQueFalla);
}

public static class Analisis
{
    private static readonly string[] ExtensionesC = [".c", ".h"];

    /// <param name="trabajo">Carpeta para los ficheros intermedios; cada analizador usa una subcarpeta.</param>
    /// <param name="notas">Lo que el informe tiene que contar aparte, como una herramienta que no se ha encontrado.</param>
    public static ResultadoAnalisis Ejecutar(
        string carpeta,
        Configuracion configuracion,
        IReadOnlyList<IAnalizador> analizadores,
        string trabajo,
        IReadOnlyList<string>? notas = null)
    {
        var contramedidas = LeerContramedidas(carpeta);
        var avisos = new List<Aviso>();
        for (var i = 0; i < analizadores.Count; i++)
        {
            var subcarpeta = Path.Combine(trabajo, $"{i}");
            Directory.CreateDirectory(subcarpeta);
            avisos.AddRange(analizadores[i].Analizar(carpeta, subcarpeta));
        }

        var (sinJustificar, justificados) = LectorContramedidas.Separar(avisos, contramedidas);

        return new ResultadoAnalisis(
            carpeta,
            analizadores.Select(a => a.Nombre).ToList(),
            sinJustificar,
            justificados,
            contramedidas,
            configuracion.GravedadQueFalla,
            notas ?? [],
            analizadores.SelectMany(a => a.Funciones).ToList());
    }

    private static List<Contramedida> LeerContramedidas(string carpeta) =>
        Directory.EnumerateFiles(carpeta, "*", SearchOption.AllDirectories)
            .Where(f => ExtensionesC.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .SelectMany(f => LectorContramedidas.Leer(Rutas.Relativa(carpeta, f), File.ReadAllText(f)))
            .ToList();
}
