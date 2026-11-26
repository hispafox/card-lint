import type { Gravedad } from './datos';

export const NOMBRE_GRAVEDAD: Record<Gravedad, string> = {
  Fallo: 'fallo',
  Advertencia: 'advertencia',
  Informacion: 'información',
};

export const PLURAL_GRAVEDAD: Record<Gravedad, string> = {
  Fallo: 'Fallos',
  Advertencia: 'Advertencias',
  Informacion: 'Información',
};

/** Los de estado: siempre acompañados de su nombre, nunca solos. */
export const COLOR_GRAVEDAD: Record<Gravedad, string> = {
  Fallo: 'var(--fallo)',
  Advertencia: 'var(--advertencia)',
  Informacion: 'var(--informacion)',
};

/** De más grave a menos, que es como se leen la leyenda y las columnas. */
export const GRAVEDADES_DE_MAS_A_MENOS: readonly Gravedad[] = ['Fallo', 'Advertencia', 'Informacion'];

const corta = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
const media = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium', timeStyle: 'short' });
const larga = new Intl.DateTimeFormat('es-ES', { dateStyle: 'long', timeStyle: 'short' });

export const fechaCorta = (iso: string): string => corta.format(new Date(iso));
export const fechaMedia = (iso: string): string => media.format(new Date(iso));
export const fechaLarga = (iso: string): string => larga.format(new Date(iso));

export const nombreFichero = (ruta: string): string => ruta.split('/').pop() ?? ruta;

export const plural = (n: number, uno: string, varios: string): string => `${n} ${n === 1 ? uno : varios}`;
