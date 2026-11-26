import { describe, expect, it } from 'vitest';
import type { Analisis, Aviso, Configuracion } from '../src/datos';
import { leerHistorial } from '../src/datos';
import { avisosDeMetricasC, cardLintJson, recalcular } from '../src/recalcular';
import { contar, dondeSeExplica, evolucion, medidas, porDonde, porFichero, porRegla } from '../src/resumen';
import { ejemplo, ejemplos, ficherosDe } from './apoyo';

// CARDLINT_HISTORIAL apunta a otra carpeta de JSON para pasar la comprobación de equivalencia sobre un
// historial entero.
const ficheros = process.env.CARDLINT_HISTORIAL ? ficherosDe(process.env.CARDLINT_HISTORIAL) : ejemplos;
const todos: Analisis[] = ficheros.map((f) => JSON.parse(f.texto));

const clave = (a: Aviso) => `${a.regla}|${a.fichero}|${a.linea}|${a.mensaje}|${a.gravedad}|${a.submodulo ?? ''}`;
const claves = (avisos: Aviso[]) => avisos.map(clave).sort();

describe('leerHistorial', () => {
  it('agrupa por carpeta y ordena cada proyecto por fecha', () => {
    const despues = ejemplos.find((f) => f.nombre.includes('1.4-despues'))!;
    const antes = ejemplos.find((f) => f.nombre.includes('1.4-antes'))!;

    const { proyectos, errores } = leerHistorial([despues, antes]);

    expect(errores).toEqual([]);
    expect(proyectos).toHaveLength(1);
    expect(proyectos[0].nombre).toBe('1.4');
    expect(proyectos[0].analisis.map((a) => a.avisos.length)).toEqual([83, 10]);
  });

  it('si dos carpetas acaban igual, el nombre lleva los tramos que las distinguen', () => {
    const analisis = (carpeta: string) => ({ nombre: carpeta, texto: JSON.stringify({ ...ejemplo('2.3-despues'), carpeta }) });

    const { proyectos } = leerHistorial([analisis('F:/ramas/1.4/antes'), analisis('F:/ramas/2.1/antes'), analisis('F:/otro/monedero')]);

    expect(proyectos.map((p) => p.nombre)).toEqual(['monedero', '1.4/antes', '2.1/antes']);
  });

  it('aparta lo que no es un análisis de card-lint y dice por qué', () => {
    const { proyectos, errores } = leerHistorial([
      { nombre: 'roto.json', texto: '{ no' },
      { nombre: 'otro.json', texto: '{"formato": 2, "carpeta": "x", "avisos": []}' },
      { nombre: 'card-lint.json', texto: '{"gravedadQueFalla": "Fallo"}' },
    ]);

    expect(proyectos).toEqual([]);
    expect(errores).toEqual([
      'roto.json: no es un JSON válido',
      'otro.json: no es un análisis de card-lint en formato 1',
      'card-lint.json: no es un análisis de card-lint en formato 1',
    ]);
  });
});

