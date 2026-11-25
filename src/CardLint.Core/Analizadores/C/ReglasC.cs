using System.Globalization;
using System.Text.RegularExpressions;

namespace CardLint.Core.Analizadores.C;

/// <summary>
/// Las reglas propias de C, sobre los volcados de Cppcheck: las del curso y los olores del libro
/// que se pueden ver en C. Cada aviso dice dónde se explica.
/// </summary>
internal static partial class ReglasC
{
    private static readonly HashSet<string> Control = ["If", "Else", "For", "While", "Do", "Switch"];
    private static readonly HashSet<string> Decisiones = ["if", "for", "while", "case", "&&", "||", "?"];
    private static readonly HashSet<string> Reserva = ["malloc", "calloc", "realloc", "free"];
    private static readonly HashSet<string> Calificadores = ["static", "inline", "extern", "const", "volatile", "register"];

    public static IReadOnlyList<Aviso> Analizar(IReadOnlyList<VolcadoC> volcados, Umbrales umbrales)
    {
        var avisos = new List<Aviso>();
        foreach (var v in volcados)
        {
            avisos.AddRange(Metricas(v, umbrales));
            avisos.AddRange(Parametros(v));
            avisos.AddRange(Prefijos(v));
            avisos.AddRange(Macros(v));
            avisos.AddRange(AsignacionDinamica(v));
            avisos.AddRange(RetornoSinComprobar(v));
            avisos.AddRange(ModuloGrande(v, umbrales));
            avisos.AddRange(NumerosMagicos(v));
            avisos.AddRange(CampoTemporal(v));
            avisos.AddRange(Extern(v));
            avisos.AddRange(CadenasDeMensajes(v));
        }

        avisos.AddRange(SwitchRepetido(volcados));
        avisos.AddRange(IncludesCiclicos(volcados));
        avisos.AddRange(GruposDeDatos(volcados));

        // Las cabeceras salen en el volcado de cada .c que las incluye: un aviso por sitio.
        return avisos.DistinctBy(a => (a.Regla, a.Fichero, a.Linea, a.Mensaje)).ToList();
    }

    // ---- Métricas (1.2, 2.2, 5.1) -------------------------------------------------------------

    private static IEnumerable<Aviso> Metricas(VolcadoC v, Umbrales u)
    {
        foreach (var (ambito, funcion, definicion) in v.FuncionesDefinidas())
        {
            var nombre = funcion.Nombre;
            var cognitiva = Cognitiva(v, ambito);
            var ciclomatica = Ciclomatica(v, ambito);
            var lineas = Lineas(v, ambito, definicion);

            if (cognitiva > u.Cognitiva)
            {
                yield return Aviso("c:complejidad-cognitiva", Gravedad.Advertencia, definicion,
                    $"Complejidad cognitiva de {nombre}: {cognitiva} (umbral {u.Cognitiva})", "1.2");
            }

            if (ciclomatica > u.Ciclomatica)
            {
                yield return Aviso("c:complejidad-ciclomatica", Gravedad.Advertencia, definicion,
                    $"Complejidad ciclomática de {nombre}: {ciclomatica} (umbral {u.Ciclomatica})", "1.2");
            }

            if (lineas > u.LineasFuncion)
            {
                yield return Aviso("c:funcion-larga", Gravedad.Advertencia, definicion,
                    $"{nombre} tiene {lineas} líneas de código (umbral {u.LineasFuncion})", "1.2, 2.2, 5.1");
            }

            if (funcion.Argumentos.Count > u.Parametros)
            {
                yield return Aviso("c:demasiados-parametros", Gravedad.Advertencia, definicion,
                    $"{nombre} tiene {funcion.Argumentos.Count} parámetros (umbral {u.Parametros})", "2.2");
            }
        }
    }

    /// <summary>
    /// Líneas con código de la función, de la firma a la llave que la cierra, sin las que están en
    /// blanco: así las cuenta la 1.2 (23 en el antes de <c>registro_escribir</c>).
    /// </summary>
    internal static int Lineas(VolcadoC v, AmbitoC ambito, TokenC definicion)
    {
        var hasta = v.Token(ambito.Fin)?.Linea ?? definicion.Linea;
        return v.Crudos.Where(t => t.Fichero == v.Fuente && t.Linea >= definicion.Linea && t.Linea <= hasta)
            .Select(t => t.Linea).Distinct().Count();
    }

