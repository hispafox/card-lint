using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CardLint.Core.Analizadores;

/// <summary>El Java Card de <c>javacard/src</c>, con PMD y el conjunto de reglas de card-lint.</summary>
public sealed partial class PmdAnalizador(string pmd) : IAnalizador
{
    public static string ConjuntoDeReglas => Path.Combine(AppContext.BaseDirectory, "Reglas", "pmd-javacard.xml");

    // Los umbrales de serie de PMD. PMD avisa desde el propio umbral (una cognitiva de 15 ya avisa), al
    // contrario que las reglas de C, que avisan cuando se pasa. El conjunto de reglas le pide a PMD que
    // mida todos los métodos, y aquí se avisa solo de los que llegan.
    public const int UmbralCognitiva = 15;
    public const int UmbralCiclomatica = 10;
    public const int UmbralInstrucciones = 60;

    private static readonly Dictionary<string, int> UmbralesDeSerie = new()
    {
        ["CognitiveComplexity"] = UmbralCognitiva,
        ["CyclomaticComplexity"] = UmbralCiclomatica,
        ["NcssCount"] = UmbralInstrucciones,
    };

    private IReadOnlyList<MetricasFuncion> funciones = [];

    public string Nombre => "PMD";

    public IReadOnlyList<MetricasFuncion> Funciones => funciones;

    public IEnumerable<Aviso> Analizar(string carpeta, string trabajo)
    {
        funciones = [];
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

        var informe = File.ReadAllText(xml);
        funciones = Medir(informe);
        return Leer(informe);
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
                if (UmbralesDeSerie.TryGetValue(regla, out var umbral) && MedidaDeMetodo(original) is { } medida && medida.Valor < umbral)
                {
                    continue;
                }

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

    /// <summary>
    /// Las métricas de cada método y constructor, del informe de PMD con el conjunto de reglas de card-lint,
    /// que las pide todas. PMD no da la cognitiva de los métodos sin ninguna ruptura del flujo: es 0.
    /// </summary>
    public static IReadOnlyList<MetricasFuncion> Medir(string xml)
    {
        var documento = XDocument.Parse(xml);
        var ns = documento.Root!.Name.Namespace;
        var funciones = new List<MetricasFuncion>();

        foreach (var fichero in documento.Root.Elements(ns + "file"))
        {
            var ruta = Rutas.Normalizar((string?)fichero.Attribute("name") ?? "");
            var medidas = fichero.Elements(ns + "violation")
                .Select(v => (Regla: (string?)v.Attribute("rule") ?? "", Linea: (int?)v.Attribute("beginline") ?? 0, Medida: MedidaDeMetodo(v.Value.Trim())))
                .Where(x => x.Medida is not null && UmbralesDeSerie.ContainsKey(x.Regla))
                .ToList();

            foreach (var metodo in medidas.GroupBy(x => (x.Linea, x.Medida!.Value.Nombre)))
            {
                int? De(string regla) => metodo.FirstOrDefault(x => x.Regla == regla).Medida?.Valor;
                funciones.Add(new MetricasFuncion(MetricasFuncion.JavaCard, ruta, metodo.Key.Linea, metodo.Key.Nombre,
                    Cognitiva: De("CognitiveComplexity") ?? 0,
                    Ciclomatica: De("CyclomaticComplexity"),
                    Instrucciones: De("NcssCount")));
            }
        }

        return funciones;
    }

    /// <summary>El método y su valor, si el mensaje es la medida de un método o un constructor (no de una clase).</summary>
    private static (string Nombre, int Valor)? MedidaDeMetodo(string mensaje) =>
        Medida().Match(mensaje) is { Success: true } m && m.Groups["que"].Value is "method" or "constructor"
            ? (m.Groups["nombre"].Value, int.Parse(m.Groups["valor"].Value, System.Globalization.CultureInfo.InvariantCulture))
            : null;

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
        var valor = Medida().Match(original) is { Success: true } medida ? medida.Groups["valor"].Value : primero;

        return regla switch
        {
            "CognitiveComplexity" => ($"Complejidad cognitiva de {nombre}: {valor} (umbral {UmbralCognitiva})", "1.2"),
            "CyclomaticComplexity" => ($"Complejidad ciclomática de {nombre}: {valor}", "1.2"),
            "NcssCount" => ($"{nombre} tiene {valor} instrucciones (NCSS)", "1.2, 2.2"),
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

    /// <summary>«The method 'process(APDU)' has a cyclomatic complexity of 26.», o «The class 'X' has a total … of 85».</summary>
    [GeneratedRegex(@"^The (?<que>\w+) '(?<nombre>[^']+)' has an? .*? of (?<valor>\d+)")]
    private static partial Regex Medida();
}
