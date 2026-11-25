using CardLint.Core;

namespace CardLint.Tests;

public class LectorContramedidasTests
{
    [Fact]
    public void Antes_de_una_funcion_cubre_la_funcion_entera()
    {
        const string codigo = """
            #include "seguridad.h"

            /* CONTRAMEDIDA (canal lateral): recorre siempre todos los bytes. */
            uint8_t seguridad_diferencia(const uint8_t *a, const uint8_t *b, size_t n)
            {
                uint8_t diferencia = 0u;
                for (size_t i = 0u; i < n; i++) {
                    diferencia |= (uint8_t)(a[i] ^ b[i]);
                }
                return diferencia;
            }

            void tarjeta_bloquear(void)
            {
                pin_bloquear();
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("src/seguridad.c", codigo));

        Assert.Equal(new Contramedida("src/seguridad.c", 3, 4, 11, "canal lateral"), c);
        Assert.False(c.Cubre("src/seguridad.c", 15));
    }

    [Fact]
    public void Un_comentario_de_varias_lineas_tambien_vale()
    {
        const string codigo = """
            /*
             * CONTRAMEDIDA (canal lateral): cero solo si coinciden.
             * El tiempo solo depende de la longitud.
             */
            static uint8_t pin_diferencia(const uint8_t *pin, uint8_t longitud)
            {
                return 0u;
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("pin.c", codigo));

        Assert.Equal(new Contramedida("pin.c", 1, 5, 8, "canal lateral"), c);
    }

    [Fact]
    public void Dentro_de_una_funcion_cubre_solo_la_instruccion_siguiente()
    {
        const string codigo = """
            uint16_t comando_verificar(void)
            {
                uint8_t resultado = pin_verificar();
                if (resultado != PIN_CORRECTO) {
                    return SW_ERROR;
                }
                /* CONTRAMEDIDA (fallos provocados): segunda comprobación, a propósito.
                 * Requisito de seguridad del producto. */
                if (resultado != PIN_CORRECTO) {
                    tarjeta_bloquear();
                    return SW_ERROR;
                }
                return SW_OK;
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("comando.c", codigo));

        Assert.Equal(new Contramedida("comando.c", 7, 9, 12, "fallos provocados"), c);
        Assert.False(c.Cubre("comando.c", 4), "la primera comprobación no está marcada");
        Assert.False(c.Cubre("comando.c", 13));
    }

    [Fact]
    public void Con_saltos_de_linea_de_Windows_cuenta_igual_las_lineas()
    {
        const string codigo = "void f(void)\r\n{\r\n    /* CONTRAMEDIDA (fallos provocados): x */\r\n    if (a) {\r\n        b();\r\n    }\r\n    c();\r\n}\r\n";

        var c = Assert.Single(LectorContramedidas.Leer("f.c", codigo));

        Assert.Equal((3, 4, 6), (c.LineaComentario, c.Desde, c.Hasta));
    }

    [Fact]
    public void Un_if_con_else_cubre_las_dos_ramas()
    {
        const string codigo = """
            void f(void)
            {
                /* CONTRAMEDIDA (fallos provocados): repetida. */
                if (a) {
                    x();
                } else {
                    y();
                }
                z();
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("f.c", codigo));

        Assert.Equal((4, 8), (c.Desde, c.Hasta));
    }

    [Fact]
    public void Un_do_while_cubre_hasta_su_punto_y_coma()
    {
        const string codigo = """
            void f(void)
            {
                /* CONTRAMEDIDA (fallos provocados): bucle repetido. */
                do {
                    x();
                } while (y());
                z();
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("f.c", codigo));

        Assert.Equal((4, 6), (c.Desde, c.Hasta));
    }

    [Fact]
    public void Una_instruccion_sin_llaves_cubre_su_linea_y_vale_con_comentario_de_linea()
    {
        const string codigo = """
            void f(void)
            {
                // CONTRAMEDIDA (datos sensibles en memoria): borrado que no se puede quitar.
                seguridad_borrar(clave, sizeof clave);
                g();
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("f.c", codigo));

        Assert.Equal(new Contramedida("f.c", 3, 4, 4, "datos sensibles en memoria"), c);
    }