describe('recalcular', () => {
  it.each(ficheros.map((f, i) => [f.nombre, todos[i]] as const))(
    'con la configuración del análisis da lo mismo que card-lint: %s',
    (_, a) => {
      const r = recalcular(a, a.configuracion);

      expect(claves(r.avisos)).toEqual(claves(a.avisos));
      expect(claves(r.justificados.map((j) => j.aviso))).toEqual(claves(a.justificados.map((j) => j.aviso)));
      expect(r.gatePasa).toBe(a.gate === 'pasa');
    },
  );

  it('al bajar un umbral salen avisos nuevos, y al subirlo se van', () => {
    const a = ejemplo('1.4-despues');
    const mas = recalcular(a, { ...a.configuracion, umbrales: { ...a.configuracion.umbrales, cognitiva: 2 } });
    const menos = recalcular(ejemplo('1.4-antes'), { ...a.configuracion, umbrales: { ...a.configuracion.umbrales, cognitiva: 45 } });

    expect(contar(a.avisos).Advertencia).toBeLessThan(contar(mas.avisos).Advertencia);
    expect(mas.avisos.filter((x) => x.regla === 'c:complejidad-cognitiva').every((x) => x.mensaje.endsWith('(umbral 2)'))).toBe(true);
    expect(menos.avisos.some((x) => x.regla === 'c:complejidad-cognitiva' && x.mensaje.includes('proc'))).toBe(false);
  });

  it('un aviso nuevo que cae en una contramedida sale justificado', () => {
    const a = ejemplo('3.5-despues');
    const funcion = a.funciones.find((f) => f.lenguaje === 'c')!;
    const conContramedida: Analisis = {
      ...a,
      contramedidas: [...a.contramedidas, { fichero: funcion.fichero.toUpperCase(), lineaComentario: funcion.linea - 1, desde: funcion.linea, hasta: funcion.linea, motivo: 'prueba' }],
    };

    const r = recalcular(conContramedida, { ...a.configuracion, umbrales: { ...a.configuracion.umbrales, parametros: 0 } });

    expect(r.justificados.some((j) => j.aviso.regla === 'c:demasiados-parametros' && j.aviso.linea === funcion.linea)).toBe(true);
    expect(r.avisos.some((x) => x.regla === 'c:demasiados-parametros' && x.linea === funcion.linea && x.fichero === funcion.fichero)).toBe(false);
  });

  it('el gate falla desde la gravedad que se elige', () => {
    const a = ejemplo('1.4-antes');

    expect(recalcular(a, a.configuracion).gatePasa).toBe(true);
    expect(recalcular(a, { ...a.configuracion, gravedadQueFalla: 'Advertencia' }).gatePasa).toBe(false);
  });

  it('solo rehace las métricas de C: los avisos de Java Card no cambian', () => {
    const a = ejemplo('4.1-antes');
    const r = recalcular(a, { ...a.configuracion, umbrales: { ...a.configuracion.umbrales, cognitiva: 1, ciclomatica: 1 } });

    expect(claves(r.avisos.filter((x) => x.regla.startsWith('pmd:')))).toEqual(claves(a.avisos.filter((x) => x.regla.startsWith('pmd:'))));
  });

  it('los mensajes son los de card-lint', () => {
    const avisos = avisosDeMetricasC(
      [{ lenguaje: 'c', fichero: 'c/src/a.c', linea: 3, nombre: 'a_leer', cognitiva: 16, ciclomatica: 11, lineas: 41, parametros: 6 }],
      { cognitiva: 15, ciclomatica: 10, lineasFuncion: 40, parametros: 5, funcionesModulo: 20, lineasModulo: 400 },
    );

    expect(avisos.map((a) => a.mensaje)).toEqual([
      'Complejidad cognitiva de a_leer: 16 (umbral 15)',
      'Complejidad ciclomática de a_leer: 11 (umbral 10)',
      'a_leer tiene 41 líneas de código (umbral 40)',
      'a_leer tiene 6 parámetros (umbral 5)',
    ]);
  });
});

describe('cardLintJson', () => {
  it('escribe la configuración con los mismos nombres que la del análisis', () => {
    const c: Configuracion = ejemplo('2.3-despues').configuracion;

    expect(JSON.parse(cardLintJson(c))).toEqual(c);
  });
});

describe('resumen', () => {
  it('cuenta por regla con la más grave primero, por dónde se explica y por fichero', () => {
    const a = ejemplo('4.1-antes');

    const reglas = porRegla(a.avisos);
    expect(reglas[0]).toEqual({ regla: 'pmd:JavaCardObjetoFueraDeInstalar', n: 21, gravedad: 'Fallo' });
    expect(porDonde(a.avisos).reduce((s, d) => s + d.n, 0)).toBe(a.avisos.length);
    expect(porFichero(a.avisos, 3)).toHaveLength(3);
    expect(dondeSeExplica('olor: código muerto')).toBe('Olor: código muerto');
    expect(dondeSeExplica('1.2, 2.2, 5.1')).toBe('Submódulo 1.2');
    expect(dondeSeExplica(undefined)).toBe('Sin asignar');
  });

  it('mide en Java Card desde el umbral y en C cuando se pasa', () => {
    const a = ejemplo('4.1-antes');
    const lista = medidas(
      [
        { lenguaje: 'javacard', fichero: 'A.java', linea: 1, nombre: 'm()', cognitiva: 15 },
        { lenguaje: 'c', fichero: 'a.c', linea: 1, nombre: 'f', cognitiva: 15 },
      ],
      a.configuracion,
      a.umbralesJavaCard,
    );

    // Las dos están justo en el umbral, así que van por nombre.
    expect(lista.map((m) => [m.funcion.nombre, m.supera])).toEqual([['f', false], ['m()', true]]);
    expect(medidas(a.funciones, a.configuracion, a.umbralesJavaCard)[0].funcion.nombre).toBe('process(APDU)');
  });

  it('la evolución recalcula cada análisis con la configuración que se prueba', () => {
    const { proyectos } = leerHistorial(ejemplos.filter((f) => f.nombre.includes('1.4')));
    const c = proyectos[0].analisis[0].configuracion;

    const serie = evolucion(proyectos[0].analisis, { ...c, gravedadQueFalla: 'Advertencia' });

    expect(serie.map((p) => p.gatePasa)).toEqual([false, false]);
    expect(serie[0].cuenta.Advertencia).toBeGreaterThan(serie[1].cuenta.Advertencia);
  });
});
