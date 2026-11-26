import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Analisis, Fichero } from '../src/datos';

export const ficherosDe = (carpeta: string): Fichero[] =>
  readdirSync(carpeta, { recursive: true, encoding: 'utf8' })
    .filter((f) => f.endsWith('.json'))
    .map((f) => ({ nombre: f, texto: readFileSync(join(carpeta, f), 'utf8') }));

/** Cinco análisis reales de card-code, con la carpeta cambiada; los dos de la 1.4 comparten carpeta. */
export const ejemplos = ficherosDe(join(__dirname, 'historial'));

export const ejemplo = (nombre: string): Analisis => JSON.parse(readFileSync(join(__dirname, 'historial', `${nombre}.json`), 'utf8'));
