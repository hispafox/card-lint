using System.Xml.Linq;

namespace CardLint.Core.Analizadores.C;

internal sealed record TokenC(
    string Id, int Indice, string Fichero, int Linea, string Texto, string Tipo,
    string? Ambito, string? VarId, string? Funcion, string? PadreAst, string? Enlace);

/// <summary>Un ámbito de Cppcheck: Global, Function, If, Else, For, While, Do, Switch, Struct…</summary>
internal sealed record AmbitoC(
    string Id, string Tipo, string? Definicion, string? Inicio, string? Fin, string? Padre, string? Funcion, string? Nombre);

internal sealed record FuncionC(string Id, string Nombre, string? TokenDeclaracion, bool EsStatic, IReadOnlyList<string> Argumentos);

internal sealed record VariableC(
    string Id, string? TokenNombre, string? TipoInicio, string? TipoFin, string Acceso, string? Ambito,
    bool EsStatic, bool EsExtern, bool EsConst, bool EsPuntero, bool EsArray = false);

internal sealed record DirectivaC(string Fichero, int Linea, string Texto);

internal sealed record TokenCrudo(string Fichero, int Linea, string Texto);

/// <summary>
/// El volcado (<c>--dump</c>) que Cppcheck deja de un fichero C: los tokens ya preprocesados,
/// los ámbitos, las funciones, las variables, las directivas y los tokens sin preprocesar.
/// Solo se lee la primera configuración.
/// </summary>
internal sealed class VolcadoC
{
    private VolcadoC(string fuente) => Fuente = fuente;

    /// <summary>El fichero .c del volcado, relativo a la carpeta analizada.</summary>
    public string Fuente { get; }

    public List<TokenC> Tokens { get; } = [];
    public Dictionary<string, TokenC> TokenPorId { get; } = [];
    public Dictionary<string, AmbitoC> Ambitos { get; } = [];
    public Dictionary<string, FuncionC> Funciones { get; } = [];
    public Dictionary<string, VariableC> Variables { get; } = [];
    public List<DirectivaC> Directivas { get; } = [];
    public List<TokenCrudo> Crudos { get; } = [];

    public TokenC? Token(string? id) => id is not null && TokenPorId.TryGetValue(id, out var t) ? t : null;

    public static VolcadoC Leer(string xml, string fuente)
    {
        var volcado = new VolcadoC(Rutas.Normalizar(fuente));
        var raiz = XDocument.Parse(xml).Root!;
        var dump = raiz.Element("dump") ?? raiz;

        foreach (var d in dump.Element("directivelist")?.Elements("directive") ?? [])
        {
            volcado.Directivas.Add(new DirectivaC(Normal(d, "file"), Entero(d, "linenr"), (string?)d.Attribute("str") ?? ""));
        }

        foreach (var t in dump.Element("tokenlist")?.Elements("token") ?? [])
        {
            var token = new TokenC(
                (string)t.Attribute("id")!, volcado.Tokens.Count, Normal(t, "file"), Entero(t, "linenr"),
                (string?)t.Attribute("str") ?? "", (string?)t.Attribute("type") ?? "",
                (string?)t.Attribute("scope"), (string?)t.Attribute("varId"), (string?)t.Attribute("function"),
                (string?)t.Attribute("astParent"), (string?)t.Attribute("link"));
            volcado.Tokens.Add(token);
            volcado.TokenPorId[token.Id] = token;
        }

        foreach (var s in dump.Element("scopes")?.Elements("scope") ?? [])
        {
            var ambito = new AmbitoC(
                (string)s.Attribute("id")!, (string?)s.Attribute("type") ?? "", (string?)s.Attribute("classDef"),
                (string?)s.Attribute("bodyStart"), (string?)s.Attribute("bodyEnd"), (string?)s.Attribute("nestedIn"),
                (string?)s.Attribute("function"), (string?)s.Attribute("className"));
            volcado.Ambitos[ambito.Id] = ambito;

            foreach (var f in s.Element("functionList")?.Elements("function") ?? [])
            {
                var funcion = new FuncionC(
                    (string)f.Attribute("id")!, (string?)f.Attribute("name") ?? "", (string?)f.Attribute("tokenDef"),
                    (string?)f.Attribute("isStatic") == "true",
                    f.Elements("arg").OrderBy(a => Entero(a, "nr")).Select(a => (string?)a.Attribute("variable") ?? "").ToList());
                volcado.Funciones[funcion.Id] = funcion;
            }
        }

        foreach (var v in dump.Element("variables")?.Elements("var") ?? [])
        {
            var variable = new VariableC(
                (string)v.Attribute("id")!, (string?)v.Attribute("nameToken"), (string?)v.Attribute("typeStartToken"),
                (string?)v.Attribute("typeEndToken"), (string?)v.Attribute("access") ?? "", (string?)v.Attribute("scope"),
                (string?)v.Attribute("isStatic") == "true", (string?)v.Attribute("isExtern") == "true",
                (string?)v.Attribute("isConst") == "true", (string?)v.Attribute("isPointer") == "true",
                (string?)v.Attribute("isArray") == "true");
            volcado.Variables[variable.Id] = variable;
        }

        var crudos = raiz.Descendants("rawtokens").FirstOrDefault();
        if (crudos is not null)
        {
            var ficheros = crudos.Elements("file").ToDictionary(f => (string?)f.Attribute("index") ?? "", f => Rutas.Normalizar((string?)f.Attribute("name") ?? ""));
            foreach (var t in crudos.Elements("tok"))
            {
                var fichero = ficheros.GetValueOrDefault((string?)t.Attribute("fileIndex") ?? "", "");
                volcado.Crudos.Add(new TokenCrudo(fichero, Entero(t, "linenr"), (string?)t.Attribute("str") ?? ""));
            }
        }

        return volcado;
    }

    /// <summary>Los tokens de entre las llaves de un ámbito, sin ellas.</summary>
    public IEnumerable<TokenC> Cuerpo(AmbitoC ambito)
    {
        var inicio = Token(ambito.Inicio);
        var fin = Token(ambito.Fin);
        if (inicio is null || fin is null)
        {
            yield break;
        }

        for (var i = inicio.Indice + 1; i < fin.Indice; i++)
        {
            yield return Tokens[i];
        }
    }

    /// <summary>El texto de un tipo, de su primer token al último.</summary>
    public string Texto(string? desde, string? hasta)
    {
        var inicio = Token(desde);
        var fin = Token(hasta);
        if (inicio is null || fin is null || fin.Indice < inicio.Indice)
        {
            return "";
        }

        return string.Join(" ", Tokens.Skip(inicio.Indice).Take(fin.Indice - inicio.Indice + 1).Select(t => t.Texto));
    }

    /// <summary>Las funciones con cuerpo en el fichero del volcado.</summary>
    public IEnumerable<(AmbitoC Ambito, FuncionC Funcion, TokenC Definicion)> FuncionesDefinidas()
    {
        foreach (var ambito in Ambitos.Values.Where(a => a.Tipo == "Function"))
        {
            if (ambito.Funcion is not null && Funciones.TryGetValue(ambito.Funcion, out var funcion)
                && Token(ambito.Definicion) is { } definicion && definicion.Fichero == Fuente)
            {
                yield return (ambito, funcion, definicion);
            }
        }
    }

    private static string Normal(XElement e, string atributo) => Rutas.Normalizar((string?)e.Attribute(atributo) ?? "");

    private static int Entero(XElement e, string atributo) => int.TryParse((string?)e.Attribute(atributo), out var n) ? n : 0;
}