    /// <summary>Uno más cada decisión: if, for, while, case, &amp;&amp;, || y el operador ternario.</summary>
    internal static int Ciclomatica(VolcadoC v, AmbitoC ambito) =>
        1 + v.Cuerpo(ambito).Count(t => Decisiones.Contains(t.Texto));

    /// <summary>
    /// La complejidad cognitiva del libro blanco de SonarSource: cada ruptura del flujo suma 1, las
    /// que anidan suman además su nivel, los else y else if no suman nivel, cada secuencia de
    /// operadores lógicos iguales suma 1 y cada goto suma 1. Cppcheck escribe el «else if» como un
    /// else que envuelve un if: ese envoltorio no cuenta ni anida.
    /// </summary>
    internal static int Cognitiva(VolcadoC v, AmbitoC funcion)
    {
        var total = 0;
        foreach (var ambito in v.Ambitos.Values.Where(a => Desciende(v, a, funcion.Id)))
        {
            total += ambito.Tipo switch
            {
                "If" when EsElseIf(v, ambito) => 1,
                "If" or "For" or "While" or "Do" or "Switch" => 1 + Nivel(v, ambito.Padre, funcion.Id),
                "Else" when !EsEnvoltorioElseIf(v, ambito) => 1,
                _ => 0,
            };
        }

        foreach (var t in v.Cuerpo(funcion))
        {
            if (t.Texto == "?")
            {
                total += 1 + Nivel(v, t.Ambito, funcion.Id);
            }
            else if (t.Texto == "goto")
            {
                total += 1;
            }
            else if (t.Texto is "&&" or "||" && v.Token(t.PadreAst)?.Texto != t.Texto)
            {
                total += 1;
            }
        }

        return total;
    }

    /// <summary>Cuántos ámbitos de control hay desde <paramref name="ambito"/> hasta la función.</summary>
    private static int Nivel(VolcadoC v, string? ambito, string funcion)
    {
        var nivel = 0;
        for (var id = ambito; id is not null && id != funcion && v.Ambitos.TryGetValue(id, out var a); id = a.Padre)
        {
            if (Control.Contains(a.Tipo) && !EsEnvoltorioElseIf(v, a))
            {
                nivel++;
            }
        }

        return nivel;
    }

    private static bool Desciende(VolcadoC v, AmbitoC ambito, string funcion)
    {
        for (var id = ambito.Padre; id is not null && v.Ambitos.TryGetValue(id, out var padre); id = padre.Padre)
        {
            if (id == funcion)
            {
                return true;
            }
        }

        return false;
    }

    private static bool EsEnvoltorioElseIf(VolcadoC v, AmbitoC ambito) =>
        ambito.Tipo == "Else" && PrimerToken(v, ambito) is { Texto: "if" } primero
        && v.Ambitos.Values.Any(a => a.Tipo == "If" && a.Padre == ambito.Id && a.Definicion == primero.Id);

    private static bool EsElseIf(VolcadoC v, AmbitoC ambito) =>
        ambito.Padre is not null && v.Ambitos.TryGetValue(ambito.Padre, out var padre)
        && EsEnvoltorioElseIf(v, padre) && PrimerToken(v, padre)?.Id == ambito.Definicion;

    private static TokenC? PrimerToken(VolcadoC v, AmbitoC ambito) =>
        v.Token(ambito.Inicio) is { } inicio && inicio.Indice + 1 < v.Tokens.Count ? v.Tokens[inicio.Indice + 1] : null;

    // ---- Parámetros: bandera (5.1) y sin usar (libro) ----------------------------------------

