using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardLint.Core;

/// <summary>
/// Lo que se puede cambiar por proyecto, en un <c>card-lint.json</c> dentro de la carpeta analizada.
/// </summary>
/// <param name="GravedadQueFalla">
/// Un aviso sin justificar de esta gravedad o mayor hace fallar el quality gate.
/// Provisional hasta que Pedro decida los umbrales.
/// </param>
/// <param name="MinimoTokensDuplicado">Cuántos tokens seguidos tienen que coincidir para que CPD lo dé por duplicado.</param>
/// <param name="Umbrales">Los de las reglas propias de C; los que no se den, por defecto.</param>
public sealed record Configuracion(Gravedad GravedadQueFalla = Gravedad.Fallo, int MinimoTokensDuplicado = 50, Umbrales? Umbrales = null)
{
    public Umbrales UmbralesEfectivos => Umbrales ?? new Umbrales();

    public const string NombreFichero = "card-lint.json";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Configuracion Leer(string carpeta)
    {
        var ruta = Path.Combine(carpeta, NombreFichero);
        if (!File.Exists(ruta))
        {
            return new Configuracion();
        }

        try
        {
            return JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(ruta), Opciones) ?? new Configuracion();
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"{NombreFichero} no se puede leer: {e.Message}", e);
        }
    }
}
