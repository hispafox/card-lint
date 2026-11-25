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
