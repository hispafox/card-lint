import { useState } from 'react';
import type { Aviso, Gravedad, Justificado } from '../datos';
import { nivel } from '../datos';
import { dondeSeExplica } from '../resumen';
import { COLOR_GRAVEDAD, NOMBRE_GRAVEDAD } from '../formato';

export interface Filtro {
  regla?: string;
  donde?: string;
  fichero?: string;
  gravedad?: Gravedad;
}

const MAXIMO = 300;

const cumple = (a: Aviso, f: Filtro): boolean =>
  (!f.regla || a.regla === f.regla) &&
  (!f.donde || dondeSeExplica(a.submodulo) === f.donde) &&
  (!f.fichero || a.fichero === f.fichero) &&
  (!f.gravedad || a.gravedad === f.gravedad);

const orden = (a: Aviso, b: Aviso): number => nivel(b.gravedad) - nivel(a.gravedad) || a.fichero.localeCompare(b.fichero) || a.linea - b.linea;

/** Los avisos en tabla, con los filtros que se eligen pulsando en los gráficos. */
export function TablaAvisos({ avisos, justificados, filtro, alFiltrar }: { avisos: Aviso[]; justificados: Justificado[]; filtro: Filtro; alFiltrar: (f: Filtro) => void }) {
  const [verJustificados, setVerJustificados] = useState(false);
  const lista = avisos.filter((a) => cumple(a, filtro)).sort(orden);
  const chips: [keyof Filtro, string][] = [
    ['gravedad', filtro.gravedad ? NOMBRE_GRAVEDAD[filtro.gravedad] : ''],
    ['regla', filtro.regla ?? ''],
    ['donde', filtro.donde ?? ''],
    ['fichero', filtro.fichero ?? ''],
  ];

  return (
    <div className="tarjeta seccion">
      <div className="titular">
        <h2>Avisos sin justificar</h2>
        <span className="ayuda" style={{ margin: 0 }}>{lista.length} de {avisos.length}</span>
        <span className="relleno" />
        {justificados.length > 0 && (
          <button className="boton" onClick={() => setVerJustificados(!verJustificados)}>
            {verJustificados ? 'Ocultar' : 'Ver'} los {justificados.length} justificados por una contramedida
          </button>
        )}
      </div>
      <div className="filtros">
        {chips.filter(([, texto]) => texto).length === 0 ? (
          <span className="ayuda" style={{ margin: 0 }}>Pulsa una barra o una celda de los gráficos para filtrar.</span>
        ) : (
          chips
            .filter(([, texto]) => texto)
            .map(([clave, texto]) => (
              <span key={clave} className="chip">
                {texto}
                <button aria-label={`Quitar el filtro ${texto}`} onClick={() => alFiltrar({ ...filtro, [clave]: undefined })}>✕</button>
              </span>
            ))
        )}
      </div>
      <table className="tabla">
        <thead>
          <tr><th>Gravedad</th><th>Regla</th><th>Dónde</th><th>Mensaje</th><th>Se explica en</th></tr>
        </thead>
        <tbody>
          {lista.slice(0, MAXIMO).map((a, i) => (
            <tr key={`${a.regla}:${a.fichero}:${a.linea}:${i}`}>
              <td><span className="gravedad"><i className="punto" style={{ background: COLOR_GRAVEDAD[a.gravedad] }} />{NOMBRE_GRAVEDAD[a.gravedad]}</span></td>
              <td>{a.regla}</td>
              <td className="donde">{a.fichero}:{a.linea}</td>
              <td title={a.detalle}>{a.mensaje}</td>
              <td>{a.submodulo ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {lista.length > MAXIMO && <p className="ayuda">Salen los {MAXIMO} primeros; filtra para ver el resto.</p>}
      {verJustificados && (
        <>
          <h2 style={{ marginTop: 18 }}>Justificados por una contramedida</h2>
          <table className="tabla">
            <thead>
              <tr><th>Regla</th><th>Dónde</th><th>Mensaje</th><th>Contramedida</th></tr>
            </thead>
            <tbody>
              {justificados.map((j, i) => (
                <tr key={i}>
                  <td>{j.aviso.regla}</td>
                  <td className="donde">{j.aviso.fichero}:{j.aviso.linea}</td>
                  <td>{j.aviso.mensaje}</td>
                  <td>{j.contramedida.motivo || 'sin motivo'} (líneas {j.contramedida.desde}–{j.contramedida.hasta})</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
