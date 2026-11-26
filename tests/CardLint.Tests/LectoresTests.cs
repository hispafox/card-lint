using CardLint.Core;
using CardLint.Core.Analizadores;

namespace CardLint.Tests;

/// <summary>Los lectores de las tres herramientas, sobre salidas reales guardadas en <c>datos/</c>.</summary>
public class LectoresTests
{
    [Fact]
    public void Cppcheck_quita_los_mensajes_del_propio_analisis_y_traduce_los_conocidos()
    {
        var avisos = CppcheckAnalizador.Leer(Datos.Leer("cppcheck-3.4-antes.xml"));

        Assert.Equal(17, avisos.Count);
        Assert.DoesNotContain(avisos, a => a.Regla is "cppcheck:checkersReport" or "cppcheck:normalCheckLevelMaxBranches" or "cppcheck:unmatchedSuppression");

        var desbordamiento = Assert.Single(avisos, a => a.Regla == "cppcheck:invalidTestForOverflow");
        Assert.Equal(("c/src/ejemplos.c", 7, Gravedad.Advertencia, "3.4"), (desbordamiento.Fichero, desbordamiento.Linea, desbordamiento.Gravedad, desbordamiento.Submodulo));
        Assert.Equal("Comprobación de desbordamiento que el compilador puede quitar", desbordamiento.Mensaje);
        Assert.StartsWith("Invalid test for overflow", desbordamiento.Detalle);

        var sinInicializar = Assert.Single(avisos, a => a.Regla == "cppcheck:uninitvar");
        Assert.Equal(("c/src/registro.c", 102), (sinInicializar.Fichero, sinInicializar.Linea));
    }

    [Fact]
    public void Cppcheck_sube_el_codigo_muerto_a_advertencia_salvo_lo_que_usan_las_pruebas()
    {
        var avisos = CppcheckAnalizador.Leer(Datos.Leer("cppcheck-3.4-antes.xml"), funcion => funcion == "leer_primero");

        var muertas = avisos.Where(a => a.Regla == "cppcheck:unusedFunction").ToList();
        Assert.Equal(13, muertas.Count);
        Assert.Contains(muertas, a => a.Mensaje == "Función sin usar: comando_procesar" && a.Gravedad == Gravedad.Advertencia);

        var deLasPruebas = Assert.Single(muertas, a => a.Mensaje.Contains("leer_primero"));
        Assert.Equal(("Función que solo usan las pruebas: leer_primero", Gravedad.Informacion), (deLasPruebas.Mensaje, deLasPruebas.Gravedad));
    }

    [Fact]
    public void Pmd_traduce_las_metricas_con_sus_cifras()
    {
        var avisos = PmdAnalizador.Leer(Datos.Leer("pmd-4.2-antes.xml"));

        Assert.Equal(3, avisos.Count);
        Assert.All(avisos, a => Assert.Equal(("cardcode/tarjeta/TarjetaApplet.java", 28, Gravedad.Advertencia), (a.Fichero, a.Linea, a.Gravedad)));
        Assert.Contains(avisos, a => a.Regla == "pmd:CognitiveComplexity" && a.Mensaje == "Complejidad cognitiva de process(APDU): 31 (umbral 15)" && a.Submodulo == "1.2");
        Assert.Contains(avisos, a => a.Regla == "pmd:CyclomaticComplexity" && a.Mensaje == "Complejidad ciclomática de process(APDU): 26");
        Assert.Contains(avisos, a => a.Regla == "pmd:NcssCount" && a.Mensaje == "process(APDU) tiene 77 instrucciones (NCSS)");
    }

