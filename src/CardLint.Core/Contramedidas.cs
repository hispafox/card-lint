namespace CardLint.Core;

/// <summary>
/// Código marcado con un comentario que empieza por <c>CONTRAMEDIDA</c>, la convención de la 3.5.
/// <see cref="Desde"/> y <see cref="Hasta"/> son las líneas de código que cubre.
/// </summary>
/// <param name="Motivo">Lo que va entre paréntesis detrás de la palabra: contra qué protege. Vacío si no lo dice.</param>
public sealed record Contramedida(string Fichero, int LineaComentario, int Desde, int Hasta, string Motivo)
{
    public bool Cubre(string fichero, int linea) =>
        Rutas.Iguales(Fichero, fichero) && linea >= Desde && linea <= Hasta;
}

/// <summary>
/// Lee las contramedidas de un fichero C. Si el comentario está fuera de cualquier función,
/// cubre la declaración o la función que viene detrás; si está dentro, la instrucción que viene detrás.
/// </summary>
public static class LectorContramedidas
{
    private const string Marca = "CONTRAMEDIDA";

    public static IReadOnlyList<Contramedida> Leer(string fichero, string codigo)
    {
        var tokens = TokenizadorC.Tokenizar(codigo);
        var resultado = new List<Contramedida>();
        var profundidad = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Es("{"))
            {
                profundidad++;
            }
            else if (token.Es("}"))
            {
                profundidad = Math.Max(0, profundidad - 1);
            }
            else if (token.Tipo == TipoToken.Comentario && EsContramedida(token.Texto, out var motivo))
            {
                resultado.Add(Cubrir(fichero, tokens, i, profundidad, motivo));
            }
        }

        return resultado;
    }

    /// <summary>Separa los avisos que caen en una contramedida de los que no.</summary>
    public static (IReadOnlyList<Aviso> SinJustificar, IReadOnlyList<AvisoJustificado> Justificados) Separar(
        IEnumerable<Aviso> avisos, IReadOnlyList<Contramedida> contramedidas)
    {
        var sinJustificar = new List<Aviso>();
        var justificados = new List<AvisoJustificado>();
        foreach (var aviso in avisos)
        {
            var contramedida = contramedidas.FirstOrDefault(c => c.Cubre(aviso.Fichero, aviso.Linea));
            if (contramedida is null)
            {
                sinJustificar.Add(aviso);
            }
            else
            {
                justificados.Add(new AvisoJustificado(aviso, contramedida));
            }
        }

        return (sinJustificar, justificados);
    }

    private static Contramedida Cubrir(string fichero, IReadOnlyList<Token> tokens, int comentario, int profundidad, string motivo)
    {
        var primero = SiguienteCodigo(tokens, comentario + 1);

        // Sin código detrás, o detrás solo cierra el bloque: cubre el propio comentario.
        if (primero < 0 || tokens[primero].Es("}"))
        {
            var c = tokens[comentario];
            return new Contramedida(fichero, c.Linea, c.Linea, c.LineaFin, motivo);
        }

        // Detrás viene una directiva, como el #define de una barrera: cubre sus líneas.
        if (tokens[primero].Tipo == TipoToken.Directiva)
        {
            var d = tokens[primero];
            return new Contramedida(fichero, tokens[comentario].Linea, d.Linea, d.LineaFin, motivo);
        }

        var ultimo = profundidad == 0 ? FinDeDeclaracion(tokens, primero) : FinDeInstruccion(tokens, primero);
        return new Contramedida(fichero, tokens[comentario].Linea, tokens[primero].Linea, tokens[ultimo].Linea, motivo);
    }

    /// <summary>Hasta el <c>;</c> de una declaración o la <c>}</c> que cierra el cuerpo de una función.</summary>
    private static int FinDeDeclaracion(IReadOnlyList<Token> tokens, int desde)
    {
        var llaves = 0;
        for (var j = desde; j < tokens.Count; j++)
        {
            var t = tokens[j];
            if (t.Es("{"))
            {
                llaves++;
            }
            else if (t.Es("}") && --llaves == 0)
            {
                return j;
            }
            else if (t.Es(";") && llaves == 0)
            {
                return j;
            }
        }

        return tokens.Count - 1;
    }

    /// <summary>
    /// Hasta el final de la instrucción: su <c>;</c> o la <c>}</c> de su bloque, siguiendo
    /// con el <c>else</c> si lo hay y con el <c>while</c> de un <c>do</c>.
    /// </summary>
    private static int FinDeInstruccion(IReadOnlyList<Token> tokens, int desde)
    {
        var esperaWhile = tokens[desde].Es("do");
        var parentesis = 0;
        var llaves = 0;
        for (var j = desde; j < tokens.Count; j++)
        {
            var t = tokens[j];
            if (t.Tipo == TipoToken.Comentario)
            {
                continue;
            }

            if (t.Es("("))
            {
                parentesis++;
            }
            else if (t.Es(")"))
            {
                parentesis--;
            }
            else if (t.Es("{"))
            {
                llaves++;
            }
            else if (t.Es("}"))
            {
                if (--llaves < 0)
                {
                    return j - 1;
                }

                if (llaves == 0 && parentesis == 0)
                {
                    if (esperaWhile)
                    {
                        esperaWhile = false;
                        continue;
                    }

                    if (!SigueElse(tokens, j))
                    {
                        return j;
                    }
                }
            }
            else if (t.Es(";") && llaves == 0 && parentesis == 0 && !SigueElse(tokens, j))
            {
                return j;
            }
        }

        return tokens.Count - 1;
    }

    private static bool SigueElse(IReadOnlyList<Token> tokens, int j)
    {
        var siguiente = SiguienteCodigo(tokens, j + 1);
        return siguiente >= 0 && tokens[siguiente].Es("else");
    }

    private static int SiguienteCodigo(IReadOnlyList<Token> tokens, int desde)
    {
        for (var j = desde; j < tokens.Count; j++)
        {
            if (tokens[j].Tipo != TipoToken.Comentario)
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Un comentario es de contramedida si alguna de sus líneas empieza por la marca.</summary>
    internal static bool EsContramedida(string comentario, out string motivo)
    {
        motivo = "";
        foreach (var bruta in comentario.Split('\n'))
        {
            var linea = bruta.Trim();
            if (linea.StartsWith("/*", StringComparison.Ordinal) || linea.StartsWith("//", StringComparison.Ordinal))
            {
                linea = linea[2..];
            }

            linea = linea.TrimStart(' ', '\t', '*');
            if (!linea.StartsWith(Marca, StringComparison.Ordinal))
            {
                continue;
            }

            var resto = linea[Marca.Length..].TrimStart();
            if (resto.StartsWith('(') && resto.IndexOf(')') is var cierre and > 0)
            {
                motivo = resto[1..cierre].Trim();
            }

            return true;
        }

        return false;
    }
}
