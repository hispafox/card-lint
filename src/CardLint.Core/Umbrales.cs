namespace CardLint.Core;

/// <summary>
/// A partir de qué cifra avisan las reglas propias de C. Son provisionales, hasta que Pedro
/// decida los del curso; la cognitiva y la ciclomática van con los de PMD para Java (15 y 10),
/// para que las dos partes del repositorio se midan igual.
/// </summary>
public sealed record Umbrales(
    int Cognitiva = 15,
    int Ciclomatica = 10,
    int LineasFuncion = 40,
    int Parametros = 5,
    int FuncionesModulo = 20,
    int LineasModulo = 400);
