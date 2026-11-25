using System.Text;

namespace CardLint.Core;

internal enum TipoToken
{
    Palabra,
    Signo,
    Comentario,

    /// <summary>Una directiva del preprocesador entera, con sus líneas continuadas.</summary>
    Directiva,
}

internal sealed record Token(TipoToken Tipo, string Texto, int Linea, int LineaFin)
{
    public bool Es(string texto) => Tipo is TipoToken.Palabra or TipoToken.Signo && Texto == texto;
}

/// <summary>
/// Lo justo de C para seguir llaves, paréntesis y comentarios: se salta las cadenas y los
/// caracteres, y cada directiva del preprocesador es un token entero, para que una llave
/// dentro de ellas no cuente.
/// </summary>
internal static class TokenizadorC
{
    public static List<Token> Tokenizar(string codigo)
    {
        var tokens = new List<Token>();
        var linea = 1;
        var inicioDeLinea = true;
        var i = 0;

        while (i < codigo.Length)
        {
            var c = codigo[i];
            var siguiente = i + 1 < codigo.Length ? codigo[i + 1] : '\0';

            if (c == '\n')
            {
                linea++;
                inicioDeLinea = true;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (inicioDeLinea && c == '#')
            {
                var desde = linea;
                var inicio = i;
                i = SaltarPreprocesador(codigo, i, ref linea);
                tokens.Add(new Token(TipoToken.Directiva, codigo[inicio..i].TrimEnd(), desde, linea));
                continue;
            }

            inicioDeLinea = false;

            if (c == '/' && siguiente == '*')
            {
                var fin = codigo.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var hasta = fin < 0 ? codigo.Length : fin + 2;
                var texto = codigo[i..hasta];
                var lineaFin = linea + Contar(texto, '\n');
                tokens.Add(new Token(TipoToken.Comentario, texto, linea, lineaFin));
                linea = lineaFin;
                i = hasta;
            }
            else if (c == '/' && siguiente == '/')
            {
                var fin = codigo.IndexOf('\n', i);
                var hasta = fin < 0 ? codigo.Length : fin;
                tokens.Add(new Token(TipoToken.Comentario, codigo[i..hasta], linea, linea));
                i = hasta;
            }
            else if (c is '"' or '\'')
            {
                i = SaltarLiteral(codigo, i, ref linea);
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var inicio = i;
                while (i < codigo.Length && (char.IsLetterOrDigit(codigo[i]) || codigo[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(TipoToken.Palabra, codigo[inicio..i], linea, linea));
            }
            else if (c is '{' or '}' or '(' or ')' or ';')
            {
                tokens.Add(new Token(TipoToken.Signo, c.ToString(), linea, linea));
                i++;
            }
            else
            {
                i++;
            }
        }

        return tokens;
    }

    /// <summary>Hasta el final de la directiva, con sus líneas continuadas con «\».</summary>
    private static int SaltarPreprocesador(string codigo, int i, ref int linea)
    {
        while (i < codigo.Length)
        {
            if (codigo[i] == '\n')
            {
                var continua = i > 0 && codigo[i - 1] == '\\'
                    || i > 1 && codigo[i - 1] == '\r' && codigo[i - 2] == '\\';
                if (!continua)
                {
                    return i;
                }

                linea++;
            }

            i++;
        }

        return i;
    }

    private static int SaltarLiteral(string codigo, int i, ref int linea)
    {
        var comilla = codigo[i++];
        while (i < codigo.Length && codigo[i] != comilla && codigo[i] != '\n')
        {
            i += codigo[i] == '\\' ? 2 : 1;
        }

        return Math.Min(i + 1, codigo.Length);
    }

    private static int Contar(string texto, char caracter)
    {
        var n = 0;
        foreach (var c in texto)
        {
            if (c == caracter)
            {
                n++;
            }
        }

        return n;
    }
}
