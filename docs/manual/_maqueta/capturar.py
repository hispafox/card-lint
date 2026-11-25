#!/usr/bin/env python3
# Saca las capturas del manual de un informe de card-lint ya generado, sección a sección, con Playwright.
# El informe lo genera quien usa card-lint (por ejemplo, el del análisis de ejemplo de informes/); este script solo
# abre el HTML en un Chromium sin ventana y fotografía cada sección. El ancho va pensado para la página de 150 mm
# de la maqueta: con 760 píxeles, la letra del informe se lee en el PDF.
#
# Uso:  python capturar.py ../informes/informe-ejemplo.html ../capturas
import sys
from pathlib import Path

SECCIONES = [
    # (fichero, título de la sección en el informe o None para la cabecera, ancho, alto máximo o 0 sin límite)
    # Las tablas de cinco y cuatro columnas van más anchas, para que los nombres de las reglas no se partan.
    ("01-cabecera", None, 760, 0),
    ("02-analizadores", "Analizadores", 760, 0),
    ("03-resumen", "Resumen por regla", 760, 0),
    ("04-avisos", "Avisos sin justificar", 1040, 900),
    ("05-justificados", "Avisos justificados por una contramedida", 1040, 0),
    ("06-contramedidas", "Contramedidas del código", 760, 0),
]

# El estilo de los encabezados de tabla de la versión actual de card-lint (no parten línea), por si el informe
# se generó con una anterior.
ESTILO = "body { margin: 1rem 1.2rem; } th { overflow-wrap: normal; white-space: nowrap; }"

# Caja de una sección: de su h2 al siguiente h2 (o al final). La cabecera va del h1 al primer h2.
CAJA_JS = """
([titulo]) => {
  const h2 = [...document.querySelectorAll('h2')];
  let inicio, fin;
  if (titulo === null) {
    inicio = document.querySelector('h1');
    fin = h2[0];
  } else {
    const i = h2.findIndex(h => h.textContent.trim() === titulo);
    if (i < 0) return null;
    inicio = h2[i];
    fin = h2[i + 1] || null;
  }
  const arriba = inicio.getBoundingClientRect().top + window.scrollY - 8;
  const abajo = fin ? fin.getBoundingClientRect().top + window.scrollY - 12 : document.body.scrollHeight;
  return { y: arriba, alto: abajo - arriba };
}
"""


def main(informe: Path, carpeta: Path) -> None:
    from playwright.sync_api import sync_playwright

    carpeta.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as p:
        navegador = p.chromium.launch()
        for nombre, titulo, ancho, maximo in SECCIONES:
            pagina = navegador.new_page(viewport={"width": ancho, "height": 900}, device_scale_factor=2)
            pagina.goto(informe.resolve().as_uri())
            # Menos margen que en pantalla: en el PDF la captura ya va dentro de un marco.
            pagina.add_style_tag(content=ESTILO)
            caja = pagina.evaluate(CAJA_JS, [titulo])
            if caja is None:
                print(f"sin sección: {titulo}")
                continue
            alto = min(caja["alto"], maximo) if maximo else caja["alto"]
            destino = carpeta / f"{nombre}.png"
            pagina.screenshot(path=str(destino), full_page=True, clip={"x": 0, "y": caja["y"], "width": ancho, "height": alto})
            print(f"{destino.name}: {ancho} x {int(alto)} px")
            pagina.close()
        navegador.close()


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
