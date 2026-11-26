#!/usr/bin/env python3
# Saca las capturas del cuadro de mando para el manual, con Playwright. Abre el panel compilado desde file://, le
# carga los JSON de una carpeta de historial y fotografía cada parte. Las del manual salen de los dos análisis de la
# 1.4 que trae panel/pruebas/historial: el código antes de limpiarlo y después.
#
# Uso:  python capturar_panel.py ../../../panel/dist/index.html ../../../panel/pruebas/historial 1.4 ../capturas
import sys
from pathlib import Path

ANCHO = 760

# (fichero, análisis que se elige: 0 el primero, -1 el último, parte que se fotografía)
PARTES = [
    ("07-panel-cabecera", -1, "cabecera"),
    ("08-panel-evolucion", -1, "Evolución"),
    ("09-panel-reparto", 0, "reparto"),
    ("10-panel-funciones", 0, "Funciones"),
    ("11-panel-configuracion", 0, "Configuración"),
]


def caja(pagina, parte):
    if parte == "cabecera":
        arriba = pagina.locator(".cabecera").bounding_box()
        abajo = pagina.locator(".kpis").bounding_box()
        return {"x": 0, "y": arriba["y"] - 12, "width": ANCHO, "height": abajo["y"] + abajo["height"] - arriba["y"] + 24}
    if parte == "reparto":
        return pagina.locator(".rejilla.dos").bounding_box()
    return pagina.locator(".tarjeta", has=pagina.locator("h2", has_text=parte)).bounding_box()


def main(panel: Path, historial: Path, carpeta: str, salida: Path) -> None:
    from playwright.sync_api import sync_playwright

    ficheros = sorted(str(f) for f in historial.glob(f"{carpeta}-*.json"))
    salida.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as p:
        navegador = p.chromium.launch()
        for nombre, analisis, parte in PARTES:
            pagina = navegador.new_page(viewport={"width": ANCHO, "height": 900}, device_scale_factor=2, color_scheme="light")
            pagina.goto(panel.resolve().as_uri())
            pagina.locator("input[type=file][accept]").set_input_files(ficheros)
            pagina.wait_for_selector(".kpis")
            selector = pagina.locator(".cabecera select")
            selector.select_option(index=analisis if analisis >= 0 else len(ficheros) - 1)
            destino = salida / f"{nombre}.png"
            pagina.screenshot(path=str(destino), full_page=True, clip=caja(pagina, parte))
            print(destino.name)
            pagina.close()
        navegador.close()


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3], Path(sys.argv[4]))
