using CardLint.Core;
using CardLint.Core.Analizadores;

namespace CardLint.Tests;

/// <summary>Las reglas propias de Java Card del conjunto de PMD de card-lint, con PMD de verdad.</summary>
public class ReglasJavaCardTests
{
    private const string Applet = """
        package prueba;

        import java.util.List;

        public class Applet {
            private byte[] buffer = new byte[4];

            Applet() { buffer = new byte[8]; }

            public static void install(byte[] b, short o, byte l) { new Applet(); }

            public void process(Object apdu) {
                int contador = 1;
                long grande = 2;
                String texto = "hola";
                byte[] temporal = new byte[16];
                ISOException.throwIt((short) 0x6A82);
                switch (contador) { case 0x30: break; default: break; }
                fabrica().siguiente().ultimo();
            }
        }
        """;

    [ConHerramientasFact]
    public void Cada_regla_propia_avisa_en_su_linea_y_con_su_submodulo()
    {
        using var carpeta = new CarpetaTemporal();
        using var trabajo = new CarpetaTemporal();
        carpeta.Escribir("javacard/src/prueba/Applet.java", Applet);

        var avisos = new PmdAnalizador(ConHerramientasFactAttribute.Herramientas.Pmd!).Analizar(carpeta.Ruta, trabajo.Ruta).ToList();

        Assert.Equal([Linea("import java.util")], Lineas(avisos, "pmd:JavaCardBibliotecaEstandar"));
        Assert.Equal([Linea("int contador")], Lineas(avisos, "pmd:JavaCardInt"));
        Assert.Equal([Linea("long grande")], Lineas(avisos, "pmd:JavaCardTipoNoDisponible"));
        Assert.Equal([Linea("String texto"), Linea("String texto")], Lineas(avisos, "pmd:JavaCardCadena"));
        Assert.Equal([Linea("byte[] temporal")], Lineas(avisos, "pmd:JavaCardObjetoFueraDeInstalar"));
        Assert.Equal([Linea("throwIt"), Linea("case 0x30")], Lineas(avisos, "pmd:JavaCardCodigoComoNumero"));
        Assert.Equal([Linea("fabrica()")], Lineas(avisos, "pmd:JavaCardCadenaDeMensajes"));

        var tipo = Assert.Single(avisos, a => a.Regla == "pmd:JavaCardTipoNoDisponible");
        Assert.Equal((Gravedad.Fallo, "4.1", "Tipo que Java Card no tiene: usa short o byte"), (tipo.Gravedad, tipo.Submodulo, tipo.Mensaje));

        // Con tildes, y sin el texto de PMD debajo, que llega con la codificación estropeada.
        var objeto = Assert.Single(avisos, a => a.Regla == "pmd:JavaCardObjetoFueraDeInstalar");
        Assert.Equal("Objeto creado fuera de la instalación: en Java Card se crean en el constructor o en install", objeto.Mensaje);
        Assert.Null(objeto.Detalle);
    }

    [ConHerramientasTheory]
    [InlineData("submodulo-4.1/lo-que-cambia", "pmd:JavaCardInt")]
    [InlineData("submodulo-4.1/lo-que-cambia", "pmd:JavaCardCadena")]
    [InlineData("submodulo-4.1/lo-que-cambia", "pmd:JavaCardBibliotecaEstandar")]
    [InlineData("submodulo-4.1/lo-que-cambia", "pmd:JavaCardObjetoFueraDeInstalar")]
    [InlineData("submodulo-4.4/procesado-apdu", "pmd:JavaCardCodigoComoNumero")]
    public void Sale_en_el_antes_y_no_en_el_despues(string rama, string regla)
    {
        using var dir = new CarpetaTemporal();
        CardCode.Extraer(rama, dir.Ruta, "antes", "despues");
        var analizador = new PmdAnalizador(ConHerramientasFactAttribute.Herramientas.Pmd!);

        Assert.Contains(Analizar(analizador, Path.Combine(dir.Ruta, "antes")), a => a.Regla == regla);
        Assert.DoesNotContain(Analizar(analizador, Path.Combine(dir.Ruta, "despues")), a => a.Regla == regla);
    }

    private static List<Aviso> Analizar(IAnalizador analizador, string carpeta)
    {
        using var trabajo = new CarpetaTemporal();
        return analizador.Analizar(carpeta, trabajo.Ruta).ToList();
    }

    private static List<int> Lineas(IEnumerable<Aviso> avisos, string regla) =>
        avisos.Where(a => a.Regla == regla).Select(a => a.Linea).Order().ToList();

    private static int Linea(string marca) =>
        Applet.Split('\n').Select((texto, i) => (texto, i)).First(x => x.texto.Contains(marca)).i + 1;
}
