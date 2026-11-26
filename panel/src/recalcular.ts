// Los avisos de un análisis con otra configuración, sin volver a lanzar card-lint. Solo cambia lo que se
// puede calcular con lo que trae el JSON: los cuatro avisos de métricas de C, que salen de las funciones,
// y el gate. Lo demás (CPD, el tamaño de los módulos, Java Card) se queda como se analizó.

import type { Analisis, Aviso, Configuracion, Contramedida, Justificado, MetricasFuncion, Umbrales } from './datos';
import { nivel } from './datos';

/** Las reglas de ReglasC.Metricas, que el panel rehace con los umbrales de la configuración. */
export const REGLAS_RECALCULABLES = [
  'c:complejidad-cognitiva',
  'c:complejidad-ciclomatica',
  'c:funcion-larga',
  'c:demasiados-parametros',
] as const;

const esRecalculable = (a: Aviso): boolean => (REGLAS_RECALCULABLES as readonly string[]).includes(a.regla);

/** Los avisos de métricas de las funciones de C, con los mismos mensajes que card-lint. En C se avisa cuando se pasa del umbral. */
export function avisosDeMetricasC(funciones: MetricasFuncion[], u: Umbrales): Aviso[] {
  const avisos: Aviso[] = [];
  for (const f of funciones.filter((x) => x.lenguaje === 'c')) {
    const aviso = (regla: string, mensaje: string, submodulo: string): Aviso => ({
      regla,
      gravedad: 'Advertencia',
      fichero: f.fichero,
      linea: f.linea,
      mensaje,
      submodulo,
    });

    if ((f.cognitiva ?? 0) > u.cognitiva) {
      avisos.push(aviso('c:complejidad-cognitiva', `Complejidad cognitiva de ${f.nombre}: ${f.cognitiva} (umbral ${u.cognitiva})`, '1.2'));
    }
    if ((f.ciclomatica ?? 0) > u.ciclomatica) {
      avisos.push(aviso('c:complejidad-ciclomatica', `Complejidad ciclomática de ${f.nombre}: ${f.ciclomatica} (umbral ${u.ciclomatica})`, '1.2'));
    }
    if ((f.lineas ?? 0) > u.lineasFuncion) {
      avisos.push(aviso('c:funcion-larga', `${f.nombre} tiene ${f.lineas} líneas de código (umbral ${u.lineasFuncion})`, '1.2, 2.2, 5.1'));
    }
    if ((f.parametros ?? 0) > u.parametros) {
      avisos.push(aviso('c:demasiados-parametros', `${f.nombre} tiene ${f.parametros} parámetros (umbral ${u.parametros})`, '2.2'));
    }
  }
  return avisos;
}

const normalizar = (ruta: string): string => ruta.replaceAll('\\', '/').toLowerCase();

/** Como Contramedida.Cubre: el mismo fichero, sin distinguir mayúsculas, y la línea dentro de lo que cubre. */
export const cubre = (c: Contramedida, fichero: string, linea: number): boolean =>
  normalizar(c.fichero) === normalizar(fichero) && linea >= c.desde && linea <= c.hasta;

export interface Resultado {
  avisos: Aviso[];
  justificados: Justificado[];
  gatePasa: boolean;
}

export function recalcular(analisis: Analisis, configuracion: Configuracion): Resultado {
  const avisos = analisis.avisos.filter((a) => !esRecalculable(a));
  const justificados = analisis.justificados.filter((j) => !esRecalculable(j.aviso));

  for (const aviso of avisosDeMetricasC(analisis.funciones, configuracion.umbrales)) {
    const contramedida = analisis.contramedidas.find((c) => cubre(c, aviso.fichero, aviso.linea));
    if (contramedida) {
      justificados.push({ aviso, contramedida });
    } else {
      avisos.push(aviso);
    }
  }

  const umbral = nivel(configuracion.gravedadQueFalla);
  return { avisos, justificados, gatePasa: !avisos.some((a) => nivel(a.gravedad) >= umbral) };
}

/** El card-lint.json de una configuración, con los nombres que lee card-lint. */
export const cardLintJson = (c: Configuracion): string =>
  JSON.stringify(
    { gravedadQueFalla: c.gravedadQueFalla, minimoTokensDuplicado: c.minimoTokensDuplicado, umbrales: { ...c.umbrales } },
    null,
    2,
  ) + '\n';
