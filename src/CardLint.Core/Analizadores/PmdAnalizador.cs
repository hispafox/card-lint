using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CardLint.Core.Analizadores;

/// <summary>El Java Card de <c>javacard/src</c>, con PMD y el conjunto de reglas de card-lint.</summary>
public sealed partial class PmdAnalizador(string pmd) : IAnalizador
{
    public static string ConjuntoDeReglas => Path.Combine(AppContext.BaseDirectory, "Reglas", "pmd-javacard.xml");

    public string Nombre => "PMD";

    public IEnumerable<Aviso> Analizar(string carpeta, string trabajo)
    {
        var fuentes = Path.Combine(carpeta, "javacard", "src");
        if (!Directory.Exists(fuentes))
        {
            return [];
        }

        var xml = Path.Combine(trabajo, "pmd.xml");
        var (codigo, salida, error) = Proceso.Ejecutar(pmd,
        [
            "check", "-d", fuentes, "-R", ConjuntoDeReglas, "-f", "xml", "-r", xml, "-z", carpeta,
            "--no-progress", "--no-cache", "--no-fail-on-violation", "--no-fail-on-error",
        ], trabajo);
        if (codigo != 0 || !File.Exists(xml))
        {
            throw new InvalidOperationException($"PMD terminó con el código {codigo}: {(error + salida).Trim()}");
        }

        return Leer(File.ReadAllText(xml));
    }

    /// <summary>Convierte el informe XML de PMD en avisos.</summary>
    public static IReadOnlyList<Aviso> Leer(string xml)
    {
        var documento = XDocument.Parse(xml);
        var ns = documento.Root!.Name.Namespace;
        var avisos = new List<Aviso>();

        foreach (var fichero in documento.Root.Elements(ns + "file"))
        {
            var ruta = Rutas.Normalizar((string?)fichero.Attribute("name") ?? "");
            foreach (var violacion in fichero.Elements(ns + "violation"))
            {
                var regla = (string?)violacion.Attribute("rule") ?? "";
                var original = violacion.Value.Trim();
                var (mensaje, submodulo) = Traducir(regla, original);
                avisos.Add(new Aviso(
                    $"pmd:{regla}",
                    Gravedad_((int?)violacion.Attribute("priority") ?? 3),
                    ruta,
                    (int?)violacion.Attribute("beginline") ?? 0,
                    mensaje,
                    submodulo,
                    regla.StartsWith("JavaCard", StringComparison.Ordinal) ? null : original));
            }
        }

        foreach (var error in documento.Root.Elements(ns + "error"))
        {
            avisos.Add(new Aviso(
                "pmd:error",
                Gravedad.Informacion,
                Rutas.Normalizar((string?)error.Attribute("filename") ?? ""),
                0,
                "PMD no ha podido analizar este fichero",
                null,
                (string?)error.Attribute("msg")));
        }

        return avisos;
    }

    /// <summary>Prioridades de PMD: 1 y 2 son fallo, 3 advertencia, 4 y 5 información.</summary>
    private static Gravedad Gravedad_(int prioridad) => prioridad switch
    {
        <= 2 => Gravedad.Fallo,
        3 => Gravedad.Advertencia,
        _ => Gravedad.Informacion,
    };

    private static (string Mensaje, string? Submodulo) Traducir(string regla, string original)
    {
        var nombre = EntreComillas().Match(original) is { Success: true } m ? m.Groups[1].Value : "";
        var numeros = Numeros().Matches(original).Select(n => n.Value).ToList();
        var primero = numeros.FirstOrDefault() ?? "?";

        return regla switch
        {
            "CognitiveComplexity" when numeros.Count >= 2 => ($"Complejidad cognitiva de {nombre}: {numeros[0]} (umbral {numeros[1]})", "1.2"),
            "CognitiveComplexity" => ($"Complejidad cognitiva de {nombre}: {primero}", "1.2"),
            "CyclomaticComplexity" => ($"Complejidad ciclomática de {nombre}: {primero}", "1.2"),
            "NcssCount" => ($"{nombre} tiene {primero} instrucciones (NCSS)", "1.2, 2.2"),
            "ExcessiveParameterList" => ("Demasiados parámetros", "2.2"),
            "TooManyMethods" => ("La clase tiene demasiados métodos", "4.2"),
            "TooManyFields" => ("La clase tiene demasiados campos", "4.2"),
            "SingularField" => ($"El campo {nombre} solo lo usa un método: puede ser local o, si es un dato de trabajo, ir en un array transitorio", "4.3"),
            "UnusedPrivateField" => ($"Campo privado sin usar: {nombre}", Olores.CodigoMuerto),
            "UnusedPrivateMethod" => ($"Método privado sin usar: {nombre}", Olores.CodigoMuerto),
            "UnusedLocalVariable" => ($"Variable local sin usar: {nombre}", Olores.CodigoMuerto),
            "UnusedFormalParameter" => ($"Parámetro sin usar: {nombre}", Olores.CodigoMuerto),

            // Las propias: el mensaje se pone aquí, porque Java en Windows lee el XML de reglas con otra
            // codificación y estropea las tildes del que lleva el conjunto de reglas.
            "JavaCardTipoNoDisponible" => ("Tipo que Java Card no tiene: usa short o byte", "4.1"),
            "JavaCardInt" => ("int es opcional en Java Card y ocupa el doble que short: usa short o byte", "4.1"),
            "JavaCardCadena" => ("Java Card no tiene cadenas: los textos van como arrays de bytes", "4.1"),
            "JavaCardBibliotecaEstandar" => ("Java Card no tiene la biblioteca estándar de Java (java.util, java.io)", "4.1"),
            "JavaCardObjetoFueraDeInstalar" => ("Objeto creado fuera de la instalación: en Java Card se crean en el constructor o en install", "4.1, 4.2"),
            "JavaCardCodigoComoNumero" => ("Código de estado o de instrucción escrito como número: dale nombre con una constante (ISO7816 o static final)", "4.4"),
            "JavaCardCadenaDeMensajes" => ("Cadena de tres llamadas o más: quien la escribe tiene que conocer todos los objetos del camino", Olores.CadenasDeMensajes),
            _ => (original, null),
        };
    }

    [GeneratedRegex("'([^']+)'")]
    private static partial Regex EntreComillas();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numeros();
}
