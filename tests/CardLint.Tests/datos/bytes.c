#include <stdint.h>

uint16_t bytes_leer(const uint8_t *p)
{
    return (uint16_t)(((uint16_t)p[0] << 8) | p[1]); /* JUNTAR */
}

void bytes_escribir(uint8_t *p, uint16_t valor)
{
    p[0] = (uint8_t)(valor >> 8);    /* PARTIR ALTO */
    p[1] = (uint8_t)(valor & 0xFFu); /* PARTIR BAJO */
}

uint32_t bytes_juntar_32(uint16_t alto, uint16_t bajo)
{
    return ((uint32_t)alto << 16) | bajo; /* JUNTAR 32 */
}

uint8_t bytes_mas_alto(uint32_t valor)
{
    return (uint8_t)(valor >> 24); /* MAS ALTO */
}

uint16_t bytes_mitad_baja(uint32_t valor)
{
    return (uint16_t)(0xFFFFu & valor); /* MASCARA DELANTE */
}

uint8_t bytes_compuesto(uint16_t valor)
{
    valor >>= 8;    /* COMPUESTO */
    valor &= 0xFFu; /* COMPUESTO Y */
    return (uint8_t)valor;
}

uint8_t bytes_otros(uint8_t valor)
{
    return (uint8_t)((valor << 3) & 0x0Fu); /* OTROS */
}

uint16_t bytes_direccion(uint16_t valor)
{
    return (uint16_t)((valor >> 8) + 0x0040u); /* DIRECCION */
}

uint16_t bytes_ocho_suelto(uint16_t valor)
{
    return (uint16_t)(valor + 8u); /* OCHO SUELTO */
}