    private static IEnumerable<Aviso> Parametros(VolcadoC v)
    {
        foreach (var (ambito, funcion, definicion) in v.FuncionesDefinidas())
        {
            var cuerpo = v.Cuerpo(ambito).ToList();
            foreach (var argumento in funcion.Argumentos)
            {
                if (!v.Variables.TryGetValue(argumento, out var variable) || v.Token(variable.TokenNombre) is not { } nombre)
                {
                    continue;
                }

                var usos = cuerpo.Where(t => t.VarId is not null && t.VarId == nombre.VarId).ToList();
                if (usos.Count == 0)
                {
                    yield return Aviso("c:parametro-sin-usar", Gravedad.Advertencia, nombre,
                        $"Parámetro sin usar: {nombre.Texto} en {funcion.Nombre}", Olores.CodigoMuerto);
                }
                else if (!variable.EsPuntero && (EsBool(v.Texto(variable.TipoInicio, variable.TipoFin)) || usos.All(t => SoloDecide(v, t))))
                {
                    yield return Aviso("c:parametro-bandera", Gravedad.Advertencia, nombre,
                        $"El parámetro {nombre.Texto} de {funcion.Nombre} solo elige qué camino toma la función: actúa de bandera", "5.1");
                }
            }
        }
    }

    private static bool EsBool(string tipo) => tipo.Split(' ').Any(p => p is "bool" or "_Bool");

    /// <summary>El parámetro es toda la condición: <c>if (p)</c>, <c>if (!p)</c>, <c>if (p != 0)</c> o <c>p ? … : …</c>.</summary>
    private static bool SoloDecide(VolcadoC v, TokenC t)
    {
        TokenC? En(int desplazamiento) => t.Indice + desplazamiento is var i && i >= 0 && i < v.Tokens.Count ? v.Tokens[i] : null;
        bool EsCondicion(TokenC? palabra) => palabra?.Texto is "if" or "while";

        return (En(-1)?.Texto == "(" && EsCondicion(En(-2)) && En(1)?.Texto == ")")
               || (En(-1)?.Texto == "!" && En(-2)?.Texto == "(" && EsCondicion(En(-3)) && En(1)?.Texto == ")")
               || (En(-1)?.Texto == "(" && EsCondicion(En(-2)) && En(1)?.Texto is "!=" or "==" && EsCero(En(2)?.Texto) && En(3)?.Texto == ")")
               || En(1)?.Texto == "?";
    }

    // ---- Prefijo del módulo (2.1) -------------------------------------------------------------

    private static IEnumerable<Aviso> Prefijos(VolcadoC v)
    {
        foreach (var (_, funcion, definicion) in v.FuncionesDefinidas())
        {
            if (!funcion.EsStatic && !funcion.Nombre.Contains('_') && funcion.Nombre != "main")
            {
                yield return Aviso("c:sin-prefijo", Gravedad.Advertencia, definicion,
                    $"La función {funcion.Nombre} sale del fichero y no lleva el prefijo de su módulo", "2.1");
            }
        }

        foreach (var variable in v.Variables.Values.Where(x => x.Acceso == "Global" && !x.EsStatic && !x.EsExtern))
        {
            if (v.Token(variable.TokenNombre) is { } nombre && nombre.Fichero == v.Fuente && !nombre.Texto.Contains('_'))
            {
                yield return Aviso("c:sin-prefijo", Gravedad.Advertencia, nombre,
                    $"La variable global {nombre.Texto} sale del fichero y no lleva el prefijo de su módulo", "2.1");
            }
        }
    }

    // ---- Macros con parámetros (2.4) ----------------------------------------------------------

    private static IEnumerable<Aviso> Macros(VolcadoC v)
    {
        foreach (var d in v.Directivas.Where(d => d.Fichero.StartsWith("c/", StringComparison.Ordinal)))
        {
            var m = MacroConParametros().Match(d.Texto);
            if (!m.Success)
            {
                continue;
            }

            // Las que no se pueden cambiar por una función (2.4): posición en el fuente, operadores del
            // preprocesador, ensamblador, y sizeof, que con un array solo funciona en una macro.
            var cuerpo = m.Groups["cuerpo"].Value;
            if (cuerpo.Contains("__FILE__") || cuerpo.Contains("__LINE__") || cuerpo.Contains('#') || cuerpo.Contains("asm")
                || cuerpo.Contains("sizeof"))
            {
                continue;
            }

            yield return new Aviso("c:macro-con-parametros", Gravedad.Informacion, d.Fichero, d.Linea,
                $"La macro {m.Groups["nombre"].Value} tiene parámetros: si se puede intercambiar, cámbiala por una función static inline", "2.4");
        }
    }

    // ---- Asignación dinámica (3.2) ------------------------------------------------------------

