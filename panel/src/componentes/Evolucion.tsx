import type { Cuenta } from '../resumen';
import { total } from '../resumen';
import { COLOR_GRAVEDAD, GRAVEDADES_DE_MAS_A_MENOS, NOMBRE_GRAVEDAD, PLURAL_GRAVEDAD, fechaCorta, fechaLarga } from '../formato';
import { useConsejo } from './Consejo';

interface Punto {
  fecha: string;
  cuenta: Cuenta;
  gatePasa: boolean;
}

// Del ancho de la tarjeta en un escritorio, para que el texto del SVG salga a su tamaño.
const ANCHO = 1160;
const ALTO = 250;
const M = { izquierda: 34, derecha: 8, arriba: 24, abajo: 30 };

/** Un paso redondo para la rejilla: 1, 2, 5, 10, 20, 50… que deje unas cuatro líneas. */
function paso(maximo: number): number {
  const bruto = Math.max(1, maximo / 4);
  const base = 10 ** Math.floor(Math.log10(bruto));
  return [1, 2, 5, 10].map((f) => f * base).find((p) => p >= bruto) ?? 10 * base;
}

/** Un rectángulo con las esquinas de arriba redondeadas: el extremo de la columna, no su base. */
function columna(x: number, y: number, w: number, h: number, r: number): string {
  const rr = Math.min(r, h, w / 2);
  return `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + w - rr}Q${x + w},${y} ${x + w},${y + rr}V${y + h}Z`;
}

/** Los avisos sin justificar de cada análisis, por gravedad, con el gate encima. */
export function Evolucion({ serie, elegido, alElegir }: { serie: Punto[]; elegido: number; alElegir: (i: number) => void }) {
  const { ref, mostrar, ocultar, nodo } = useConsejo();
  const maximo = Math.max(1, ...serie.map((p) => total(p.cuenta)));
  const p = paso(maximo);
  const tope = Math.ceil(maximo / p) * p;
  const anchoUtil = ANCHO - M.izquierda - M.derecha;
  const altoUtil = ALTO - M.arriba - M.abajo;
  const banda = anchoUtil / serie.length;
  const ancho = Math.max(3, Math.min(46, banda * 0.62));
  const y = (v: number) => M.arriba + altoUtil - (v / tope) * altoUtil;
  const cadaCuantas = Math.max(1, Math.ceil(serie.length / 8));

  return (
    <div className="grafico" ref={ref}>
      <svg viewBox={`0 0 ${ANCHO} ${ALTO}`} role="img" aria-label="Avisos sin justificar en cada análisis">
        {Array.from({ length: tope / p + 1 }, (_, i) => i * p).map((v) => (
          <g key={v}>
            <line className={v === 0 ? 'eje' : 'rejilla-linea'} x1={M.izquierda} x2={ANCHO - M.derecha} y1={y(v)} y2={y(v)} />
            <text x={M.izquierda - 6} y={y(v) + 4} textAnchor="end">{v}</text>
          </g>
        ))}

        {serie.map((punto, i) => {
          const x = M.izquierda + banda * i + (banda - ancho) / 2;
          let base = 0;
          const tramos = GRAVEDADES_DE_MAS_A_MENOS.filter((g) => punto.cuenta[g] > 0);
          return (
            <g key={punto.fecha + i}>
              {i === elegido && serie.length > 1 && <rect x={M.izquierda + banda * i + 1} y={M.arriba - 18} width={banda - 2} height={altoUtil + 18} rx={6} fill="var(--azul-claro)" opacity={0.55} />}
              {tramos.map((g, j) => {
                const desde = base;
                base += punto.cuenta[g];
                const arriba = y(base);
                // 2 px de superficie entre tramos: cada uno deja libre su borde de arriba, salvo el último.
                const alto = Math.max(0, y(desde) - arriba - (j < tramos.length - 1 ? 2 : 0));
                const yTramo = j < tramos.length - 1 ? arriba + 2 : arriba;
                return j === tramos.length - 1 ? (
                  <path key={g} d={columna(x, yTramo, ancho, alto, 4)} fill={COLOR_GRAVEDAD[g]} />
                ) : (
                  <rect key={g} x={x} y={yTramo} width={ancho} height={alto} fill={COLOR_GRAVEDAD[g]} />
                );
              })}
              <text x={x + ancho / 2} y={y(total(punto.cuenta)) - 6} textAnchor="middle" style={{ fill: punto.gatePasa ? 'var(--bien-texto)' : 'var(--mal-texto)', fontWeight: 700 }}>
                {punto.gatePasa ? '✓' : '✕'}
              </text>
              {i % cadaCuantas === 0 && (
                <text x={x + ancho / 2} y={ALTO - 10} textAnchor="middle">{fechaCorta(punto.fecha)}</text>
              )}
              <rect
                className="clicable"
                x={M.izquierda + banda * i}
                y={M.arriba - 18}
                width={banda}
                height={altoUtil + 18}
                fill="transparent"
                onClick={() => alElegir(i)}
                onMouseMove={(e) =>
                  mostrar(
                    e,
                    <>
                      <strong>{fechaLarga(punto.fecha)}</strong>
                      {GRAVEDADES_DE_MAS_A_MENOS.map((g) => (
                        <div key={g}>{PLURAL_GRAVEDAD[g]}: {punto.cuenta[g]}</div>
                      ))}
                      <div>Quality gate: {punto.gatePasa ? 'pasa' : 'no pasa'}</div>
                    </>,
                  )
                }
                onMouseLeave={ocultar}
              />
            </g>
          );
        })}
      </svg>
      {nodo}
      <div className="leyenda">
        {GRAVEDADES_DE_MAS_A_MENOS.map((g) => (
          <span key={g}><i className="punto" style={{ background: COLOR_GRAVEDAD[g] }} />{NOMBRE_GRAVEDAD[g]}</span>
        ))}
        <span>✓ el gate pasa · ✕ no pasa</span>
      </div>
    </div>
  );
}
