using System.Text.Json;
using CardLint.Core;

namespace CardLint.Tests;

public class InformeJsonTests
{
    private static readonly DateTimeOffset Fecha = new(2026, 11, 25, 19, 30, 5, TimeSpan.FromHours(1));

    private static readonly MetricasFuncion Process =
        new(MetricasFuncion.JavaCard, "javacard/src/A.java", 28, "process(APDU)", 31, 40, Instrucciones: 83);

    private static ResultadoAnalisis Resultado(params Aviso[] avisos) =>
        new("F:/repo", ["fijo"], avisos, [], [new Contramedida("c/src/pin.c", 68, 73, 90, "canal lateral")], Gravedad.Fallo,
            ["una nota"], [Process]);

    [Fact]
    public void Lleva_el_formato_la_fecha_el_gate_y_los_avisos_con_la_gravedad_en_texto()
    {
        var fallo = new Aviso("cppcheck:ctunullpointer", Gravedad.Fallo, "c/src/comando.c", 55, "Puntero nulo", "3.4");

        using var json = JsonDocument.Parse(InformeJson.Generar(Resultado(fallo), new Configuracion(), Fecha));
        var raiz = json.RootElement;

        Assert.Equal(InformeJson.Formato, raiz.GetProperty("formato").GetInt32());
        Assert.Equal(Fecha, raiz.GetProperty("fecha").GetDateTimeOffset());
        Assert.Equal("falla", raiz.GetProperty("gate").GetString());
        var aviso = Assert.Single(raiz.GetProperty("avisos").EnumerateArray());
        Assert.Equal(("Fallo", "c/src/comando.c", 55), (aviso.GetProperty("gravedad").GetString(), aviso.GetProperty("fichero").GetString(), aviso.GetProperty("linea").GetInt32()));
        Assert.Equal(["una nota"], raiz.GetProperty("notas").EnumerateArray().Select(n => n.GetString()));
        Assert.Equal("canal lateral", Assert.Single(raiz.GetProperty("contramedidas").EnumerateArray()).GetProperty("motivo").GetString());
    }

    [Fact]
    public void Las_metricas_que_no_mide_un_lenguaje_no_se_escriben()
    {
        using var json = JsonDocument.Parse(InformeJson.Generar(Resultado(), new Configuracion(), Fecha));

        var funcion = Assert.Single(json.RootElement.GetProperty("funciones").EnumerateArray());
        Assert.Equal(("javacard", "process(APDU)", 40, 83), (funcion.GetProperty("lenguaje").GetString(), funcion.GetProperty("nombre").GetString(),
            funcion.GetProperty("ciclomatica").GetInt32(), funcion.GetProperty("instrucciones").GetInt32()));
        Assert.False(funcion.TryGetProperty("lineas", out _));
        Assert.False(funcion.TryGetProperty("parametros", out _));
        Assert.Equal("pasa", json.RootElement.GetProperty("gate").GetString());
    }

    [Fact]
    public void La_configuracion_copiada_a_card_lint_json_se_lee_igual()
    {
        using var carpeta = new CarpetaTemporal();
        var configuracion = new Configuracion(Gravedad.Advertencia, 70, new Umbrales(Cognitiva: 20, Ciclomatica: 12, LineasFuncion: 60));

        using var json = JsonDocument.Parse(InformeJson.Generar(Resultado(), configuracion, Fecha));
        carpeta.Escribir(Configuracion.NombreFichero, json.RootElement.GetProperty("configuracion").GetRawText());

        Assert.Equal(configuracion, Configuracion.Leer(carpeta.Ruta));
    }

    [Fact]
    public void Lleva_los_umbrales_de_PMD_y_las_tildes_sin_escapar()
    {
        var aviso = new Aviso("c:numero-magico", Gravedad.Informacion, "c/src/a.c", 1, "Número escrito a pelo: 8");

        var texto = InformeJson.Generar(Resultado(aviso), new Configuracion(), Fecha);
        using var json = JsonDocument.Parse(texto);

        var pmd = json.RootElement.GetProperty("umbralesJavaCard");
        Assert.Equal((15, 10, 60), (pmd.GetProperty("cognitiva").GetInt32(), pmd.GetProperty("ciclomatica").GetInt32(), pmd.GetProperty("instrucciones").GetInt32()));
        Assert.Contains("Número escrito a pelo", texto);
    }

    [Fact]
    public void El_nombre_del_fichero_lleva_la_fecha_y_la_hora()
    {
        Assert.Equal("card-lint-20261125-193005.json", InformeJson.NombreFichero(Fecha));
    }
}
