# card-lint

Analizador de código C y Java Card para el curso de código limpio: olores, métricas, contramedidas y un quality gate, en un informe HTML que se abre sin servidor y sin conexión. Usa Cppcheck y PMD, y les añade reglas propias.

- **Para usarlo:** [`docs/manual/manual-usuario.md`](docs/manual/manual-usuario.md), también en PDF.
- **Cómo está hecho y por qué:** [`docs/diseno.md`](docs/diseno.md).
- **Las herramientas que lleva dentro y sus licencias:** [`docs/licencias-herramientas.md`](docs/licencias-herramientas.md).

## Para trabajar en él

| Qué | Cómo |
|---|---|
| Compilar | `dotnet build card-lint.slnx` |
| Pruebas | `dotnet test card-lint.slnx` |
| Publicar para el equipo de destino | `.\publicar.ps1`, que deja todo en `publicacion\`, con el cuadro de mando como `cuadro-de-mando.html` |
| Pruebas del cuadro de mando | `npm test`, desde `panel\` |
| Compilar el cuadro de mando | `npm run build`, desde `panel\`: deja un solo HTML en `panel\dist\` |
| Rehacer el PDF del manual | `python docs\manual\_maqueta\build_pdf.py`, la maqueta de la casa (WeasyPrint con GTK y las fuentes Poppins y Lora) |
| Rehacer sus capturas | `python docs\manual\_maqueta\capturar.py docs\manual\informes\informe-ejemplo.html docs\manual\capturas` (Playwright) |
| Rehacer las del cuadro de mando | `python docs\manual\_maqueta\capturar_panel.py panel\dist\index.html panel\pruebas\historial 1.4 docs\manual\capturas`, con el panel ya compilado |

Necesita .NET 10. Las pruebas de integración usan Cppcheck y PMD de `herramientas\` y el repositorio card-code, que buscan al lado de este (o en `CARDLINT_HERRAMIENTAS` y `CARDLINT_CARDCODE`). Si no los encuentran, se saltan y dicen por qué.

El cuadro de mando (`panel\`) necesita Node.js para compilarse, no para usarse. Sus paquetes van a `panel\node_modules\` y la caché de npm a `.npm-cache\`, las dos en el repositorio y fuera de git.