    [Fact]
    public void Pmd_mide_todos_los_metodos_aunque_no_lleguen_al_umbral()
    {
        var funciones = PmdAnalizador.Medir(Datos.Leer("pmd-4.2-antes.xml"));

        Assert.Equal(13, funciones.Count);
        Assert.All(funciones, f => Assert.Equal((MetricasFuncion.JavaCard, null, null), (f.Lenguaje, f.Lineas, f.Parametros)));
        var process = Assert.Single(funciones, f => f.Nombre == "process(APDU)");
        Assert.Equal(("cardcode/tarjeta/TarjetaApplet.java", 28, 31, 26, 77),
            (process.Fichero, process.Linea, process.Cognitiva, process.Ciclomatica, process.Instrucciones));

        // PMD no da la cognitiva de un método sin rupturas del flujo: es 0.
        var constructor = Assert.Single(funciones, f => f.Nombre == "Limite(short)");
        Assert.Equal((0, 1, 2), (constructor.Cognitiva, constructor.Ciclomatica, constructor.Instrucciones));
    }

    [Fact]
    public void Pmd_avisa_desde_el_umbral_de_serie_y_no_confunde_los_numeros_del_nombre()
    {
        // Mensajes reales de PMD 7.29 con el umbral a 1, en la frontera de cada umbral de serie.
        var xml = InformePmd(
            ("CyclomaticComplexity", "The method 'paso2(short)' has a cyclomatic complexity of 9."),
            ("CyclomaticComplexity", "The method 'paso3(short)' has a cyclomatic complexity of 10."),
            ("CognitiveComplexity", "The method 'cuenta4(short)' has a cognitive complexity of 14, current threshold is 1"),
            ("CognitiveComplexity", "The method 'cuenta5(short)' has a cognitive complexity of 15, current threshold is 1"),
            ("NcssCount", "The method 'suma6(short)' has a NCSS line count of 59."),
            ("NcssCount", "The method 'suma7(short)' has a NCSS line count of 60."),
            ("CyclomaticComplexity", "The class 'Frontera' has a total cyclomatic complexity of 82 (highest 17)."));

        var avisos = PmdAnalizador.Leer(xml);
        var funciones = PmdAnalizador.Medir(xml);

        Assert.Equal(
        [
            "Complejidad ciclomática de paso3(short): 10",
            "Complejidad cognitiva de cuenta5(short): 15 (umbral 15)",
            "suma7(short) tiene 60 instrucciones (NCSS)",
            "Complejidad ciclomática de Frontera: 82",
        ], avisos.Select(a => a.Mensaje));
        Assert.Equal(["paso2(short)", "paso3(short)", "cuenta4(short)", "cuenta5(short)", "suma6(short)", "suma7(short)"], funciones.Select(f => f.Nombre));
    }

    /// <summary>Un informe de PMD con un fichero y una violación por línea, en el orden en que se dan.</summary>
    private static string InformePmd(params (string Regla, string Mensaje)[] violaciones)
    {
        var filas = violaciones.Select((v, i) =>
            $"<violation beginline=\"{i + 1}\" endline=\"{i + 1}\" rule=\"{v.Regla}\" ruleset=\"Design\" priority=\"3\">{v.Mensaje}</violation>");
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <pmd xmlns="http://pmd.sourceforge.net/report/2.0.0" version="7.29.0">
            <file name="p\Frontera.java">
            {string.Join('\n', filas)}
            </file>
            </pmd>
            """;
    }

    [Fact]
    public void Cpd_da_un_aviso_por_aparicion_con_la_otra_al_lado()
    {
        const string carpeta = @"F:\w\repos\card-lint\herramientas\_muestras\submodulo-5.1\antes";

        var avisos = CpdAnalizador.Leer(Datos.Leer("cpd-5.1-antes.xml"), carpeta);

        Assert.Equal(2, avisos.Count);
        Assert.Equal([36, 45], avisos.Select(a => a.Linea));
        Assert.All(avisos, a => Assert.Equal("c/pruebas/prueba_extracto.c", a.Fichero));
        Assert.Equal("Código duplicado: 4 líneas (39 tokens), igual que en c/pruebas/prueba_extracto.c:45", avisos[0].Mensaje);
    }
}
