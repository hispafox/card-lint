import type { Configuracion, Gravedad, Umbrales } from '../datos';
import { cardLintJson } from '../recalcular';
import { NOMBRE_GRAVEDAD } from '../formato';

const DESLIZADORES: { clave: keyof Umbrales; nombre: string; min: number; max: number }[] = [
  { clave: 'cognitiva', nombre: 'Complejidad cognitiva', min: 1, max: 60 },
  { clave: 'ciclomatica', nombre: 'Complejidad ciclomática', min: 1, max: 50 },
  { clave: 'lineasFuncion', nombre: 'Líneas por función', min: 5, max: 200 },
  { clave: 'parametros', nombre: 'Parámetros', min: 0, max: 12 },
];

function descargar(texto: string, nombre: string) {
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(new Blob([texto], { type: 'application/json' }));
  enlace.download = nombre;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}

/**
 * Los umbrales de card-lint.json. Los cuatro de las funciones de C y la gravedad que falla se recalculan aquí
 * mismo; los demás solo se aplican cuando card-lint vuelve a analizar.
 */
export function ConfiguracionPanel({ configuracion, original, avisosAntes, avisosAhora, alCambiar }: {
  configuracion: Configuracion;
  original: Configuracion;
  avisosAntes: number;
  avisosAhora: number;
  alCambiar: (c: Configuracion) => void;
}) {
  const umbral = (clave: keyof Umbrales, valor: number) => alCambiar({ ...configuracion, umbrales: { ...configuracion.umbrales, [clave]: valor } });
  const json = cardLintJson(configuracion);
  const cambiada = json !== cardLintJson(original);
  const diferencia = avisosAhora - avisosAntes;

  return (
    <div className="tarjeta seccion">
      <h2>Configuración</h2>
      <p className="ayuda">
        Mueve los umbrales y mira cómo cambian los avisos y el gate. Cuando te convenza, descarga el card-lint.json y ponlo en la carpeta que
        analizas. Los umbrales de Java Card son los de PMD y no se cambian aquí.
      </p>
      <div className="config">
        <div>
          <div className="subtitulo">Se recalcula al momento</div>
          {DESLIZADORES.map((d) => (
            <label key={d.clave} className="deslizador">
              <span>{d.nombre}</span>
              <input type="range" min={d.min} max={d.max} value={configuracion.umbrales[d.clave]} onChange={(e) => umbral(d.clave, Number(e.target.value))} />
              <output className={configuracion.umbrales[d.clave] !== original.umbrales[d.clave] ? 'cambiado' : ''}>{configuracion.umbrales[d.clave]}</output>
            </label>
          ))}
          <label className="numero">
            <span>El gate falla desde</span>
            <select value={configuracion.gravedadQueFalla} onChange={(e) => alCambiar({ ...configuracion, gravedadQueFalla: e.target.value as Gravedad })}>
              {(['Fallo', 'Advertencia', 'Informacion'] as const).map((g) => (
                <option key={g} value={g}>{NOMBRE_GRAVEDAD[g]}</option>
              ))}
            </select>
          </label>
          <div className="subtitulo">Se aplica al volver a analizar</div>
          <label className="numero">
            <span>Tokens para un duplicado</span>
            <input type="number" min={10} value={configuracion.minimoTokensDuplicado} onChange={(e) => alCambiar({ ...configuracion, minimoTokensDuplicado: Number(e.target.value) })} />
          </label>
          <label className="numero">
            <span>Funciones por módulo</span>
            <input type="number" min={1} value={configuracion.umbrales.funcionesModulo} onChange={(e) => umbral('funcionesModulo', Number(e.target.value))} />
          </label>
          <label className="numero">
            <span>Líneas por módulo</span>
            <input type="number" min={1} value={configuracion.umbrales.lineasModulo} onChange={(e) => umbral('lineasModulo', Number(e.target.value))} />
          </label>
        </div>
        <div>
          <div className="subtitulo">card-lint.json</div>
          <pre>{json}</pre>
          <p className="ayuda">
            {!cambiada
              ? 'Es la configuración con la que se hizo este análisis.'
              : diferencia === 0
                ? 'Con esta configuración salen los mismos avisos que en el análisis.'
                : `Con esta configuración salen ${Math.abs(diferencia)} avisos ${diferencia > 0 ? 'más' : 'menos'} que en el análisis.`}
          </p>
          <div className="acciones">
            <button className="boton principal" onClick={() => descargar(json, 'card-lint.json')}>Descargar card-lint.json</button>
            <button className="boton" disabled={!cambiada} onClick={() => alCambiar(original)}>Volver a la del análisis</button>
          </div>
        </div>
      </div>
    </div>
  );
}
