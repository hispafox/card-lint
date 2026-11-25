namespace CardLint.Core;

internal static class Rutas
{
    /// <summary>Ruta relativa a <paramref name="carpeta"/>, con «/» como separador.</summary>
    public static string Relativa(string carpeta, string ruta) =>
        Normalizar(Path.GetRelativePath(carpeta, ruta));

    public static string Normalizar(string ruta) => ruta.Replace('\\', '/');

    public static bool Iguales(string a, string b) =>
        string.Equals(Normalizar(a), Normalizar(b), StringComparison.OrdinalIgnoreCase);
}
