// Lo que pintan los gráficos, sacado de los avisos y las funciones de un análisis ya recalculado.

import type { Analisis, Aviso, Configuracion, Gravedad, MetricasFuncion, UmbralesJavaCard } from './datos';
import { GRAVEDADES, nivel } from './datos';
import { recalcular } from './recalcular';

export type Cuenta = Record<Gravedad, number>;

export function contar(avisos: Aviso[]): Cuenta {
  const cuenta: Cuenta = { Informacion: 0, Advertencia: 0, Fallo: 0 };
  for (const a of avisos) cuenta[a.gravedad]++;
  return cuenta;
}

export const total = (c: Cuenta): number => GRAVEDADES.reduce((s, g) => s + c[g], 0);

/** Cuántos avisos da cada regla, con la gravedad más alta que tenga: primero las más graves, luego las que más avisan. */
export function porRegla(avisos: Aviso[]): { regla: string; n: number; gravedad: Gravedad }[] {
  const reglas = new Map<string, { regla: string; n: number; gravedad: Gravedad }>();
  for (const a of avisos) {
    const r = reglas.get(a.regla) ?? { regla: a.regla, n: 0, gravedad: a.gravedad };
    r.n++;
    if (nivel(a.gravedad) > nivel(r.gravedad)) r.gravedad = a.gravedad;
    reglas.set(a.regla, r);
  }
  return [...reglas.values()].sort((a, b) => nivel(b.gravedad) - nivel(a.gravedad) || b.n - a.n || a.regla.localeCompare(b.regla));
}

/** Dónde se explica cada aviso: el primer submódulo que nombra, el olor del libro o «Sin asignar». */
export function dondeSeExplica(submodulo?: string): string {
  if (!submodulo) return 'Sin asignar';
  if (submodulo.startsWith('olor: ')) return 'Olor: ' + submodulo.slice(6);
  return 'Submódulo ' + submodulo.split(',')[0].trim();
}

export function porDonde(avisos: Aviso[]): { donde: string; n: number }[] {
  const cuenta = new Map<string, number>();
  for (const a of avisos) {
    const d = dondeSeExplica(a.submodulo);
    cuenta.set(d, (cuenta.get(d) ?? 0) + 1);
  }
  return [...cuenta.entries()].map(([donde, n]) => ({ donde, n })).sort((a, b) => b.n - a.n || a.donde.localeCompare(b.donde, 'es'));
}

/** Los ficheros con más avisos, con el reparto por gravedad. */
export function porFichero(avisos: Aviso[], maximo: number): { fichero: string; cuenta: Cuenta }[] {
  const ficheros = new Map<string, Aviso[]>();
  for (const a of avisos) ficheros.set(a.fichero, [...(ficheros.get(a.fichero) ?? []), a]);
  return [...ficheros.entries()]
    .map(([fichero, lista]) => ({ fichero, cuenta: contar(lista) }))
    .sort((a, b) => total(b.cuenta) - total(a.cuenta) || a.fichero.localeCompare(b.fichero))
    .slice(0, maximo);
}

export type Metrica = 'cognitiva' | 'ciclomatica' | 'lineas' | 'parametros' | 'instrucciones';

export const NOMBRE_METRICA: Record<Metrica, string> = {
  cognitiva: 'cognitiva',
  ciclomatica: 'ciclomática',
  lineas: 'líneas',
  parametros: 'parámetros',
  instrucciones: 'instrucciones (NCSS)',
};

export interface Medida {
  funcion: MetricasFuncion;
  metrica: Metrica;
  valor: number;
  umbral: number;
  supera: boolean;
}

/**
 * Cada métrica de cada función frente a su umbral, de la que más se pasa a la que menos. En C avisa lo que
 * pasa del umbral; en Java Card, como en PMD, ya avisa el propio umbral.
 */
export function medidas(funciones: MetricasFuncion[], c: Configuracion, javacard: UmbralesJavaCard): Medida[] {
  const lista: Medida[] = [];
  for (const f of funciones) {
    const umbrales: Partial<Record<Metrica, number>> =
      f.lenguaje === 'c'
        ? { cognitiva: c.umbrales.cognitiva, ciclomatica: c.umbrales.ciclomatica, lineas: c.umbrales.lineasFuncion, parametros: c.umbrales.parametros }
        : { cognitiva: javacard.cognitiva, ciclomatica: javacard.ciclomatica, instrucciones: javacard.instrucciones };

    for (const [metrica, umbral] of Object.entries(umbrales) as [Metrica, number][]) {
      const valor = f[metrica];
      if (valor === undefined) continue;
      lista.push({ funcion: f, metrica, valor, umbral, supera: f.lenguaje === 'c' ? valor > umbral : valor >= umbral });
    }
  }
  return lista.sort((a, b) => b.valor / b.umbral - a.valor / a.umbral || a.funcion.nombre.localeCompare(b.funcion.nombre));
}

/** Un punto por análisis, recalculado con la configuración que se está probando. */
export function evolucion(analisis: Analisis[], c: Configuracion): { fecha: string; cuenta: Cuenta; gatePasa: boolean }[] {
  return analisis.map((a) => {
    const r = recalcular(a, c);
    return { fecha: a.fecha, cuenta: contar(r.avisos), gatePasa: r.gatePasa };
  });
}
