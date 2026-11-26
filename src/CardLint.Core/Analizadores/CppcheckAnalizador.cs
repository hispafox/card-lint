using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CardLint.Core.Analizadores;

/// <summary>
/// El C de <c>c/</c>, con Cppcheck. Trabaja sobre una copia, porque el volcado de Cppcheck
/// se escribe al lado de cada fuente y la carpeta analizada no se toca.
/// </summary>
public sealed partial class CppcheckAnalizador(string cppcheck, Umbrales? umbrales = null) : IAnalizador
{
    public const string Codigo = "c";

    /// <summary>Mensajes de Cppcheck sobre el propio análisis, que no son avisos del código.</summary>
    private static readonly HashSet<string> Ignorados =
    [
        "checkersReport", "missingInclude", "missingIncludeSystem", "normalCheckLevelMaxBranches",
        "toomanyconfigs", "unmatchedSuppression", "noValidConfiguration",
    ];

    /// <summary>Lo que se dice en español de cada aviso de Cppcheck que se conoce, y dónde se explica.</summary>
    private static readonly Dictionary<string, (string Mensaje, string? Submodulo)> Catalogo = new()
    {
        ["uninitvar"] = ("Variable que se usa sin inicializar", "3.4"),
        ["invalidTestForOverflow"] = ("Comprobación de desbordamiento que el compilador puede quitar", "3.4"),
        ["integerOverflow"] = ("Desbordamiento de un entero con signo", "3.4"),
        ["nullPointer"] = ("Desreferencia de un puntero nulo", "3.4"),
        ["nullPointerRedundantCheck"] = ("Puntero que se usa antes de comprobar si es nulo", "3.4"),
        ["shiftTooManyBits"] = ("Desplazamiento de más bits de los que tiene el tipo", "3.4"),
        ["shiftTooManyBitsSigned"] = ("Desplazamiento de más bits de los que tiene el tipo", "3.4"),
        ["shiftNegative"] = ("Desplazamiento negativo", "3.4"),
        ["missingReturn"] = ("Función que puede acabar sin devolver valor", "3.4"),
        ["arrayIndexOutOfBounds"] = ("Acceso fuera de los límites de un array", "3.2"),
        ["arrayIndexOutOfBoundsCond"] = ("Acceso fuera de los límites de un array", "3.2"),
        ["bufferAccessOutOfBounds"] = ("Acceso fuera de los límites de un búfer", "3.2"),
        ["negativeIndex"] = ("Índice negativo", "3.2"),
        ["memleak"] = ("Memoria reservada que no se libera", "3.2"),
        ["signConversion"] = ("Conversión de un valor negativo a un tipo sin signo", "3.3"),
        ["signConversionCond"] = ("Conversión de un valor negativo a un tipo sin signo", "3.3"),
        ["constParameter"] = ("Parámetro que podría ser const", "3.3"),
        ["constParameterPointer"] = ("Parámetro puntero que podría apuntar a const", "3.3"),
        ["constVariable"] = ("Variable que podría ser const", "3.3"),
        ["constVariablePointer"] = ("Variable puntero que podría apuntar a const", "3.3"),
        ["staticFunction"] = ("Función que no sale del fichero y podría ser static", "2.2"),
        ["duplicateCondition"] = ("Condición repetida: si es a propósito, márcala como CONTRAMEDIDA", "3.5"),
        ["identicalConditionAfterEarlyExit"] = ("Condición repetida: si es a propósito, márcala como CONTRAMEDIDA", "3.5"),
        ["identicalInnerCondition"] = ("Condición repetida: si es a propósito, márcala como CONTRAMEDIDA", "3.5"),
        ["knownConditionTrueFalse"] = ("Condición que siempre da el mismo resultado", "3.5"),
        ["duplicateExpression"] = ("Expresión repetida", null),
        ["redundantAssignment"] = ("Asignación que se pisa antes de usarse", null),
        ["variableScope"] = ("El ámbito de la variable se puede reducir", null),
        ["unusedFunction"] = ("Función sin usar", Olores.CodigoMuerto),
        ["unusedVariable"] = ("Variable sin usar", Olores.CodigoMuerto),
        ["unreadVariable"] = ("Valor asignado que nadie lee", Olores.CodigoMuerto),
        ["unusedStructMember"] = ("Campo de struct sin usar", Olores.CodigoMuerto),
    };

    /// <summary>Los de código muerto suben a advertencia aunque Cppcheck los dé como estilo.</summary>
    private static readonly HashSet<string> CodigoMuerto = ["unusedFunction", "unusedVariable", "unreadVariable", "unusedStructMember"];

    private IReadOnlyList<MetricasFuncion> funciones = [];

    public string Nombre => "Cppcheck y reglas de C";

    public IReadOnlyList<MetricasFuncion> Funciones => funciones;

