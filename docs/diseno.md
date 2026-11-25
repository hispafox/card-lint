# card-lint · Diseño

Propuesta del 25-11-2026, aprobada por Pedro ese día. La primera versión está construida: lo que cambió al hacerla está en «Lo que cambió al construirla», al final.

## Para qué sirve

card-lint analiza el código C y Java Card de un repositorio con la forma de card-code y deja un informe HTML con los olores, las métricas y un quality gate, como hace SonarQube. Es el regalo del curso CLNCOD: se entrega aparte y no se enseña en ninguna base, clase ni lab.

Detecta lo que enseña el curso, regla a regla, y cada aviso dice en qué submódulo se explica. No intenta copiar las reglas de SonarQube.

El catálogo de olores toma como referencia *Dive Into Refactoring (C#)*, de Alexander Shvets (refactoring.guru, 2019), que Pedro tiene convertido en el vault, en `Libros/dive-into-refactoring-c.md`. De ahí salen qué olores se añaden a los del curso, la familia de cada uno y sus excepciones. Los nombres de los olores son los de Fowler. Las descripciones y los mensajes de card-lint se escriben de nuevo: el libro es de uso personal y no se copia ningún texto suyo.

## Lo que ya está decidido

Lo decidió Pedro el 25-11-2026:

- Se escribe en .NET, sin Python.
- Llama a Cppcheck para el C y a PMD para el Java Card y la duplicación. La parte en .NET une los resultados, calcula lo que esas herramientas no dan y escribe el informe.
- Es una línea de comandos que deja un informe HTML. No hay servidor.
- Funciona sin conexión, porque el equipo del cliente trabaja en una red aislada.

## Cómo se usa

```powershell
card-lint analizar <carpeta> --salida informe.html
```

La carpeta tiene la forma de las versiones de card-code: `c/include`, `c/src` y, si hay applet, `javacard/src`. Las rutas y los umbrales se pueden cambiar en un `card-lint.json` dentro de la carpeta analizada.

Si el quality gate falla, el comando termina con un código distinto de cero. Así se puede lanzar en cada cambio, que es lo que M6.4 cuenta del análisis.

## Los proyectos

```text
card-lint/
  src/CardLint.Core/      el modelo (aviso, métrica, regla), la lectura de Cppcheck y PMD,
                          las reglas propias y el quality gate
  src/CardLint.Cli/       el comando y el informe HTML
  tests/CardLint.Tests/   pruebas con xUnit
  herramientas/           Cppcheck y PMD (pendiente: ver al final)
  docs/
```

Va en .NET 10, que es el que hay instalado (10.0.404), y se publica autocontenido para Windows de 64 bits: quien lo use no necesita instalar .NET.

## Qué pasa en un análisis

**El C pasa por Cppcheck**, que da dos cosas. Una es su lista de avisos en XML. La otra es el volcado (`--dump`): un XML por fichero, ya preprocesado, con los tokens, los ámbitos y las funciones. card-lint lee ese volcado para medir cada función y aplicar sus reglas de C, sin tener que escribir un analizador de C propio.

El volcado tiene un riesgo: es un formato interno de Cppcheck y puede cambiar de una versión a otra. Por eso la versión de Cppcheck queda fijada, y las pruebas avisan si al cambiarla se rompe la lectura.

**El Java Card pasa por PMD**, con un conjunto de reglas propio en XML. Lleva reglas de PMD que ya existen y reglas nuevas para Java Card, escritas en XPath sobre el árbol del código Java.

**La duplicación la busca CPD**, el detector de copias que viene con PMD, en C y en Java.

Después, todos los avisos pasan al mismo modelo: la regla, la gravedad, el fichero, la línea, el mensaje y el submódulo del curso donde se explica.

**El código redundante a propósito no se cuenta como fallo.** La 3.5 lo marca con un comentario que empieza por `CONTRAMEDIDA` y dice entre paréntesis contra qué protege. Si el comentario está justo antes de una función, la cubre entera. Si está dentro de una función, cubre la instrucción que viene detrás. Un aviso que cae en código cubierto sale como justificado: el informe lo enseña en su propia lista y el quality gate no lo cuenta.

El quality gate compara los avisos sin justificar y las métricas con los umbrales de `card-lint.json`. Al final queda un solo HTML con el CSS dentro, que se abre en el navegador sin servidor y sin conexión.

## Las reglas de la primera versión

Cada regla se contrasta con su base antes de escribirla.

| Regla | Lenguaje | Quién la detecta | Dónde lo enseña el curso |
|---|---|---|---|
| Complejidad cognitiva por función | C | card-lint, con el volcado | 1.2 |
| Complejidad ciclomática por función | C | card-lint, con el volcado | 1.2 |
| Función larga | C | card-lint | 1.2, 2.2, 5.1 |
| Demasiados parámetros | C | card-lint | 2.2 |
| Parámetro que actúa de bandera | C | card-lint | 5.1 |
| Función sin `static` que no está en ninguna cabecera | C | card-lint | 2.2, 2.3 |
| Función pública sin el prefijo de su módulo | C | card-lint | 2.1 |
| Macro con parámetros que podría ser `static inline` | C | card-lint | 2.4 |
| Asignación dinámica: `malloc`, `calloc`, `realloc`, `free` | C | card-lint | 3.2 |
| Valor de retorno que no se comprueba | C | card-lint | 3.1 |
| `switch` sobre la misma expresión en varias funciones | C | card-lint | 5.1 |
| Comportamiento indefinido, accesos fuera de límites y el resto de avisos de Cppcheck | C | Cppcheck | 3.2, 3.4 |
| Complejidad cognitiva (`CognitiveComplexity`) | Java Card | PMD | 1.2 |
| Complejidad ciclomática, método largo y demasiados parámetros | Java Card | PMD | 1.2, 2.2 |
| `String`, `int`, `long`, `float`, `double` y `java.util` | Java Card | PMD, regla propia | 4.1 |
| `new` fuera del constructor y de `install` | Java Card | PMD, regla propia | 4.1, 4.2 |
| Código de estado o de instrucción escrito como número | Java Card | PMD, regla propia | 4.4 |
| Código duplicado | C y Java Card | CPD | 1.2, 5.1 |

La regla del prefijo tiene una duda que resolver con la 2.1 antes de escribirla: en `seguridad.c`, en la versión `despues` de la rama de la 3.5, hay una función `tarjeta_bloquear`. Si el prefijo se saca del nombre del fichero, esa función daría aviso.

## Los olores que se añaden desde el libro

El libro trae 23 olores en cinco familias: los que hinchan el código, los que abusan de la orientación a objetos, los que impiden cambiarlo, los que sobran y los que acoplan, más uno suelto. Cuatro ya están en la tabla de arriba: la función larga, la lista larga de parámetros, el `switch` repetido y el código duplicado. Estos se añaden:

| Olor | Familia | En C | En Java Card | Dónde lo enseña el curso |
|---|---|---|---|---|
| Código muerto y generalidad especulativa | Los que sobran | Funciones, variables y parámetros sin usar: `unusedFunction` y los avisos de variables de Cppcheck, más los parámetros, que mira card-lint | `UnusedPrivateMethod`, `UnusedPrivateField`, `UnusedLocalVariable` y `UnusedFormalParameter` de PMD | No está en el curso |
| Clase o módulo grande | Los que hinchan | Un `.c` con demasiadas funciones o líneas, con el volcado | `TooManyMethods` y `TooManyFields` de PMD | 4.2 |
| Obsesión por los primitivos | Los que hinchan | Números escritos a pelo en el código, fuera de un `#define`, un `enum` o un `static const` | Ya está arriba, con los códigos de estado | 2.1, 4.4 |
| Grupos de datos | Los que hinchan | Los mismos tres o más parámetros, con el mismo nombre y tipo, en varias funciones | Se deja para más adelante | No está en el curso |
| Campo temporal | Los que abusan de la orientación a objetos | Una variable `static` del fichero que solo usa una función | Un campo que solo usa un método | 4.3, en Java Card |
| Intimidad inapropiada | Los que acoplan | Ciclos de `#include` entre módulos, y variables globales de otro módulo usadas con `extern` | — | 2.3 |
| Cadenas de mensajes | Los que acoplan | `p->a->b->c` | `a.b().c().d()`, con una regla propia | No está en el curso |

Del libro salen también cuatro decisiones de diseño:

- **El código muerto y la generalidad especulativa van juntos**, porque el libro les da el mismo síntoma: algo que nadie usa. Y antes de dar algo por muerto, card-lint mira también en `pruebas/`. Si solo lo usan las pruebas, el aviso lo dice en vez de pedir que se borre, que es la excepción que pone el libro.
- **El campo temporal en Java Card apunta a la 4.3.** Un campo que solo usa un método suele ser un dato de trabajo, y la 4.3 enseña a ponerlo en un array transitorio.
- **Las cadenas de mensajes salen como información, no como fallo.** El propio libro avisa de que esconderlas todas llena el código de intermediarios.
- **El intermediario no entra.** El libro dice que no se quita uno puesto a propósito para cortar una dependencia, y es justo lo que hace la interfaz en C que aísla el ensamblador en la 2.4.

Los olores que vienen solo del libro no tienen rama en card-code. Sus pruebas son unitarias, sobre ficheros pequeños hechos para cada regla.

## Lo que se queda fuera de la primera versión

- **El cambio divergente y la cirugía de escopeta.** Solo se ven en el historial de git, mirando qué ficheros cambian juntos. Se pueden sacar de `git log` en una versión posterior.
- **Los olores del libro que piden criterio o hablan de herencia:** herencia rechazada, clases alternativas con interfaces distintas, jerarquías paralelas, clase perezosa, clase de datos, envidia de funcionalidad, intermediario y clase de biblioteca incompleta.

- **MISRA C.** El módulo MISRA de Cppcheck está escrito en Python.
- **Las conversiones implícitas de la 3.3**, porque ya las avisa el compilador con `-Wconversion`, que está activado en `main` de card-code.
- **Los comentarios que explican lo que el código debería decir (5.1) y los datos de trabajo en arrays transitorios (4.3).** Para detectarlos hay que saber qué quería hacer quien escribió el código. El libro admite el comentario que explica por qué algo está hecho así, que es lo que hace `CONTRAMEDIDA` en la 3.5.
- **El historial entre análisis y la idea de «código nuevo»** que tiene SonarQube.
- **Groovy y el ensamblador.**

## Las pruebas

Las unitarias, con xUnit, trabajan sobre ficheros pequeños guardados en `tests/datos/`: volcados de Cppcheck y salidas de PMD ya generados. Así no necesitan las herramientas instaladas y dan siempre el mismo resultado.

Las de integración usan card-code como banco de pruebas. Para cada regla se toma la rama del submódulo donde se enseña: en la función que la base pone de ejemplo, la versión `antes` tiene que dar el aviso y la versión `despues`, no. En la rama de la 3.5, la comprobación duplicada tiene que salir como justificada. Cada prueba saca su rama con `git archive` a un directorio temporal y lo borra al terminar, de modo que card-code solo se lee. Estas pruebas necesitan Cppcheck y PMD: si no los encuentran, se saltan y dicen por qué.

## La entrega sin conexión

`dotnet publish` deja una carpeta con `card-lint.exe`, Cppcheck, PMD y una carpeta `licencias/`, como la de `lib/` en card-code. PMD necesita Java, y el equipo ya lo tiene por el kit de Java Card (card-code pide un JDK 11 o posterior). Cppcheck tiene licencia GPL, así que se reparte con su código fuente.

## Lo que falta decidir

1. ~~**Instalar Cppcheck y PMD en el equipo de Pedro.**~~ Hecho el 25-11-2026, con su permiso: Cppcheck 2.22.0 y PMD 7.29.0, descargados de sus páginas oficiales en GitHub a `herramientas/`, en F:. Cppcheck va extraído, sin instalar.
2. **Dónde se guardan sus binarios:** dentro del repositorio, como `lib/` en card-code, o solo en el paquete de entrega.
3. **Los umbrales del quality gate.** Hasta que Pedro los decida, el gate falla con un aviso de gravedad «fallo», y las reglas de C usan unos provisionales: cognitiva 15 y ciclomática 10, los mismos que PMD en Java, 40 líneas de código por función, 5 parámetros, 20 funciones y 400 líneas por módulo. Todos se cambian en `card-lint.json`.
4. **Si basta con Windows** o hace falta también una versión para Linux.
5. **El primer commit y si el repositorio se sube a GitHub**, como card-code.

## Lo que cambió al construirla

La primera versión quedó hecha el 25-11-2026, con 70 pruebas en verde. Estas son las diferencias con lo de arriba, y por qué.

- **Las líneas de una función son líneas de código**, sin las que están en blanco. Así las cuenta la 1.2: 23 en el antes de `registro_escribir`, que tiene 24 contando la línea vacía. Con ese criterio card-lint da las cifras de la base en el antes y en el después: cognitiva 18, ciclomática 6 y 23 líneas, y luego 3 y 3, 4 y 3, 13 y 9.
- **La regla de «sin static» la da Cppcheck**, que desde la 2.22 avisa de las funciones que podrían ser `static`. card-lint descarta el aviso si la función está en una cabecera o la llaman las pruebas, porque entonces sí sale del fichero.
- **El prefijo del módulo solo avisa de los nombres sin prefijo**, como `leer` o `inicializar`. La 2.1 deja que cada módulo elija su prefijo, así que card-lint no lo compara con el nombre del fichero, y la duda de `tarjeta_bloquear` en `seguridad.c` desaparece.
- **Para el ciclo de inclusión solo cuentan las cabeceras.** Que dos `.c` usen cada uno la cabecera del otro no forma un ciclo, y en el después de la 2.3 ese caso sigue existiendo a propósito.
- **El `switch` repetido cuenta también dentro de una misma función**, que es como aparece en la 5.1.
- **Una regla nueva para la 3.2: el array local cuyo tamaño sale de una variable.** Es lo que tiene el antes de esa rama, no un `malloc`. Las expresiones con `sizeof` o con constantes no avisan.
- **Las macros con `sizeof` no avisan**, porque con un array solo funcionan como macro: es la excepción de la macro genérica de la 2.4.
- **Cppcheck se ejecuta sobre una copia**, en una carpeta de trabajo al lado del informe que se borra al terminar, porque deja su volcado junto a cada fuente.
- **Las reglas propias de Java Card van en XPath** dentro de `Reglas/pmd-javacard.xml`. La lista larga de llamadas encadenadas tiene la suya, y el campo que solo usa un método lo da `SingularField`, de PMD.
- **El informe lleva un resumen por regla** antes de la lista de avisos, y debajo de cada mensaje, en pequeño, el original de la herramienta.
- **La publicación la hace `publicar.ps1`.** Copia de Cppcheck solo la línea de comandos, sin la interfaz gráfica.
