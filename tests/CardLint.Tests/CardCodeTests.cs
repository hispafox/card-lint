using CardLint.Core;

namespace CardLint.Tests;

/// <summary>Las contramedidas de verdad: las de la versión «despues» de la rama de la 3.5 de card-code.</summary>
public class CardCodeTests
{
    private const string Rama = "submodulo-3.5/codigo-redundante";

    [ConCardCodeFact]
    public void La_segunda_comprobacion_del_PIN_cubre_solo_su_if()
    {
        var codigo = CardCode.Leer(Rama, "despues/c/src/comando.c");

        var c = Assert.Single(LectorContramedidas.Leer("c/src/comando.c", codigo));

        Assert.Equal(new Contramedida("c/src/comando.c", 123, 125, 128, "fallos provocados"), c);
    }

    [ConCardCodeFact]
    public void La_comparacion_del_PIN_cubre_su_funcion()
    {
        var codigo = CardCode.Leer(Rama, "despues/c/src/pin.c");

        var c = Assert.Single(LectorContramedidas.Leer("c/src/pin.c", codigo));

        Assert.Equal((68, 73, "canal lateral"), (c.LineaComentario, c.Desde, c.Motivo));
        Assert.True(c.Hasta > c.Desde);
    }

    [ConCardCodeFact]
    public void El_modulo_de_seguridad_tiene_sus_dos_contramedidas()
    {
        var codigo = CardCode.Leer(Rama, "despues/c/src/seguridad.c");

        var contramedidas = LectorContramedidas.Leer("c/src/seguridad.c", codigo);

        Assert.Equal(["canal lateral", "datos sensibles en memoria"], contramedidas.Select(c => c.Motivo));
        Assert.All(contramedidas, c => Assert.Equal(c.LineaComentario + 1, c.Desde));
    }
}