    public IEnumerable<Aviso> Analizar(string carpeta, string trabajo)
    {
        funciones = [];
        var origen = Path.Combine(carpeta, Codigo);
        if (!Directory.Exists(origen))
        {
            return [];
        }

        CopiarSinPruebas(origen, Path.Combine(trabajo, Codigo));
        var objetivo = Directory.Exists(Path.Combine(trabajo, Codigo, "src")) ? $"{Codigo}/src" : Codigo;
        var xml = Path.Combine(trabajo, "cppcheck.xml");
        var argumentos = new List<string>
        {
            "--enable=warning,style,performance,portability,information,unusedFunction",
            "--check-level=exhaustive", "--std=c99", "--quiet", "--dump",
            "--suppress=missingIncludeSystem", "--xml", "--xml-version=2", $"--output-file={xml}",
        };
        if (Directory.Exists(Path.Combine(trabajo, Codigo, "include")))
        {
            argumentos.Add($"-I{Codigo}/include");
        }

        argumentos.Add(objetivo);

        var (codigo, _, error) = Proceso.Ejecutar(cppcheck, argumentos, trabajo);
        if (codigo != 0 || !File.Exists(xml))
        {
            throw new InvalidOperationException($"Cppcheck terminó con el código {codigo}: {error.Trim()}");
        }

        var pruebas = Path.Combine(origen, "pruebas");
        var cabeceras = Path.Combine(origen, "include");
        var avisos = Leer(File.ReadAllText(xml), nombre => Aparece(pruebas, "*.c", nombre), nombre => Aparece(cabeceras, "*.h", nombre)).ToList();

        var volcados = Directory.EnumerateFiles(Path.Combine(trabajo, Codigo), "*.dump", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(d => C.VolcadoC.Leer(File.ReadAllText(d), Path.GetRelativePath(trabajo, d[..^".dump".Length])))
            .ToList();
        avisos.AddRange(C.ReglasC.Analizar(volcados, umbrales ?? new Umbrales()));
        funciones = C.ReglasC.Medir(volcados);
        return avisos;
    }

    /// <summary>Convierte el XML de Cppcheck en avisos.</summary>
    /// <param name="soloEnPruebas">Si una función aparece en las pruebas.</param>
    /// <param name="enCabecera">Si una función está declarada en una cabecera de <c>c/include</c>.</param>
    public static IReadOnlyList<Aviso> Leer(string xml, Func<string, bool>? soloEnPruebas = null, Func<string, bool>? enCabecera = null)
    {
        var avisos = new List<Aviso>();
        foreach (var error in XDocument.Parse(xml).Descendants("error"))
        {
            var id = (string?)error.Attribute("id") ?? "";
            var ubicacion = error.Element("location");
            if (Ignorados.Contains(id) || ubicacion is null)
            {
                continue;
            }

            var original = (string?)error.Attribute("msg") ?? "";
            var (mensaje, submodulo) = Catalogo.TryGetValue(id, out var conocido) ? conocido : ($"Aviso de Cppcheck ({id})", null);
            var gravedad = CodigoMuerto.Contains(id) ? Gravedad.Advertencia : Gravedad_((string?)error.Attribute("severity"));

            // Una función que está en la cabecera es parte del contrato del módulo (2.3), y una que llaman
            // las pruebas sale del fichero aunque Cppcheck no las vea: ninguna de las dos tiene que ser static.
            if (id == "staticFunction" && NombreEntreComillas(original) is { } usada
                && (soloEnPruebas?.Invoke(usada) == true || enCabecera?.Invoke(usada) == true))
            {
                continue;
            }

            if (id == "unusedFunction" && NombreEntreComillas(original) is { } funcion)
            {
                mensaje = $"Función sin usar: {funcion}";
                if (soloEnPruebas?.Invoke(funcion) == true)
                {
                    mensaje = $"Función que solo usan las pruebas: {funcion}";
                    gravedad = Gravedad.Informacion;
                }
            }

            avisos.Add(new Aviso(
                $"cppcheck:{id}",
                gravedad,
                Rutas.Normalizar((string?)ubicacion.Attribute("file") ?? ""),
                (int?)ubicacion.Attribute("line") ?? 0,
                mensaje,
                submodulo,
                original));
        }

        return avisos;
    }

    private static Gravedad Gravedad_(string? severidad) => severidad switch
    {
        "error" => Gravedad.Fallo,
        "warning" => Gravedad.Advertencia,
        _ => Gravedad.Informacion,
    };

    private static string? NombreEntreComillas(string texto) =>
        EntreComillas().Match(texto) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Si <paramref name="funcion"/> aparece seguida de un paréntesis en algún fichero de la carpeta.</summary>
    private static bool Aparece(string carpeta, string patron, string funcion)
    {
        if (!Directory.Exists(carpeta))
        {
            return false;
        }

        var llamada = new Regex($@"\b{Regex.Escape(funcion)}\s*\(");
        return Directory.EnumerateFiles(carpeta, patron, SearchOption.AllDirectories).Any(f => llamada.IsMatch(File.ReadAllText(f)));
    }

    private static void CopiarSinPruebas(string origen, string destino)
    {
        foreach (var fichero in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
        {
            var relativa = Path.GetRelativePath(origen, fichero);
            if (relativa.Split(Path.DirectorySeparatorChar)[0] == "pruebas")
            {
                continue;
            }

            var copia = Path.Combine(destino, relativa);
            Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
            File.Copy(fichero, copia);
        }
    }

    [GeneratedRegex("'([^']+)'")]
    private static partial Regex EntreComillas();
}
