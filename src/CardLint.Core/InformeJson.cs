using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardLint.Core;

/// <summary>
/// El resultado de un análisis en JSON, para el cuadro de mando: los avisos, la configuración con la que
/// se hizo y las métricas de todas las funciones. Un fichero por análisis; juntos son el historial.
/// </summary>
public static class InformeJson
{
    /// <summary>Sube cuando cambia la forma del fichero, para que el cuadro de mando sepa qué lee.</summary>
    public const int Formato = 1;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },

        // El fichero no va dentro de una página: las tildes pueden ir tal cual.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Generar(ResultadoAnalisis resultado, Configuracion configuracion, DateTimeOffset fecha) =>
        JsonSerializer.Serialize(new
        {
            formato = Formato,
            fecha,
            carpeta = resultado.Carpeta,
            gate = resultado.GatePasa ? "pasa" : "falla",

            // Los nombres son los de card-lint.json, que se lee sin distinguir mayúsculas: copiado a la carpeta, vale tal cual.
            configuracion = new
            {
                configuracion.GravedadQueFalla,
                configuracion.MinimoTokensDuplicado,
                Umbrales = configuracion.UmbralesEfectivos,
            },

            // Los de PMD no se configuran, y avisan desde el umbral; los de C, cuando se pasa.
            umbralesJavaCard = new
            {
                cognitiva = Analizadores.PmdAnalizador.UmbralCognitiva,
                ciclomatica = Analizadores.PmdAnalizador.UmbralCiclomatica,
                instrucciones = Analizadores.PmdAnalizador.UmbralInstrucciones,
            },
            analizadores = resultado.Analizadores,
            notas = resultado.Notas ?? [],
            avisos = resultado.Avisos,
            justificados = resultado.Justificados,
            contramedidas = resultado.Contramedidas,
            funciones = resultado.Funciones ?? [],
        }, Opciones);

    /// <summary>El nombre del fichero de un análisis: la fecha y la hora, para que se ordenen solos.</summary>
    public static string NombreFichero(DateTimeOffset fecha) => $"card-lint-{fecha:yyyyMMdd-HHmmss}.json";
}
