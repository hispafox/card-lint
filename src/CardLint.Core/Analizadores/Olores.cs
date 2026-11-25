namespace CardLint.Core.Analizadores;

/// <summary>
/// Nombres de los olores que vienen solo del catálogo del libro, para la columna «se explica en».
/// Los que también están en el curso llevan su submódulo.
/// </summary>
internal static class Olores
{
    public const string CodigoMuerto = "olor: código muerto";
    public const string ModuloGrande = "olor: clase o módulo grande";
    public const string CampoTemporal = "olor: campo temporal";
    public const string GruposDeDatos = "olor: grupos de datos";
    public const string CadenasDeMensajes = "olor: cadenas de mensajes";
}
