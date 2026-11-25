namespace CardLint.Core;

/// <summary>Gravedad de un aviso, de menos a más. El quality gate compara con ella.</summary>
public enum Gravedad
{
    Informacion,
    Advertencia,
    Fallo,
}

/// <summary>
/// Un hallazgo de un analizador. <see cref="Fichero"/> va relativo a la carpeta analizada,
/// con «/» como separador.
/// </summary>
/// <param name="Submodulo">
/// Dónde se explica: el submódulo del curso (por ejemplo «3.2») o, si el olor viene solo del
/// catálogo del libro, su nombre («olor: código muerto»).
/// </param>
/// <param name="Detalle">El mensaje original de la herramienta, si lo hay.</param>
public sealed record Aviso(
    string Regla,
    Gravedad Gravedad,
    string Fichero,
    int Linea,
    string Mensaje,
    string? Submodulo = null,
    string? Detalle = null);

/// <summary>Un aviso que cae en código marcado como contramedida.</summary>
public sealed record AvisoJustificado(Aviso Aviso, Contramedida Contramedida);
