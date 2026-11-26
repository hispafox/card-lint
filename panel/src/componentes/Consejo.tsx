import { useRef, useState, type MouseEvent, type ReactNode } from 'react';

/** El recuadro que sale al pasar el ratón por una marca de un gráfico, colocado sobre el puntero. */
export function useConsejo() {
  const ref = useRef<HTMLDivElement>(null);
  const [consejo, setConsejo] = useState<{ x: number; y: number; contenido: ReactNode } | null>(null);

  const mostrar = (e: MouseEvent, contenido: ReactNode) => {
    const caja = ref.current?.getBoundingClientRect();
    if (caja) setConsejo({ x: e.clientX - caja.left, y: e.clientY - caja.top, contenido });
  };

  const nodo = consejo && (
    <div className="consejo" style={{ left: consejo.x, top: consejo.y }} role="tooltip">
      {consejo.contenido}
    </div>
  );

  return { ref, mostrar, ocultar: () => setConsejo(null), nodo };
}
