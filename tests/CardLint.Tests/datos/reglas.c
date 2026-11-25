#include <stdlib.h>
#include <stdint.h>
#include "modulo.h"
#include "ciclo_a.h"

#define CUADRADO(x) ((x) * (x))
#define ELEMENTOS(a) (sizeof(a) / sizeof((a)[0]))

struct nodo { struct nodo *siguiente; int valor; };

static int veces;
static int solo_una;
extern int otro_global;

int reglas_cognitiva(int a, int b)
{
    if (a > 0 && b > 0 || a < 0) {
        return 1;
    } else if (a == 0) {
        return a ? 2 : 3;
    } else {
        for (int i = 0; i < b; i++) {
            if (i == 7) { /* MAGICO */
                goto fin;
            }
        }
    }
fin:
    return 0;
}

int reglas_usa_global(void)
{
    const int limite = 25; /* CONST */
    solo_una++;
    veces++;
    return modulo_estado + otro_global + limite;
}

void reglas_memoria(int n)
{
    uint8_t pila[n]; /* VLA */
    uint8_t tabla[4];
    uint8_t copia[ELEMENTOS(tabla)]; /* SIZEOF */
    int *p = malloc(sizeof(int)); /* MALLOC */
    pila[0] = tabla[0] = copia[0] = 0;
    free(p);
}

int reglas_cadena(struct nodo *n)
{
    return n->siguiente->siguiente->siguiente->valor; /* CADENA */
}

void reglas_llama(void)
{
    modulo_cuenta(1, 2); /* SIN COMPROBAR */
    (void)modulo_cuenta(3, 4);
    veces = 0;
}

int bandera(int con_algo, int x) /* BANDERA */
{
    if (con_algo) {
        return x;
    }
    return 0;
}

void reglas_sin_usar(int usado, int ignorado) /* SIN USAR */
{
    veces = usado;
}

int reglas_g1(const uint8_t *datos, uint8_t longitud, uint8_t desde) { return datos[desde] + longitud; }
int reglas_g2(const uint8_t *datos, uint8_t longitud, uint8_t desde) { return datos[longitud] + desde; }
int reglas_g3(const uint8_t *datos, uint8_t longitud, uint8_t desde) { return datos[0] + longitud + desde; }

int reglas_s1(int tipo) { switch (tipo) { case 1: return 0; default: return 1; } }
int reglas_s2(int tipo) { switch (tipo) { case 0: return 1; default: return 0; } }
