using System.Xml.Linq;

namespace CardLint.Core.Analizadores;

/// <summary>Código duplicado en el C y en el Java Card, con CPD, el detector de copias de PMD.</summary>
public sealed class CpdAnalizador(string pmd, int minimoTokens) : IAnalizador
{
    public string Nombre => "CPD";

    public IEnumerable<Aviso> Analizar(string carpeta, string trabajo)
    {
        var avisos = new List<Aviso>();
        foreach (var (lenguaje, carpetas) in new[]
                 {
                     ("cpp", new[] { Path.Combine(carpeta, "c", "src"), Path.Combine(carpeta, "c", "include") }),
                     ("java", new[] { Path.Combine(carpeta, "javacard", "src") }),
                 })
        {
            var existentes = carpetas.Where(Directory.Exists).ToList();
            if (existentes.Count == 0)
            {
                continue;
            }

            var xml = Path.Combine(trabajo, $"cpd-{lenguaje}.xml");
            var argumentos = new List<string> { "cpd", "--language", lenguaje, "--minimum-tokens", $"{minimoTokens}", "--format", "xml", "-r", xml, "--no-fail-on-violation" };
            foreach (var c in existentes)
            {
                argumentos.AddRange(["-d", c]);
            }

            var (codigo, salida, error) = Proceso.Ejecutar(pmd, argumentos, trabajo);
            if (codigo != 0 || !File.Exists(xml))
            {
                throw new InvalidOperationException($"CPD terminó con el código {codigo}: {(error + salida).Trim()}");
            }

            avisos.AddRange(Leer(File.ReadAllText(xml), carpeta));
        }

        return avisos;
    }

    /// <summary>Un aviso por cada aparición de cada duplicado, que dice dónde está la otra.</summary>
    public static IReadOnlyList<Aviso> Leer(string xml, string carpeta)
    {
        var documento = XDocument.Parse(xml);
        var ns = documento.Root!.Name.Namespace;
        var avisos = new List<Aviso>();

        foreach (var duplicado in documento.Root.Elements(ns + "duplication"))
        {
            var lineas = (int?)duplicado.Attribute("lines") ?? 0;
            var tokens = (int?)duplicado.Attribute("tokens") ?? 0;
            var apariciones = duplicado.Elements(ns + "file")
                .Select(f => (Fichero: Relativa(carpeta, (string?)f.Attribute("path") ?? ""), Linea: (int?)f.Attribute("line") ?? 0))
                .ToList();

            foreach (var aparicion in apariciones)
            {
                var otras = string.Join(", ", apariciones.Where(a => a != aparicion).Select(a => $"{a.Fichero}:{a.Linea}"));
                avisos.Add(new Aviso(
                    "cpd:duplicado",
                    Gravedad.Advertencia,
                    aparicion.Fichero,
                    aparicion.Linea,
                    $"Código duplicado: {lineas} líneas ({tokens} tokens), igual que en {otras}",
                    "1.2, 5.1"));
            }
        }

        return avisos;
    }

    private static string Relativa(string carpeta, string ruta) =>
        Path.IsPathRooted(ruta) ? Rutas.Relativa(carpeta, ruta) : Rutas.Normalizar(ruta);
}
