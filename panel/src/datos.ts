// La forma del JSON que escribe `card-lint analizar … --historial <carpeta>` (formato 1) y su lectura.
// Los nombres son los de InformeJson.cs, en camelCase.

export type Gravedad = 'Informacion' | 'Advertencia' | 'Fallo';

/** De menos a más: el gate compara con este orden. */
export const GRAVEDADES: readonly Gravedad[] = ['Informacion', 'Advertencia', 'Fallo'];

export const nivel = (g: Gravedad): number => GRAVEDADES.indexOf(g);

export interface Aviso {
  regla: string;
  gravedad: Gravedad;
  fichero: string;
  linea: number;
  mensaje: string;
  submodulo?: string;
  detalle?: string;
}

export interface Contramedida {
  fichero: string;
  lineaComentario: number;
  desde: number;
  hasta: number;
  motivo: string;
}

export interface Justificado {
  aviso: Aviso;
  contramedida: Contramedida;
}

export interface Umbrales {
  cognitiva: number;
  ciclomatica: number;
  lineasFuncion: number;
  parametros: number;
  funcionesModulo: number;
  lineasModulo: number;
}

/** La de card-lint.json. */
export interface Configuracion {
  gravedadQueFalla: Gravedad;
  minimoTokensDuplicado: number;
  umbrales: Umbrales;
}

export interface UmbralesJavaCard {
  cognitiva: number;
  ciclomatica: number;
  instrucciones: number;
}

/** Lo que una herramienta no mide en ese lenguaje no viene: en C no hay instrucciones; en Java Card, ni líneas ni parámetros. */
export interface MetricasFuncion {
  lenguaje: 'c' | 'javacard';
  fichero: string;
  linea: number;
  nombre: string;
  cognitiva?: number;
  ciclomatica?: number;
  lineas?: number;
  parametros?: number;
  instrucciones?: number;
}

export interface Analisis {
  formato: number;
  fecha: string;
  carpeta: string;
  gate: 'pasa' | 'falla';
  configuracion: Configuracion;
  umbralesJavaCard: UmbralesJavaCard;
  analizadores: string[];
  notas: string[];
  avisos: Aviso[];
  justificados: Justificado[];
  contramedidas: Contramedida[];
  funciones: MetricasFuncion[];
}

/** Los análisis de una misma carpeta, del más antiguo al más reciente. */
export interface Proyecto {
  carpeta: string;
  nombre: string;
  analisis: Analisis[];
}

export const FORMATO = 1;

export interface Fichero {
  nombre: string;
  texto: string;
}

/**
 * Agrupa por carpeta los JSON de --historial y ordena cada grupo por fecha. Lo que no es un análisis de
 * card-lint no para la lectura: va a `errores` con su motivo.
 */
export function leerHistorial(ficheros: Fichero[]): { proyectos: Proyecto[]; errores: string[] } {
  const errores: string[] = [];
  const porCarpeta = new Map<string, Analisis[]>();

  for (const { nombre, texto } of ficheros) {
    let datos: Partial<Analisis>;
    try {
      datos = JSON.parse(texto);
    } catch {
      errores.push(`${nombre}: no es un JSON válido`);
      continue;
    }

    if (datos.formato !== FORMATO || typeof datos.carpeta !== 'string' || !Array.isArray(datos.avisos)) {
      errores.push(`${nombre}: no es un análisis de card-lint en formato ${FORMATO}`);
      continue;
    }

    const analisis = datos as Analisis;
    const lista = porCarpeta.get(analisis.carpeta) ?? [];
    lista.push(analisis);
    porCarpeta.set(analisis.carpeta, lista);
  }

  const carpetas = [...porCarpeta.keys()].sort((a, b) => a.localeCompare(b, 'es'));
  const proyectos = carpetas.map((carpeta) => ({
    carpeta,
    nombre: nombreCorto(carpeta, carpetas),
    analisis: porCarpeta.get(carpeta)!.sort((a, b) => Date.parse(a.fecha) - Date.parse(b.fecha)),
  }));

  return { proyectos, errores };
}

const tramos = (ruta: string): string[] => ruta.split(/[\\/]/).filter(Boolean);

/** El último tramo de la carpeta; si otra carpeta acaba igual, los tramos que hagan falta para distinguirlas. */
function nombreCorto(carpeta: string, todas: string[]): string {
  const propios = tramos(carpeta);
  for (let n = 1; n < propios.length; n++) {
    const nombre = propios.slice(-n).join('/');
    if (todas.every((otra) => otra === carpeta || tramos(otra).slice(-n).join('/') !== nombre)) return nombre;
  }
  return propios.join('/');
}