    private static IEnumerable<Aviso> AsignacionDinamica(VolcadoC v)
    {
        foreach (var t in v.Tokens.Where(t => t.Fichero == v.Fuente && Reserva.Contains(t.Texto) && Siguiente(v, t)?.Texto == "("))
        {
            yield return Aviso("c:asignacion-dinamica", Gravedad.Advertencia, t,
                $"Asignación dinámica de memoria con {t.Texto}: usa un búfer estático", "3.2");
        }

        // Arrays locales cuyo tamaño sale de una variable: van en la pila con un tamaño que no se conoce al compilar.
        foreach (var variable in v.Variables.Values.Where(x => x.Acceso == "Local" && x.EsArray))
        {
            if (v.Token(variable.TokenNombre) is not { } nombre || nombre.Fichero != v.Fuente
                || Siguiente(v, nombre) is not { Texto: "[" } abre || v.Token(abre.Enlace) is not { } cierra)
            {
                continue;
            }

            var tamano = v.Tokens.Skip(abre.Indice + 1).Take(cierra.Indice - abre.Indice - 1).ToList();
            if (VariablesQueCambian(v, tamano).Any())
            {
                yield return Aviso("c:array-de-tamano-variable", Gravedad.Advertencia, nombre,
                    $"El tamaño del array {nombre.Texto} se decide al ejecutar ({string.Join(" ", tamano.Select(t => t.Texto))}): usa un búfer estático con el tamaño máximo",
                    "3.2");
            }
        }
    }

    /// <summary>
    /// Las variables de una expresión que pueden valer distinto en cada ejecución: ni constantes,
    /// ni dentro de un <c>sizeof</c>, que se calcula al compilar.
    /// </summary>
    private static IEnumerable<TokenC> VariablesQueCambian(VolcadoC v, List<TokenC> expresion)
    {
        var constantes = v.Variables.Values.Where(x => x.EsConst).Select(x => v.Token(x.TokenNombre)?.VarId).ToHashSet();
        for (var i = 0; i < expresion.Count; i++)
        {
            var t = expresion[i];
            if (t.Texto == "sizeof" && i + 1 < expresion.Count && v.Token(expresion[i + 1].Enlace) is { } fin)
            {
                i = expresion.FindIndex(x => x.Id == fin.Id);
                if (i < 0)
                {
                    yield break;
                }

                continue;
            }

            if (t.VarId is not null && t.VarId != "0" && !constantes.Contains(t.VarId))
            {
                yield return t;
            }
        }
    }

    // ---- Valor de retorno sin comprobar (3.1) ---------------------------------------------------

    private static IEnumerable<Aviso> RetornoSinComprobar(VolcadoC v)
    {
        foreach (var t in v.Tokens.Where(t => t.Fichero == v.Fuente && t.Funcion is not null))
        {
            if (!v.Funciones.TryGetValue(t.Funcion!, out var funcion) || EsVoid(v, funcion))
            {
                continue;
            }

            var abre = Siguiente(v, t);
            var anterior = t.Indice > 0 ? v.Tokens[t.Indice - 1] : null;
            if (abre?.Texto != "(" || anterior?.Texto is not (";" or "{" or "}" or "else") || v.Token(abre.Enlace) is not { } cierra)
            {
                continue;
            }

            if (Siguiente(v, cierra)?.Texto == ";")
            {
                yield return Aviso("c:retorno-sin-comprobar", Gravedad.Advertencia, t,
                    $"El valor que devuelve {funcion.Nombre} no se comprueba", "3.1");
            }
        }
    }

    /// <summary>Si la función devuelve void: los tokens de antes del nombre, sin calificadores, son solo «void».</summary>
    private static bool EsVoid(VolcadoC v, FuncionC funcion)
    {
        if (v.Token(funcion.TokenDeclaracion) is not { } nombre)
        {
            return true;
        }

        var tipo = new List<string>();
        for (var i = nombre.Indice - 1; i >= 0 && v.Tokens[i].Texto is not (";" or "{" or "}"); i--)
        {
            tipo.Add(v.Tokens[i].Texto);
        }

        var limpio = tipo.Where(p => !Calificadores.Contains(p)).ToList();
        return limpio.Count == 1 && limpio[0] == "void";
    }

    // ---- Módulo grande (libro) ----------------------------------------------------------------