    [Fact]
    public void Antes_de_una_declaracion_global_cubre_hasta_su_punto_y_coma()
    {
        const string codigo = """
            /* CONTRAMEDIDA (datos sensibles en memoria): volatile para que no se quite. */
            static volatile uint8_t barrera;
            int h(void) { return 0; }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("g.c", codigo));

        Assert.Equal((2, 2), (c.Desde, c.Hasta));
    }

    [Fact]
    public void Antes_de_una_directiva_cubre_la_directiva_y_no_lo_que_viene_despues()
    {
        // Como en seguridad.h de la 3.5: la barrera es una macro, y la siguiente contramedida es de la declaración.
        const string codigo = """
            /*
             * CONTRAMEDIDA (fallos provocados): la barrera.
             */
            #define SEGURIDAD_BARRERA(v) \
                __asm__ __volatile__("" : "+r"(v) : : "memory")

            /* CONTRAMEDIDA (canal lateral): cero solo si coinciden. */
            uint8_t seguridad_diferencia(const uint8_t *a, const uint8_t *b, size_t n);
            """;

        var contramedidas = LectorContramedidas.Leer("seguridad.h", codigo);

        Assert.Equal(
            [new Contramedida("seguridad.h", 1, 4, 5, "fallos provocados"), new Contramedida("seguridad.h", 7, 8, 8, "canal lateral")],
            contramedidas);
    }

    [Fact]
    public void Sin_parentesis_el_motivo_queda_vacio()
    {
        const string codigo = """
            /* CONTRAMEDIDA: sin decir contra qué. */
            int k;
            """;

        Assert.Equal("", Assert.Single(LectorContramedidas.Leer("k.c", codigo)).Motivo);
    }

    [Fact]
    public void Nombrar_la_palabra_en_medio_de_un_comentario_no_es_una_contramedida()
    {
        const string codigo = """
            /* Ver la CONTRAMEDIDA de comando.c. */
            void f(void) { }
            """;

        Assert.Empty(LectorContramedidas.Leer("f.c", codigo));
    }

    [Fact]
    public void Las_llaves_de_cadenas_caracteres_y_preprocesador_no_cuentan()
    {
        const string codigo = """
            #define ABRE {
            #define LARGA(x) \
                do { x; } while (0)
            static const char *texto = "}{";
            static const char llave = '}';
            /* CONTRAMEDIDA (canal lateral): comparación en tiempo constante. */
            int g(void)
            {
                return 1;
            }
            """;

        var c = Assert.Single(LectorContramedidas.Leer("g.c", codigo));

        Assert.Equal((7, 10), (c.Desde, c.Hasta));
    }

    [Fact]
    public void Separar_aparta_los_avisos_que_caen_en_una_contramedida()
    {
        var contramedida = new Contramedida("src/comando.c", 7, 9, 12, "fallos provocados");
        var dentro = new Aviso("condicion-repetida", Gravedad.Fallo, "src/comando.c", 9, "Condición repetida");
        var antes = new Aviso("condicion-repetida", Gravedad.Fallo, "src/comando.c", 4, "Condición repetida");
        var otroFichero = new Aviso("condicion-repetida", Gravedad.Fallo, "src/pin.c", 10, "Condición repetida");
        var conBarras = new Aviso("condicion-repetida", Gravedad.Fallo, @"src\comando.c", 12, "Condición repetida");

        var (sinJustificar, justificados) = LectorContramedidas.Separar([dentro, antes, otroFichero, conBarras], [contramedida]);

        Assert.Equal([antes, otroFichero], sinJustificar);
        Assert.Equal([dentro, conBarras], justificados.Select(j => j.Aviso));
        Assert.All(justificados, j => Assert.Same(contramedida, j.Contramedida));
    }
}
