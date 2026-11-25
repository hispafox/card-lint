using CardLint.Core;

namespace CardLint.Tests;

public class AnalisisTests
{
    private const string Comando = """
        uint16_t comando_verificar(void)
        {
            uint8_t resultado = pin_verificar();
            if (resultado != PIN_CORRECTO) {
                return SW_ERROR;
            }
            /* CONTRAMEDIDA (fallos provocados): segunda comprobación, a propósito. */
            if (resultado != PIN_CORRECTO) {
                return SW_ERROR;
            }
            return SW_OK;
        }
        """;

    [Fact]
    public void Un_fallo_justificado_no_hace_fallar_el_gate()
    {
        using var carpeta = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        carpeta.Escribir("c/src/comando.c", Comando);
        var fallo = new Aviso("condicion-repetida", Gravedad.Fallo, "c/src/comando.c", 8, "Condición repetida");
        var advertencia = new Aviso("funcion-larga", Gravedad.Advertencia, "c/src/comando.c", 1, "Función larga");

        var r = Analisis.Ejecutar(carpeta.Ruta, new Configuracion(), [new AnalizadorFijo(fallo, advertencia)], trabajo.Ruta);

        Assert.True(r.GatePasa);
        Assert.Equal([advertencia], r.Avisos);
        Assert.Equal(fallo, Assert.Single(r.Justificados).Aviso);
        Assert.Equal("c/src/comando.c", Assert.Single(r.Contramedidas).Fichero);
        Assert.Equal(["fijo"], r.Analizadores);
    }

    [Fact]
    public void Un_fallo_sin_justificar_hace_fallar_el_gate()
    {
        using var carpeta = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        carpeta.Escribir("c/src/comando.c", Comando);
        var fallo = new Aviso("condicion-repetida", Gravedad.Fallo, "c/src/comando.c", 4, "Condición repetida");

        var r = Analisis.Ejecutar(carpeta.Ruta, new Configuracion(), [new AnalizadorFijo(fallo)], trabajo.Ruta);

        Assert.False(r.GatePasa);
    }

    [Fact]
    public void La_gravedad_que_falla_se_puede_bajar()
    {
        using var carpeta = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        var advertencia = new Aviso("funcion-larga", Gravedad.Advertencia, "c/src/comando.c", 1, "Función larga");

        var r = Analisis.Ejecutar(carpeta.Ruta, new Configuracion(Gravedad.Advertencia), [new AnalizadorFijo(advertencia)], trabajo.Ruta);

        Assert.False(r.GatePasa);
    }

    [Fact]
    public void Solo_lee_contramedidas_de_ficheros_c_y_h()
    {
        using var carpeta = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        carpeta.Escribir("c/include/seguridad.h", "/* CONTRAMEDIDA (canal lateral): x */\nint a;\n");
        carpeta.Escribir("javacard/src/Applet.java", "/* CONTRAMEDIDA (canal lateral): x */\nclass A {}\n");

        var r = Analisis.Ejecutar(carpeta.Ruta, new Configuracion(), [], trabajo.Ruta, ["una nota"]);

        Assert.Equal("c/include/seguridad.h", Assert.Single(r.Contramedidas).Fichero);
        Assert.Equal(["una nota"], r.Notas);
    }
}

public class ConfiguracionTests
{
    [Fact]
    public void Sin_fichero_usa_los_valores_por_defecto()
    {
        using var carpeta = new CarpetaTemporal();

        Assert.Equal(new Configuracion(Gravedad.Fallo, 50), Configuracion.Leer(carpeta.Ruta));
    }

    [Fact]
    public void Lee_los_valores_del_fichero()
    {
        using var carpeta = new CarpetaTemporal();
        carpeta.Escribir(Configuracion.NombreFichero, """{ "gravedadQueFalla": "Advertencia", "minimoTokensDuplicado": 30 }""");

        Assert.Equal(new Configuracion(Gravedad.Advertencia, 30), Configuracion.Leer(carpeta.Ruta));
    }

    [Fact]
    public void Un_fichero_mal_escrito_da_un_error_que_lo_nombra()
    {
        using var carpeta = new CarpetaTemporal();
        carpeta.Escribir(Configuracion.NombreFichero, "{ gravedad");

        var e = Assert.Throws<InvalidOperationException>(() => Configuracion.Leer(carpeta.Ruta));
        Assert.Contains(Configuracion.NombreFichero, e.Message);
    }
}
