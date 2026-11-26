import { useState } from 'react';
import type { Configuracion, MetricasFuncion, UmbralesJavaCard } from '../datos';
import { NOMBRE_METRICA, medidas } from '../resumen';
import { nombreFichero, plural } from '../formato';

const FILAS = 15;

/**
 * Cada métrica de cada función frente a su umbral. La barra azul llega hasta el umbral y lo que se pasa va en
 * naranja; la raya negra es el umbral.
 */
export function Funciones({ funciones, configuracion, javacard }: { funciones: MetricasFuncion[]; configuracion: Configuracion; javacard: UmbralesJavaCard }) {
  const [todas, setTodas] = useState(false);
  const [lenguaje, setLenguaje] = useState<'todos' | 'c' | 'javacard'>('todos');
  const [verTodas, setVerTodas] = useState(false);

  const lenguajes = new Set(funciones.map((f) => f.lenguaje));
  const deLenguaje = funciones.filter((f) => lenguaje === 'todos' || f.lenguaje === lenguaje);
  const lista = medidas(deLenguaje, configuracion, javacard);
  const avisan = lista.filter((m) => m.supera);
  const conAviso = new Set(avisan.map((m) => m.funcion)).size;
  const mostradas = todas ? lista : avisan;
  const filas = verTodas ? mostradas : mostradas.slice(0, FILAS);

  return (
    <div className="tarjeta seccion">
      <div className="titular">
        <h2>Funciones</h2>
        <span className="ayuda" style={{ margin: 0 }}>
          {plural(deLenguaje.length, 'función medida', 'funciones medidas')} · {conAviso} con algún umbral pasado
        </span>
        <span className="relleno" />
        {lenguajes.size > 1 && (
          <div className="conmutador">
            {(['todos', 'c', 'javacard'] as const).map((l) => (
              <button key={l} className={lenguaje === l ? 'activo' : ''} onClick={() => setLenguaje(l)}>
                {{ todos: 'C y Java Card', c: 'C', javacard: 'Java Card' }[l]}
              </button>
            ))}
          </div>
        )}
        <div className="conmutador">
          <button className={!todas ? 'activo' : ''} onClick={() => setTodas(false)}>Las que avisan</button>
          <button className={todas ? 'activo' : ''} onClick={() => setTodas(true)}>Todas</button>
        </div>
      </div>
      <p className="ayuda">
        Primero las que más se pasan de su umbral. En C avisa lo que pasa del umbral; en Java Card, como en PMD, ya avisa el propio umbral.
      </p>
      {filas.length === 0 ? (
        <p className="vacio">{todas ? 'Este análisis no trae funciones medidas.' : 'Ninguna función pasa de sus umbrales.'}</p>
      ) : (
        <div className="funciones">
          <span className="cab">Función</span>
          <span className="cab">Métrica</span>
          <span className="cab">Frente al umbral</span>
          <span className="cab" style={{ textAlign: 'right' }}>Valor</span>
          {filas.map((m) => {
            const escala = Math.max(m.valor, m.umbral) * 1.1;
            const dentro = Math.min(m.valor, m.umbral);
            return (
              <div key={`${m.funcion.fichero}:${m.funcion.linea}:${m.funcion.nombre}:${m.metrica}`} style={{ display: 'contents' }}>
                <span className="nombre" title={`${m.funcion.fichero}:${m.funcion.linea}`}>
                  {m.funcion.nombre}
                  <small>{nombreFichero(m.funcion.fichero)}:{m.funcion.linea}</small>
                </span>
                <span className="metrica">{NOMBRE_METRICA[m.metrica]}</span>
                <span className="medidor" aria-hidden="true">
                  <span className="dentro" style={{ width: `${(dentro / escala) * 100}%`, borderRadius: m.valor > m.umbral ? '5px 0 0 5px' : 5 }} />
                  {m.valor > m.umbral && (
                    <span className="fuera" style={{ left: `${(m.umbral / escala) * 100}%`, width: `${((m.valor - m.umbral) / escala) * 100}%` }} />
                  )}
                  <span className="marca" style={{ left: `calc(${(m.umbral / escala) * 100}% - 1px)` }} />
                </span>
                <span className="valor">{m.valor} / {m.umbral}</span>
              </div>
            );
          })}
        </div>
      )}
      {mostradas.length > FILAS && (
        <button className="boton" style={{ marginTop: 10 }} onClick={() => setVerTodas(!verTodas)}>
          {verTodas ? 'Ver solo las primeras' : `Ver las ${mostradas.length}`}
        </button>
      )}
    </div>
  );
}