    private static IEnumerable<Aviso> ModuloGrande(VolcadoC v, Umbrales u)
    {
        var funciones = v.FuncionesDefinidas().Count();
        if (funciones > u.FuncionesModulo)
        {
            yield return new Aviso("c:modulo-grande", Gravedad.Advertencia, v.Fuente, 1,
                $"{v.Fuente} define {funciones} funciones (umbral {u.FuncionesModulo})", Olores.ModuloGrande);
        }

        var lineas = v.Crudos.Where(t => t.Fichero == v.Fuente).Select(t => t.Linea).DefaultIfEmpty(0).Max();
        if (lineas > u.LineasModulo)
        {
            yield return new Aviso("c:modulo-grande", Gravedad.Advertencia, v.Fuente, 1,
                $"{v.Fuente} tiene {lineas} líneas (umbral {u.LineasModulo})", Olores.ModuloGrande);
        }
    }

    // ---- Números mágicos (2.1, libro: obsesión por los primitivos) -------------------------------

    /// <summary>
    /// Se miran los tokens sin preprocesar, porque en los preprocesados las macros ya están
    /// sustituidas por su número. Solo dentro de las funciones, fuera de las líneas con <c>const</c>,
    /// y sin contar el 0 y el 1.
    /// </summary>
    private static IEnumerable<Aviso> NumerosMagicos(VolcadoC v)
    {
        var cuerpos = v.FuncionesDefinidas()
            .Select(f => (Desde: v.Token(f.Ambito.Inicio)?.Linea ?? 0, Hasta: v.Token(f.Ambito.Fin)?.Linea ?? 0))
            .ToList();

        foreach (var linea in v.Crudos.Where(t => t.Fichero == v.Fuente).GroupBy(t => t.Linea))
        {
            var tokens = linea.ToList();
            if (tokens[0].Texto == "#" || tokens.Any(t => t.Texto is "const" or "enum")
                || !cuerpos.Any(c => linea.Key > c.Desde && linea.Key < c.Hasta))
            {
                continue;
            }

            var numeros = tokens.Select(t => t.Texto).Where(EsNumero).Where(n => !EsCero(n) && !EsUno(n)).Distinct().ToList();
            if (numeros.Count > 0)
            {
                yield return new Aviso("c:numero-magico", Gravedad.Informacion, v.Fuente, linea.Key,
                    $"Número escrito a pelo: {string.Join(", ", numeros)}. Dale un nombre con un enum, un static const o un #define", "2.1");
            }
        }
    }

    // ---- Campo temporal (libro) ---------------------------------------------------------------

    private static IEnumerable<Aviso> CampoTemporal(VolcadoC v)
    {
        var funciones = v.FuncionesDefinidas().ToList();
        foreach (var variable in v.Variables.Values.Where(x => x.Acceso == "Global" && x.EsStatic && !x.EsConst))
        {
            if (v.Token(variable.TokenNombre) is not { } nombre || nombre.Fichero != v.Fuente || nombre.VarId is null)
            {
                continue;
            }

            var usuarias = funciones
                .Where(f => v.Cuerpo(f.Ambito).Any(t => t.VarId == nombre.VarId))
                .Select(f => f.Funcion.Nombre)
                .ToList();
            if (usuarias.Count == 1)
            {
                yield return Aviso("c:campo-temporal", Gravedad.Informacion, nombre,
                    $"La variable {nombre.Texto} es de todo el fichero, pero solo la usa {usuarias[0]}: puede ser una variable static dentro de esa función",
                    Olores.CampoTemporal);
            }
        }
    }

    // ---- Intimidad inapropiada: extern e includes cíclicos (2.3) ----------------------------------

    private static IEnumerable<Aviso> Extern(VolcadoC v)
    {
        foreach (var variable in v.Variables.Values.Where(x => x.EsExtern))
        {
            if (v.Token(variable.TokenNombre) is not { } nombre)
            {
                continue;
            }

            if (nombre.Fichero == v.Fuente)
            {
                yield return Aviso("c:global-de-otro-modulo", Gravedad.Advertencia, nombre,
                    $"La variable global {nombre.Texto} de otro módulo se declara con extern dentro de un .c", "2.3");
                continue;
            }

            var modulo = Modulo(nombre.Fichero);
            if (modulo == Modulo(v.Fuente))
            {
                continue;
            }

            var uso = v.Tokens.FirstOrDefault(t => t.Fichero == v.Fuente && t.VarId == nombre.VarId);
            if (uso is not null)
            {
                yield return Aviso("c:global-de-otro-modulo", Gravedad.Advertencia, uso,
                    $"Usa la variable global {nombre.Texto} del módulo {modulo}: pídesela con una función", "2.3");
            }
        }
    }

