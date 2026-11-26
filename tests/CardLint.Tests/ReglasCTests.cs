using CardLint.Core;
using CardLint.Core.Analizadores;
using CardLint.Core.Analizadores.C;

namespace CardLint.Tests;

/// <summary>
/// Las reglas propias de C sobre <c>datos/reglas.c</c>, un fichero hecho para tocar cada una, y su
/// volcado real de Cppcheck 2.22. Las líneas se buscan por la marca que lleva cada caso en un comentario.
/// </summary>
public class ReglasCTests
{
    private static readonly string Fuente = Datos.Leer("reglas.c");
    private static readonly VolcadoC Volcado = VolcadoC.Leer(Datos.Leer("reglas.c.dump"), "c/src/reglas.c");
    private static readonly IReadOnlyList<Aviso> Avisos = ReglasC.Analizar([Volcado], new Umbrales());

    [Fact]
    public void La_cognitiva_cuenta_anidamiento_else_if_ternario_logicos_y_goto()
    {
        // if 1 · && y || 2 · else if 1 · ternario dentro del else if 2 · else 1 · for dentro del else 2
        // · if dentro del for 3 · goto 1 = 13
        Assert.Equal(13, ReglasC.Cognitiva(Volcado, Funcion("reglas_cognitiva")));
    }

    [Fact]
    public void La_ciclomatica_cuenta_cada_decision()
    {
        // 1 + tres if + for + && + || + ternario = 8
        Assert.Equal(8, ReglasC.Ciclomatica(Volcado, Funcion("reglas_cognitiva")));
    }

    [Fact]
    public void Mide_todas_las_funciones_aunque_no_pasen_de_los_umbrales()
    {
        var funciones = ReglasC.Medir([Volcado]);

        Assert.Equal(Volcado.FuncionesDefinidas().Count(), funciones.Count);
        var f = Assert.Single(funciones, x => x.Nombre == "reglas_cognitiva");
        Assert.Equal((MetricasFuncion.C, "c/src/reglas.c", Linea("int reglas_cognitiva("), 13, 8, 2, null),
            (f.Lenguaje, f.Fichero, f.Linea, f.Cognitiva, f.Ciclomatica, f.Parametros, f.Instrucciones));
        Assert.True(f.Lineas > 0);

        // Con 13 no llega al umbral de serie (15): se mide, pero no avisa.
        Assert.DoesNotContain(De("c:complejidad-cognitiva"), a => a.Mensaje.Contains("reglas_cognitiva"));
    }

    [Fact]
    public void Las_macros_con_parametros_avisan_salvo_las_de_sizeof()
    {
        var macro = Assert.Single(De("c:macro-con-parametros"));

        Assert.Equal(("c/src/reglas.c", Linea("#define CUADRADO")), (macro.Fichero, macro.Linea));
        Assert.Contains("CUADRADO", macro.Mensaje);
    }

    [Fact]
    public void Las_cabeceras_que_se_incluyen_en_circulo_avisan_en_las_dos()
    {
        Assert.Equal(["c/include/ciclo_a.h", "c/include/ciclo_b.h"], De("c:include-ciclico").Select(a => a.Fichero).Order());
    }

    [Fact]
    public void Una_variable_del_fichero_que_solo_usa_una_funcion_es_un_campo_temporal()
    {
        var aviso = Assert.Single(De("c:campo-temporal"));

        Assert.Equal(Linea("static int solo_una"), aviso.Linea);
        Assert.Contains("reglas_usa_global", aviso.Mensaje);
        Assert.Equal(Olores.CampoTemporal, aviso.Submodulo);
    }

    [Fact]
    public void Las_globales_de_otro_modulo_avisan_por_extern_en_el_c_y_por_uso()
    {
        var avisos = De("c:global-de-otro-modulo").ToList();

        Assert.Contains(avisos, a => a.Linea == Linea("extern int otro_global") && a.Mensaje.Contains("dentro de un .c"));
        Assert.Contains(avisos, a => a.Mensaje.Contains("modulo_estado del módulo modulo"));
    }

    [Fact]
    public void La_memoria_avisa_del_array_variable_y_de_malloc_y_free_pero_no_de_sizeof()
    {
        Assert.Equal([Linea("/* VLA */")], De("c:array-de-tamano-variable").Select(a => a.Linea));
        Assert.Equal([Linea("/* MALLOC */"), Linea("free(p)")], De("c:asignacion-dinamica").Select(a => a.Linea).Order());
    }

    [Fact]
    public void Una_cadena_de_tres_accesos_o_mas_es_una_cadena_de_mensajes()
    {
        Assert.Equal([Linea("/* CADENA */")], De("c:cadena-de-mensajes").Select(a => a.Linea));
    }

    [Fact]
    public void El_retorno_sin_comprobar_avisa_salvo_con_void()
    {
        Assert.Equal([Linea("/* SIN COMPROBAR */")], De("c:retorno-sin-comprobar").Select(a => a.Linea));
    }

    [Fact]
    public void Los_parametros_avisan_de_bandera_y_de_sin_usar()
    {
        var bandera = Assert.Single(De("c:parametro-bandera"));
        var sinUsar = Assert.Single(De("c:parametro-sin-usar"));

        Assert.Equal(Linea("/* BANDERA */"), bandera.Linea);
        Assert.Contains("con_algo", bandera.Mensaje);
        Assert.Equal(Linea("/* SIN USAR */"), sinUsar.Linea);
        Assert.Contains("ignorado", sinUsar.Mensaje);
    }

