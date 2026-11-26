namespace CardLint.Core;

/// <summary>
/// Lo que mide card-lint de una función de C o de un método de Java Card, pase o no de los umbrales:
/// el cuadro de mando las necesita todas para recalcular con otros umbrales. <see cref="Fichero"/> va
/// relativo a la carpeta analizada, con «/» como separador. Lo que la herramienta de ese lenguaje no
/// mide va a null: en C no hay instrucciones (NCSS), y en Java Card no hay líneas ni parámetros.
/// </summary>
/// <param name="Lenguaje"><see cref="C"/> o <see cref="JavaCard"/>.</param>
/// <param name="Nombre">El de la función en C; en Java Card, el método con los tipos de sus parámetros, como «process(APDU)».</param>
public sealed record MetricasFuncion(
    string Lenguaje,
    string Fichero,
    int Linea,
    string Nombre,
    int? Cognitiva = null,
    int? Ciclomatica = null,
    int? Lineas = null,
    int? Parametros = null,
    int? Instrucciones = null)
{
    public const string C = "c";
    public const string JavaCard = "javacard";
}
