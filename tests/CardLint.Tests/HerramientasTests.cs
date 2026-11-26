using CardLint.Core;
using CardLint.Core.Analizadores;

namespace CardLint.Tests;

/// <summary>
/// Cppcheck, PMD y CPD de verdad sobre las ramas de card-code: lo que enseña el submódulo
/// tiene que salir en la versión «antes» y desaparecer en la «despues».
/// </summary>
public class HerramientasTests
{
    private static Herramientas H => ConHerramientasFactAttribute.Herramientas;

    [ConHerramientasFact]
    public void Cppcheck_ve_el_comportamiento_indefinido_de_la_3_4_y_no_toca_la_carpeta()
    {
        using var rama = new CarpetaTemporal();
        CardCode.Extraer("submodulo-3.4/comportamiento-indefinido", rama.Ruta, "antes", "despues");
        var analizador = new CppcheckAnalizador(H.Cppcheck!);

        var antes = Analizar(analizador, Path.Combine(rama.Ruta, "antes"));
        var despues = Analizar(analizador, Path.Combine(rama.Ruta, "despues"));

        Assert.Contains(antes, a => a.Regla == "cppcheck:invalidTestForOverflow" && a.Fichero == "c/src/ejemplos.c" && a.Linea == 7);
        Assert.Contains(antes, a => a.Mensaje.StartsWith("Función que solo usan las pruebas", StringComparison.Ordinal));
        Assert.DoesNotContain(despues, a => a.Submodulo == "3.4");
        Assert.Empty(Directory.GetFiles(rama.Ruta, "*.dump", SearchOption.AllDirectories));
    }

    [ConHerramientasFact]
    public void Pmd_ve_que_la_4_4_baja_la_complejidad_del_process()
    {
        using var rama = new CarpetaTemporal();
        CardCode.Extraer("submodulo-4.4/procesado-apdu", rama.Ruta, "antes", "despues");
        var analizador = new PmdAnalizador(H.Pmd!);

        var antes = Analizar(analizador, Path.Combine(rama.Ruta, "antes"));
        var despues = Analizar(analizador, Path.Combine(rama.Ruta, "despues"));

        var cognitiva = Assert.Single(antes, a => a.Regla == "pmd:CognitiveComplexity");
        Assert.Equal(("javacard/src/cardcode/tarjeta/TarjetaApplet.java", 30), (cognitiva.Fichero, cognitiva.Linea));
        Assert.DoesNotContain(despues, a => a.Regla == "pmd:CognitiveComplexity");
    }

    [ConHerramientasFact]
    public void Cppcheck_y_Pmd_miden_todas_las_funciones_aunque_no_pasen_de_los_umbrales()
    {
        using var c = new CarpetaTemporal();
        using var java = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        CardCode.Extraer("submodulo-1.2/metricas", c.Ruta, "antes");
        CardCode.Extraer("submodulo-4.1/lo-que-cambia", java.Ruta, "antes");

        var deC = Analisis.Ejecutar(Path.Combine(c.Ruta, "antes"), new Configuracion(), [new CppcheckAnalizador(H.Cppcheck!)], trabajo.Ruta).Funciones!;
        var analizador = new PmdAnalizador(H.Pmd!);
        var avisosJava = Analizar(analizador, Path.Combine(java.Ruta, "antes"));

        // 23 líneas en el antes de registro_escribir, como las cuenta la 1.2; no llega al umbral de 40.
        var registro = Assert.Single(deC, f => f.Nombre == "registro_escribir");
        Assert.Equal((MetricasFuncion.C, 23), (registro.Lenguaje, registro.Lineas));
        var process = Assert.Single(analizador.Funciones, f => f.Nombre == "process(APDU)");
        Assert.Equal((31, 40, 83), (process.Cognitiva, process.Ciclomatica, process.Instrucciones));
        Assert.Contains(analizador.Funciones, f => f.Ciclomatica < PmdAnalizador.UmbralCiclomatica);
        Assert.DoesNotContain(avisosJava, a => a.Regla == "pmd:CyclomaticComplexity" && !a.Mensaje.Contains("process(APDU)"));
    }

    [ConHerramientasFact]
    public void Cpd_encuentra_una_funcion_copiada_en_dos_ficheros()
    {
        using var carpeta = new CarpetaTemporal();
        const string funcion = """
            int sumar_saldo(int saldo, int importe, int limite)
            {
                int total = saldo + importe;
                if (total > limite) {
                    total = limite;
                }
                if (total < 0) {
                    total = 0;
                }
                return total * 2 + importe - saldo;
            }
            """;
        carpeta.Escribir("c/src/uno.c", funcion);
        carpeta.Escribir("c/src/dos.c", funcion.Replace("sumar_saldo", "sumar_otro"));

        var avisos = Analizar(new CpdAnalizador(H.Pmd!, 30), carpeta.Ruta);

        Assert.Equal(["c/src/dos.c", "c/src/uno.c"], avisos.Select(a => a.Fichero).Order());
        Assert.All(avisos, a => Assert.Contains("igual que en", a.Mensaje));
    }

    [ConHerramientasFact]
    public void La_segunda_comprobacion_del_PIN_de_la_3_5_sale_como_justificada()
    {
        using var rama = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        CardCode.Extraer("submodulo-3.5/codigo-redundante", rama.Ruta, "despues");

        var r = Analisis.Ejecutar(Path.Combine(rama.Ruta, "despues"), new Configuracion(), [new CppcheckAnalizador(H.Cppcheck!)], trabajo.Ruta);

        var justificado = Assert.Single(r.Justificados, j => j.Aviso.Regla == "cppcheck:identicalConditionAfterEarlyExit");
        Assert.Equal(("c/src/comando.c", 125, "fallos provocados"), (justificado.Aviso.Fichero, justificado.Aviso.Linea, justificado.Contramedida.Motivo));
        Assert.DoesNotContain(r.Avisos, a => a.Regla == "cppcheck:identicalConditionAfterEarlyExit");
    }

    private static List<Aviso> Analizar(IAnalizador analizador, string carpeta)
    {
        using var trabajo = new CarpetaTemporal();
        return analizador.Analizar(carpeta, trabajo.Ruta).ToList();
    }
}