    /// <summary>
    /// Cabeceras que se incluyen en círculo (2.3). Solo cuentan los <c>#include</c> de las cabeceras:
    /// que dos .c usen cada uno la cabecera del otro no hace un ciclo de inclusión.
    /// </summary>
    private static IEnumerable<Aviso> IncludesCiclicos(IReadOnlyList<VolcadoC> volcados)
    {
        var includes = volcados
            .SelectMany(v => v.Directivas)
            .Where(d => d.Fichero.StartsWith("c/", StringComparison.Ordinal) && d.Fichero.EndsWith(".h", StringComparison.Ordinal))
            .Select(d => (Directiva: d, Destino: IncludeLocal().Match(d.Texto)))
            .Where(x => x.Destino.Success)
            .Select(x => (x.Directiva, Desde: Modulo(x.Directiva.Fichero), Hasta: Modulo(x.Destino.Groups[1].Value)))
            .Where(x => x.Desde != x.Hasta)
            .DistinctBy(x => (x.Directiva.Fichero, x.Directiva.Linea))
            .ToList();

        var aristas = includes.GroupBy(x => x.Desde).ToDictionary(g => g.Key, g => g.Select(x => x.Hasta).ToHashSet());
        foreach (var (directiva, desde, hasta) in includes)
        {
            if (Alcanza(aristas, hasta, desde))
            {
                yield return new Aviso("c:include-ciclico", Gravedad.Advertencia, directiva.Fichero, directiva.Linea,
                    $"Inclusión circular: {desde}.h incluye a {hasta}.h, y {hasta}.h acaba incluyendo a {desde}.h", "2.3");
            }
        }
    }

    private static bool Alcanza(Dictionary<string, HashSet<string>> aristas, string desde, string objetivo)
    {
        var vistos = new HashSet<string>();
        var pendientes = new Stack<string>([desde]);
        while (pendientes.TryPop(out var actual))
        {
            if (actual == objetivo)
            {
                return true;
            }

            if (vistos.Add(actual) && aristas.TryGetValue(actual, out var siguientes))
            {
                foreach (var s in siguientes)
                {
                    pendientes.Push(s);
                }
            }
        }

        return false;
    }

    // ---- switch repetido (5.1) -----------------------------------------------------------------

    private static IEnumerable<Aviso> SwitchRepetido(IReadOnlyList<VolcadoC> volcados)
    {
        var switches = new List<(string Expresion, string Funcion, TokenC Token)>();
        foreach (var v in volcados)
        {
            foreach (var (ambito, funcion, _) in v.FuncionesDefinidas())
            {
                foreach (var s in v.Ambitos.Values.Where(a => a.Tipo == "Switch" && Desciende(v, a, ambito.Id)))
                {
                    if (v.Token(s.Definicion) is { } palabra && Siguiente(v, palabra) is { Texto: "(" } abre && v.Token(abre.Enlace) is { } cierra)
                    {
                        var expresion = string.Join(" ", v.Tokens.Skip(abre.Indice + 1).Take(cierra.Indice - abre.Indice - 1).Select(t => t.Texto));
                        switches.Add((expresion, funcion.Nombre, palabra));
                    }
                }
            }
        }

        // La misma selección dos veces, en la misma función o en otra (5.1: «Repeated Switches»).
        foreach (var grupo in switches.GroupBy(s => s.Expresion).Where(g => g.Count() >= 2))
        {
            var funciones = string.Join(", ", grupo.Select(s => s.Funcion).Distinct().Order(StringComparer.Ordinal));
            foreach (var s in grupo)
            {
                yield return Aviso("c:switch-repetido", Gravedad.Advertencia, s.Token,
                    $"El mismo switch ({grupo.Key}) aparece {grupo.Count()} veces, en {funciones}: la selección puede ir en una tabla", "5.1");
            }
        }
    }

