using CardLint.Cli;
using CardLint.Core;

namespace CardLint.Tests;

public class InformeHtmlTests
{
    private static readonly DateTimeOffset Fecha = new(2026, 11, 25, 19, 30, 0, TimeSpan.FromHours(1));

    private static ResultadoAnalisis Resultado(IReadOnlyList<string> analizadores, params Aviso[] avisos) =>
        new("F:/repo", analizadores, avisos, [], [new Contramedida("c/src/pin.c", 68, 73, 90, "canal lateral")], Gravedad.Fallo);

    [Fact]
    public void Escapa_el_texto_de_los_avisos()
    {
        var aviso = new Aviso("regla", Gravedad.Advertencia, "c/src/a.c", 3, "<script>alert(1)</script>");

        var html = InformeHtml.Generar(Resultado(["fijo"], aviso), Fecha);

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Dice_si_el_gate_no_pasa_y_lista_las_contramedidas()
    {
        var fallo = new Aviso("regla", Gravedad.Fallo, "c/src/a.c", 3, "Algo");

        var html = InformeHtml.Generar(Resultado(["fijo"], fallo), Fecha);

        Assert.Contains("Quality gate: no pasa", html);
        Assert.Contains("canal lateral", html);
        Assert.Contains("73–90", html);
    }

    [Fact]
    public void Avisa_cuando_no_se_ha_ejecutado_ningun_analizador_y_cuenta_las_notas()
    {
        var resultado = Resultado([]) with { Notas = ["No se ha encontrado Cppcheck: el código C no se ha analizado."] };

        var html = InformeHtml.Generar(resultado, Fecha);

        Assert.Contains("Quality gate: pasa", html);
        Assert.Contains("No se ha ejecutado ningún analizador", html);
        Assert.Contains("No se ha encontrado Cppcheck", html);
    }

    [Fact]
    public void Resume_los_avisos_por_regla_con_el_mas_grave_primero()
    {
        var html = InformeHtml.Generar(Resultado(["fijo"],
            new Aviso("c:numero-magico", Gravedad.Informacion, "c/src/a.c", 1, "x", "2.1"),
            new Aviso("c:numero-magico", Gravedad.Informacion, "c/src/a.c", 2, "y", "2.1"),
            new Aviso("pmd:JavaCardInt", Gravedad.Advertencia, "j/A.java", 3, "z", "4.1")), Fecha);

        var resumen = html[html.IndexOf("Resumen por regla", StringComparison.Ordinal)..html.IndexOf("Avisos sin justificar</h2>", StringComparison.Ordinal)];
        Assert.True(resumen.IndexOf("pmd:JavaCardInt", StringComparison.Ordinal) < resumen.IndexOf("c:numero-magico", StringComparison.Ordinal));
        Assert.Contains("<td>c:numero-magico</td><td>información</td><td>2</td><td>2.1</td>", resumen);
    }

    [Fact]
    public void Ensena_el_mensaje_original_de_la_herramienta_debajo_del_nuestro()
    {
        var aviso = new Aviso("cppcheck:uninitvar", Gravedad.Advertencia, "c/src/a.c", 3, "Variable que se usa sin inicializar", "3.4", "Uninitialized variable: v");

        var html = InformeHtml.Generar(Resultado(["Cppcheck"], aviso), Fecha);

        Assert.Contains("<div class=\"detalle\">Uninitialized variable: v</div>", html);
    }
}

public class ProgramaTests
{
    /// <summary>Sin herramientas, para que estas pruebas no lancen Cppcheck ni PMD.</summary>
    private static readonly Herramientas Ninguna = new(null, null);

    [Fact]
    public void Sin_argumentos_explica_el_uso()
    {
        var error = new StringWriter();

        Assert.Equal(Programa.ErrorDeUso, Programa.Ejecutar([], TextWriter.Null, error, Ninguna));
        Assert.Contains("Uso:", error.ToString());
    }

    [Fact]
    public void Una_opcion_desconocida_es_error_de_uso()
    {
        using var carpeta = new CarpetaTemporal();

        Assert.Equal(Programa.ErrorDeUso, Programa.Ejecutar(["analizar", carpeta.Ruta, "--rapido"], TextWriter.Null, TextWriter.Null, Ninguna));
    }

    [Fact]
    public void Una_carpeta_que_no_existe_es_error_de_uso()
    {
        var error = new StringWriter();

        Assert.Equal(Programa.ErrorDeUso, Programa.Ejecutar(["analizar", "no-existe-esta-carpeta"], TextWriter.Null, error, Ninguna));
        Assert.Contains("No existe la carpeta", error.ToString());
    }

    [Fact]
    public void Un_card_lint_json_mal_escrito_es_error_de_uso()
    {
        using var carpeta = new CarpetaTemporal();
        carpeta.Escribir(Configuracion.NombreFichero, "{ gravedad");

        var codigo = Programa.Ejecutar(["analizar", carpeta.Ruta, "--salida", Path.Combine(carpeta.Ruta, "i.html")], TextWriter.Null, TextWriter.Null, Ninguna);

        Assert.Equal(Programa.ErrorDeUso, codigo);
    }

    [Fact]
    public void Analiza_la_carpeta_escribe_el_informe_y_no_deja_la_carpeta_de_trabajo()
    {
        using var carpeta = new CarpetaTemporal();
        using var salidaInforme = new CarpetaTemporal();
        carpeta.Escribir("c/src/seguridad.c", "/* CONTRAMEDIDA (canal lateral): x */\nint a;\n");
        var informe = Path.Combine(salidaInforme.Ruta, "informe.html");
        var salida = new StringWriter();

        var codigo = Programa.Ejecutar(["analizar", carpeta.Ruta, "--salida", informe], salida, TextWriter.Null, Ninguna);

        Assert.Equal(Programa.GatePasa, codigo);
        Assert.Contains("contramedidas: 1", salida.ToString());
        Assert.Contains("No se ha encontrado Cppcheck", salida.ToString());
        Assert.Contains("canal lateral", File.ReadAllText(informe));
        Assert.Equal([informe], Directory.GetFileSystemEntries(salidaInforme.Ruta));
    }
}
