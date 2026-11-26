import type { Aviso, Gravedad } from '../datos';
import { porFichero } from '../resumen';
import { COLOR_GRAVEDAD, GRAVEDADES_DE_MAS_A_MENOS, NOMBRE_GRAVEDAD, PLURAL_GRAVEDAD } from '../formato';
import type { Filtro } from './TablaAvisos';

const FICHEROS = 10;

/** El paso de la rampa (1 a 6) que toca a una cifra, en proporción a la mayor del mapa. */
const paso = (n: number, maximo: number): number => Math.max(1, Math.ceil((n / maximo) * 6));

/** Los ficheros con más avisos, cruzados con la gravedad. Más oscuro, más avisos. */
export function MapaFicheros({ avisos, filtro, alFiltrar }: { avisos: Aviso[]; filtro: Filtro; alFiltrar: (f: Filtro) => void }) {
  const filas = porFichero(avisos, FICHEROS);
  const maximo = Math.max(1, ...filas.flatMap((f) => GRAVEDADES_DE_MAS_A_MENOS.map((g) => f.cuenta[g])));
  const elegir = (fichero: string, gravedad: Gravedad) =>
    alFiltrar(filtro.fichero === fichero && filtro.gravedad === gravedad ? { ...filtro, fichero: undefined, gravedad: undefined } : { ...filtro, fichero, gravedad });

  return (
    <div className="tarjeta">
      <h2>Dónde se concentran</h2>
      <p className="ayuda">Los {FICHEROS} ficheros con más avisos, con la cifra de cada gravedad. Pulsa una celda para ver sus avisos.</p>
      {filas.length === 0 ? (
        <p className="vacio">No queda ningún aviso sin justificar.</p>
      ) : (
        <div className="mapa">
          <span />
          {GRAVEDADES_DE_MAS_A_MENOS.map((g) => (
            <span key={g} className="cabeza"><i className="punto" style={{ background: COLOR_GRAVEDAD[g] }} />{NOMBRE_GRAVEDAD[g]}</span>
          ))}
          {filas.map((f) => (
            <FilaMapa key={f.fichero} fichero={f.fichero} cuenta={f.cuenta} maximo={maximo} filtro={filtro} alElegir={elegir} />
          ))}
        </div>
      )}
    </div>
  );
}

function FilaMapa({ fichero, cuenta, maximo, filtro, alElegir }: {
  fichero: string;
  cuenta: Record<Gravedad, number>;
  maximo: number;
  filtro: Filtro;
  alElegir: (fichero: string, g: Gravedad) => void;
}) {
  return (
    <>
      <span className="fichero" title={fichero}>{fichero}</span>
      {GRAVEDADES_DE_MAS_A_MENOS.map((g) => {
        const n = cuenta[g];
        if (n === 0) return <span key={g} className="celda cero">0</span>;
        const p = paso(n, maximo);
        const elegida = filtro.fichero === fichero && filtro.gravedad === g;
        return (
          <span
            key={g}
            className="celda"
            title={`${fichero}: ${n} de ${PLURAL_GRAVEDAD[g].toLowerCase()}`}
            style={{ background: `var(--mapa-${p})`, color: `var(--mapa-t${p})`, outline: elegida ? '2px solid var(--tinta)' : undefined }}
            onClick={() => alElegir(fichero, g)}
          >
            {n}
          </span>
        );
      })}
    </>
  );
}
