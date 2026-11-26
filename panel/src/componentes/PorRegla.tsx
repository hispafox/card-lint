import { useState } from 'react';
import type { Aviso } from '../datos';
import { porDonde, porRegla } from '../resumen';
import { COLOR_GRAVEDAD, GRAVEDADES_DE_MAS_A_MENOS, NOMBRE_GRAVEDAD } from '../formato';
import type { Filtro } from './TablaAvisos';

const FILAS = 12;

/** Cuántos avisos da cada regla, o dónde se explican en el curso. Al pulsar una fila, la tabla se queda con esos. */
export function PorRegla({ avisos, filtro, alFiltrar }: { avisos: Aviso[]; filtro: Filtro; alFiltrar: (f: Filtro) => void }) {
  const [vista, setVista] = useState<'regla' | 'donde'>('regla');
  const filas =
    vista === 'regla'
      ? porRegla(avisos).map((r) => ({ clave: r.regla, n: r.n, color: COLOR_GRAVEDAD[r.gravedad], titulo: `${r.regla} · ${NOMBRE_GRAVEDAD[r.gravedad]}` }))
      : porDonde(avisos).map((d) => ({ clave: d.donde, n: d.n, color: 'var(--azul)', titulo: d.donde }));
  const maximo = Math.max(1, ...filas.map((f) => f.n));
  const elegida = vista === 'regla' ? filtro.regla : filtro.donde;

  return (
    <div className="tarjeta">
      <div className="titular">
        <h2>{vista === 'regla' ? 'Avisos por regla' : 'Dónde se explican'}</h2>
        <span className="relleno" />
        <div className="conmutador">
          <button className={vista === 'regla' ? 'activo' : ''} onClick={() => setVista('regla')}>Por regla</button>
          <button className={vista === 'donde' ? 'activo' : ''} onClick={() => setVista('donde')}>Por submódulo</button>
        </div>
      </div>
      <p className="ayuda">
        {vista === 'regla'
          ? 'El color es la gravedad más alta de la regla. Pulsa una fila para ver sus avisos abajo.'
          : 'El submódulo del curso que explica cada aviso, o el olor del libro. Pulsa una fila para verlos.'}
      </p>
      {vista === 'regla' && (
        <div className="leyenda">
          {GRAVEDADES_DE_MAS_A_MENOS.map((g) => (
            <span key={g}><i className="punto" style={{ background: COLOR_GRAVEDAD[g] }} />{NOMBRE_GRAVEDAD[g]}</span>
          ))}
        </div>
      )}
      {filas.length === 0 ? (
        <p className="vacio">No queda ningún aviso sin justificar.</p>
      ) : (
        <div className="barras">
          {filas.slice(0, FILAS).map((f) => (
            <div
              key={f.clave}
              className={'fila' + (elegida === f.clave ? ' elegida' : '')}
              title={f.titulo}
              onClick={() => alFiltrar(vista === 'regla' ? { ...filtro, regla: elegida === f.clave ? undefined : f.clave } : { ...filtro, donde: elegida === f.clave ? undefined : f.clave })}
            >
              <span className="nombre">{f.clave}</span>
              <span className="pista">
                <span className="relleno-barra" style={{ display: 'block', width: `${(f.n / maximo) * 100}%`, background: f.color }} />
              </span>
              <span className="n">{f.n}</span>
            </div>
          ))}
        </div>
      )}
      {filas.length > FILAS && <p className="ayuda" style={{ marginTop: 8 }}>Y {filas.length - FILAS} más, con menos avisos.</p>}
    </div>
  );
}