    [Fact]
    public void Una_funcion_publica_sin_prefijo_avisa()
    {
        var aviso = Assert.Single(De("c:sin-prefijo"));

        Assert.Contains("bandera", aviso.Mensaje);
    }

    [Fact]
    public void Tres_funciones_con_los_mismos_tres_parametros_son_un_grupo_de_datos()
    {
        Assert.Equal(3, De("c:grupo-de-datos").Count());
    }

    [Fact]
    public void El_mismo_switch_en_dos_funciones_se_repite()
    {
        var avisos = De("c:switch-repetido").ToList();

        Assert.Equal(2, avisos.Count);
        Assert.All(avisos, a => Assert.Contains("reglas_s1, reglas_s2", a.Mensaje));
    }

    [Fact]
    public void Los_numeros_magicos_salen_dentro_de_las_funciones_y_no_en_las_lineas_const()
    {
        var lineas = De("c:numero-magico").Select(a => a.Linea).ToList();

        Assert.Contains(Linea("/* MAGICO */"), lineas);
        Assert.DoesNotContain(Linea("/* CONST */"), lineas);
        Assert.DoesNotContain(Linea("#define CUADRADO"), lineas);
    }

    private static IEnumerable<Aviso> De(string regla) => Avisos.Where(a => a.Regla == regla);

    private static AmbitoC Funcion(string nombre) => Volcado.FuncionesDefinidas().Single(f => f.Funcion.Nombre == nombre).Ambito;

    private static int Linea(string marca) =>
        Fuente.Split('\n').Select((texto, i) => (texto, i)).First(x => x.texto.Contains(marca)).i + 1;
}

/// <summary>
/// Las reglas de C de verdad sobre card-code: lo que enseña cada submódulo sale en su «antes»
/// y desaparece en su «despues».
/// </summary>
public class ReglasCEnCardCodeTests
{
    [ConHerramientasTheory]
    [InlineData("submodulo-2.1/nombres", "c:sin-prefijo")]
    [InlineData("submodulo-2.2/funciones", "cppcheck:staticFunction")]
    [InlineData("submodulo-2.2/funciones", "c:parametro-bandera")]
    [InlineData("submodulo-2.3/cabeceras", "c:include-ciclico")]
    [InlineData("submodulo-2.4/macros-y-ensamblador", "c:macro-con-parametros")]
    [InlineData("submodulo-3.1/errores", "c:retorno-sin-comprobar")]
    [InlineData("submodulo-3.2/memoria", "c:array-de-tamano-variable")]
    [InlineData("submodulo-5.1/code-smells", "c:switch-repetido")]
    [InlineData("submodulo-5.1/code-smells", "c:parametro-bandera")]
    public void Sale_en_el_antes_y_no_en_el_despues(string rama, string regla)
    {
        var (antes, despues) = AntesYDespues(rama, new Umbrales());

        Assert.Contains(antes, a => a.Regla == regla);
        Assert.DoesNotContain(despues, a => a.Regla == regla);
    }

    [ConHerramientasFact]
    public void Las_metricas_de_la_1_2_dan_las_cifras_de_la_base()
    {
        var todas = new Umbrales(Cognitiva: 0, Ciclomatica: 0, LineasFuncion: 0);
        var (antes, despues) = AntesYDespues("submodulo-1.2/metricas", todas);

        Assert.Contains(antes, a => a.Mensaje == "Complejidad cognitiva de registro_escribir: 18 (umbral 0)");
        Assert.Contains(antes, a => a.Mensaje == "Complejidad ciclomática de registro_escribir: 6 (umbral 0)");
        Assert.Contains(antes, a => a.Mensaje == "registro_escribir tiene 23 líneas de código (umbral 0)");

        Assert.Contains(despues, a => a.Mensaje == "Complejidad cognitiva de registro_escribir: 3 (umbral 0)");
        Assert.Contains(despues, a => a.Mensaje == "Complejidad cognitiva de datos_grabar: 3 (umbral 0)");
        Assert.Contains(despues, a => a.Mensaje == "Complejidad ciclomática de registro_escribir: 4 (umbral 0)");
        Assert.Contains(despues, a => a.Mensaje == "Complejidad ciclomática de datos_grabar: 3 (umbral 0)");
        Assert.Contains(despues, a => a.Mensaje == "registro_escribir tiene 13 líneas de código (umbral 0)");
        Assert.Contains(despues, a => a.Mensaje == "datos_grabar tiene 9 líneas de código (umbral 0)");
    }

    private static (List<Aviso> Antes, List<Aviso> Despues) AntesYDespues(string rama, Umbrales umbrales)
    {
        using var dir = new CarpetaTemporal();
        CardCode.Extraer(rama, dir.Ruta, "antes", "despues");
        var analizador = new CppcheckAnalizador(ConHerramientasFactAttribute.Herramientas.Cppcheck!, umbrales);
        return (Analizar(analizador, Path.Combine(dir.Ruta, "antes")), Analizar(analizador, Path.Combine(dir.Ruta, "despues")));
    }

    private static List<Aviso> Analizar(IAnalizador analizador, string carpeta)
    {
        using var trabajo = new CarpetaTemporal();
        return analizador.Analizar(carpeta, trabajo.Ruta).ToList();
    }
}
