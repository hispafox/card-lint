using System.Globalization;
using System.Net;
using System.Text;

namespace CardLint.Core;

/// <summary>Un solo HTML con el estilo dentro: se abre en el navegador sin servidor ni conexión.</summary>
public static class InformeHtml
{
    private const string Estilo = """
        body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; color: #1f2328; max-width: 70rem; }
        h1 { margin-bottom: .2rem; }
        .datos { color: #59636e; margin-top: 0; }
        .gate { display: inline-block; padding: .4rem .9rem; border-radius: .4rem; font-weight: 600; color: #fff; }
        .pasa { background: #1a7f37; }
        .falla { background: #cf222e; }
        table { border-collapse: collapse; width: 100%; margin: .5rem 0 1.5rem; }
        th, td { border-bottom: 1px solid #d1d9e0; padding: .35rem .5rem; text-align: left; vertical-align: top; overflow-wrap: anywhere; }
        th { background: #f6f8fa; overflow-wrap: normal; white-space: nowrap; }
        code { font-family: Consolas, monospace; }
        .nota { color: #59636e; }
        .detalle { color: #59636e; font-size: .85em; margin-top: .2rem; }
        """;

    public static string Generar(ResultadoAnalisis resultado, DateTimeOffset fecha)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"es\">");
        html.AppendLine("<head><meta charset=\"utf-8\"><title>card-lint · informe</title>");
        html.AppendLine($"<style>{Estilo}</style></head>");
        html.AppendLine("<body>");
        html.AppendLine("<h1>card-lint · informe</h1>");
        html.AppendLine($"<p class=\"datos\">{E(resultado.Carpeta)} · {fecha.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)}</p>");

        var (clase, texto) = resultado.GatePasa ? ("pasa", "Quality gate: pasa") : ("falla", "Quality gate: no pasa");
        html.AppendLine($"<p><span class=\"gate {clase}\">{texto}</span></p>");
        html.AppendLine($"<p>{resultado.Avisos.Count} avisos sin justificar · {resultado.Justificados.Count} justificados · " +
                        $"falla a partir de la gravedad «{NombreGravedad(resultado.GravedadQueFalla)}».</p>");

        html.AppendLine("<h2>Analizadores</h2>");
        html.AppendLine(resultado.Analizadores.Count == 0
            ? "<p class=\"nota\">No se ha ejecutado ningún analizador: en este análisis solo se leen las contramedidas.</p>"
            : $"<p>{E(string.Join(" · ", resultado.Analizadores))}</p>");
        foreach (var nota in resultado.Notas ?? [])
        {
            html.AppendLine($"<p class=\"nota\">{E(nota)}</p>");
        }

        if (resultado.Avisos.Count > 0)
        {
            html.AppendLine("<h2>Resumen por regla</h2>");
            html.AppendLine("<table><tr><th>Regla</th><th>Gravedad</th><th>Avisos</th><th>Se explica en</th></tr>");
            foreach (var g in resultado.Avisos.GroupBy(a => a.Regla)
                         .OrderByDescending(g => g.Max(a => a.Gravedad)).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                html.AppendLine($"<tr><td>{E(g.Key)}</td><td>{NombreGravedad(g.Max(a => a.Gravedad))}</td><td>{g.Count()}</td>" +
                                $"<td>{E(string.Join(" · ", g.Select(a => a.Submodulo).Where(s => !string.IsNullOrEmpty(s)).Distinct()))}</td></tr>");
            }

            html.AppendLine("</table>");
        }

        html.AppendLine("<h2>Avisos sin justificar</h2>");
        if (resultado.Avisos.Count == 0)
        {
            html.AppendLine("<p class=\"nota\">Ninguno.</p>");
        }
        else
        {
            html.AppendLine("<table><tr><th>Gravedad</th><th>Regla</th><th>Dónde</th><th>Mensaje</th><th>Se explica en</th></tr>");
            foreach (var a in Ordenar(resultado.Avisos))
            {
                html.AppendLine($"<tr><td>{NombreGravedad(a.Gravedad)}</td><td>{E(a.Regla)}</td><td><code>{E(a.Fichero)}:{a.Linea}</code></td>" +
                                $"<td>{E(a.Mensaje)}{Detalle(a)}</td><td>{E(a.Submodulo ?? "")}</td></tr>");
            }

            html.AppendLine("</table>");
        }

        html.AppendLine("<h2>Avisos justificados por una contramedida</h2>");
        if (resultado.Justificados.Count == 0)
        {
            html.AppendLine("<p class=\"nota\">Ninguno.</p>");
        }
        else
        {
            html.AppendLine("<table><tr><th>Regla</th><th>Dónde</th><th>Mensaje</th><th>Contramedida</th></tr>");
            foreach (var j in resultado.Justificados.OrderBy(j => j.Aviso.Fichero, StringComparer.Ordinal).ThenBy(j => j.Aviso.Linea))
            {
                html.AppendLine($"<tr><td>{E(j.Aviso.Regla)}</td><td><code>{E(j.Aviso.Fichero)}:{j.Aviso.Linea}</code></td>" +
                                $"<td>{E(j.Aviso.Mensaje)}{Detalle(j.Aviso)}</td><td>{E(Describir(j.Contramedida))}</td></tr>");
            }

            html.AppendLine("</table>");
        }

        html.AppendLine("<h2>Contramedidas del código</h2>");
        if (resultado.Contramedidas.Count == 0)
        {
            html.AppendLine("<p class=\"nota\">No hay ningún comentario <code>CONTRAMEDIDA</code>.</p>");
        }
        else
        {
            html.AppendLine("<table><tr><th>Fichero</th><th>Líneas</th><th>Contra qué protege</th></tr>");
            foreach (var c in resultado.Contramedidas)
            {
                html.AppendLine($"<tr><td><code>{E(c.Fichero)}</code></td><td>{c.Desde}–{c.Hasta}</td><td>{E(c.Motivo)}</td></tr>");
            }

            html.AppendLine("</table>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static IEnumerable<Aviso> Ordenar(IEnumerable<Aviso> avisos) =>
        avisos.OrderByDescending(a => a.Gravedad).ThenBy(a => a.Fichero, StringComparer.Ordinal).ThenBy(a => a.Linea);

    /// <summary>El mensaje original de la herramienta, debajo y en pequeño, si dice algo más que el nuestro.</summary>
    private static string Detalle(Aviso a) =>
        string.IsNullOrEmpty(a.Detalle) || a.Detalle == a.Mensaje ? "" : $"<div class=\"detalle\">{E(a.Detalle)}</div>";

    private static string Describir(Contramedida c) =>
        c.Motivo.Length == 0 ? $"líneas {c.Desde}–{c.Hasta}" : $"{c.Motivo}, líneas {c.Desde}–{c.Hasta}";

    private static string NombreGravedad(Gravedad gravedad) => gravedad switch
    {
        Gravedad.Informacion => "información",
        Gravedad.Advertencia => "advertencia",
        Gravedad.Fallo => "fallo",
        _ => gravedad.ToString(),
    };

    private static string E(string texto) => WebUtility.HtmlEncode(texto);
}