    // ---- Grupos de datos (libro) ---------------------------------------------------------------

    private static IEnumerable<Aviso> GruposDeDatos(IReadOnlyList<VolcadoC> volcados)
    {
        var firmas = new Dictionary<string, (TokenC Definicion, List<string> Parametros)>();
        foreach (var v in volcados)
        {
            foreach (var (_, funcion, definicion) in v.FuncionesDefinidas())
            {
                var parametros = funcion.Argumentos
                    .Select(a => v.Variables.TryGetValue(a, out var x) ? $"{v.Texto(x.TipoInicio, x.TipoFin)} {v.Token(x.TokenNombre)?.Texto}" : "")
                    .ToList();
                firmas.TryAdd(funcion.Nombre, (definicion, parametros));
            }
        }

        var trios = firmas
            .SelectMany(f => Enumerable.Range(0, Math.Max(0, f.Value.Parametros.Count - 2))
                .Select(i => (Trio: string.Join(", ", f.Value.Parametros.Skip(i).Take(3)), Funcion: f.Key)))
            .GroupBy(x => x.Trio)
            .Where(g => g.Select(x => x.Funcion).Distinct().Count() >= 3);

        foreach (var grupo in trios)
        {
            var funciones = grupo.Select(x => x.Funcion).Distinct().Order(StringComparer.Ordinal).ToList();
            foreach (var funcion in funciones)
            {
                yield return Aviso("c:grupo-de-datos", Gravedad.Informacion, firmas[funcion].Definicion,
                    $"Los parámetros ({grupo.Key}) van juntos en {funciones.Count} funciones: pueden ir en un struct", Olores.GruposDeDatos);
            }
        }
    }

    // ---- Cadenas de mensajes (libro) -----------------------------------------------------------

    private static IEnumerable<Aviso> CadenasDeMensajes(VolcadoC v)
    {
        var avisadas = new HashSet<int>();
        var tokens = v.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Fichero != v.Fuente || tokens[i].Tipo != "name")
            {
                continue;
            }

            var j = i;
            var accesos = 0;
            while (j + 2 < tokens.Count && tokens[j + 1].Texto is "->" or "." && tokens[j + 2].Tipo == "name")
            {
                accesos++;
                j += 2;
            }

            if (accesos >= 3 && avisadas.Add(tokens[i].Linea))
            {
                var texto = string.Concat(tokens.Skip(i).Take(j - i + 1).Select(t => t.Texto));
                yield return Aviso("c:cadena-de-mensajes", Gravedad.Informacion, tokens[i],
                    $"Cadena de accesos {texto}: quien la escribe tiene que conocer la forma de todas las estructuras del camino",
                    Olores.CadenasDeMensajes);
            }

            i = j;
        }
    }

    // ---- Apoyo -------------------------------------------------------------------------------

    private static Aviso Aviso(string regla, Gravedad gravedad, TokenC token, string mensaje, string? submodulo) =>
        new(regla, gravedad, token.Fichero, token.Linea, mensaje, submodulo);

    private static TokenC? Siguiente(VolcadoC v, TokenC t) => t.Indice + 1 < v.Tokens.Count ? v.Tokens[t.Indice + 1] : null;

    private static string Modulo(string ruta) => Path.GetFileNameWithoutExtension(ruta);

    private static bool EsNumero(string texto) => texto.Length > 0 && char.IsAsciiDigit(texto[0]);

    private static bool EsCero(string? texto) => Valor(texto) == 0;

    private static bool EsUno(string? texto) => Valor(texto) == 1;

    /// <summary>El valor de un literal entero, sin sufijos; null si no es un entero.</summary>
    private static long? Valor(string? texto)
    {
        if (texto is null || !EsNumero(texto))
        {
            return null;
        }

        var limpio = texto.TrimEnd('u', 'U', 'l', 'L');
        if (limpio.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(limpio[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : null;
        }

        return long.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    [GeneratedRegex(@"^#\s*define\s+(?<nombre>\w+)\((?<parametros>[^)]*)\)\s*(?<cuerpo>.*)$")]
    private static partial Regex MacroConParametros();

    [GeneratedRegex("^#\\s*include\\s*\"([^\"]+)\"")]
    private static partial Regex IncludeLocal();
}
