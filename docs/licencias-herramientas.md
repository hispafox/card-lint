# Licencias de las herramientas que van con card-lint

card-lint llama a dos herramientas externas, que viajan con él en la carpeta `herramientas/`.

| Herramienta | Versión | Licencia | De dónde sale |
|---|---|---|---|
| Cppcheck | 2.22.0 | GPL versión 3 (`cppcheck-GPLv3.txt`) | <https://github.com/cppcheck-opensource/cppcheck/releases/tag/2.22.0> |
| PMD | 7.29.0 | BSD (`pmd-bin-7.29.0-LICENSE.txt`) | <https://github.com/pmd/pmd/releases/tag/pmd_releases%2F7.29.0> |

De Cppcheck va solo la línea de comandos, sin la interfaz gráfica, tal como la publica su proyecto. Su código fuente, el de esta misma versión, está en la página de la versión de arriba. card-lint no lo modifica: lo lanza como un programa aparte.

PMD necesita Java 8 o posterior en el equipo.
