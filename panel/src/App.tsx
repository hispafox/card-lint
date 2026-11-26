import { useMemo, useState, type DragEvent } from 'react';
import type { Analisis, Configuracion, Fichero, Proyecto } from './datos';
import { leerHistorial } from './datos';
import { recalcular, type Resultado } from './recalcular';
import { contar, evolucion } from './resumen';
import { COLOR_GRAVEDAD, GRAVEDADES_DE_MAS_A_MENOS, PLURAL_GRAVEDAD, fechaMedia, plural } from './formato';
import { ConfiguracionPanel } from './componentes/ConfiguracionPanel';
import { Evolucion } from './componentes/Evolucion';
import { Funciones } from './componentes/Funciones';
import { MapaFicheros } from './componentes/MapaFicheros';
import { PorRegla } from './componentes/PorRegla';
import { TablaAvisos, type Filtro } from './componentes/TablaAvisos';

async function leer(ficheros: File[]): Promise<Fichero[]> {
  return Promise.all(
    ficheros.filter((f) => f.name.toLowerCase().endsWith('.json')).map(async (f) => ({ nombre: f.webkitRelativePath || f.name, texto: await f.text() })),
  );
}

/** Los ficheros de lo que se suelta en la página, entrando en las carpetas. */
async function soltados(datos: DataTransfer): Promise<File[]> {
  // Las entradas hay que pedirlas antes del primer await: después el navegador ya no las da.
  const entradas = [...datos.items].map((i) => i.webkitGetAsEntry()).filter((e): e is FileSystemEntry => e !== null);
  if (entradas.length === 0) return [...datos.files];

  const ficheros: File[] = [];
  const recorrer = async (entrada: FileSystemEntry): Promise<void> => {
    if (entrada.isFile) {
      ficheros.push(await new Promise<File>((ok, mal) => (entrada as FileSystemFileEntry).file(ok, mal)));
      return;
    }
    const lector = (entrada as FileSystemDirectoryEntry).createReader();
    for (;;) {
      const lote = await new Promise<FileSystemEntry[]>((ok, mal) => lector.readEntries(ok, mal));
      if (lote.length === 0) return;
      for (const hija of lote) await recorrer(hija);
    }
  };
  for (const e of entradas) await recorrer(e);
  return ficheros;
}

function BotonesAbrir({ alAbrir, principal, cortos }: { alAbrir: (f: File[]) => void; principal?: boolean; cortos?: boolean }) {
  return (
    <>
      <label className={'boton' + (principal ? ' principal' : '')}>
        {cortos ? 'Abrir carpeta…' : 'Abrir la carpeta del historial'}
        <input type="file" multiple ref={(el) => el?.setAttribute('webkitdirectory', '')} onChange={(e) => alAbrir([...(e.target.files ?? [])])} />
      </label>
      <label className="boton">
        {cortos ? 'Abrir JSON…' : 'Abrir ficheros JSON'}
        <input type="file" multiple accept=".json,application/json" onChange={(e) => alAbrir([...(e.target.files ?? [])])} />
      </label>
    </>
  );
}

export function App({ inicial = [] }: { inicial?: Proyecto[] }) {
  const [proyectos, setProyectos] = useState<Proyecto[]>(inicial);
  const [errores, setErrores] = useState<string[]>([]);
  const [iProyecto, setIProyecto] = useState(0);
  const [iAnalisis, setIAnalisis] = useState(Math.max(0, (inicial[0]?.analisis.length ?? 1) - 1));
  const [configuracion, setConfiguracion] = useState<Configuracion | null>(inicial[0]?.analisis.at(-1)?.configuracion ?? null);
  const [filtro, setFiltro] = useState<Filtro>({});
  const [soltando, setSoltando] = useState(false);

  const elegirProyecto = (lista: Proyecto[], i: number) => {
    const ultimo = lista[i]?.analisis.at(-1);
    setIProyecto(i);
    setIAnalisis(Math.max(0, (lista[i]?.analisis.length ?? 1) - 1));
    setConfiguracion(ultimo?.configuracion ?? null);
    setFiltro({});
  };

  const abrir = async (ficheros: File[]) => {
    const { proyectos: nuevos, errores: fallidos } = leerHistorial(await leer(ficheros));
    if (nuevos.length === 0 && fallidos.length === 0) fallidos.push('No había ningún fichero .json.');
    setErrores(fallidos);
    if (nuevos.length > 0) {
      setProyectos(nuevos);
      elegirProyecto(nuevos, 0);
    }
  };

  const alSoltar = async (e: DragEvent) => {
    e.preventDefault();
    setSoltando(false);
    await abrir(await soltados(e.dataTransfer));
  };

  const arrastre = {
    onDragOver: (e: DragEvent) => {
      e.preventDefault();
      setSoltando(true);
    },
    onDragLeave: () => setSoltando(false),
    onDrop: alSoltar,
  };

  const proyecto = proyectos[iProyecto];
  const analisis = proyecto?.analisis[iAnalisis];
  const serie = useMemo(() => (proyecto && configuracion ? evolucion(proyecto.analisis, configuracion) : []), [proyecto, configuracion]);

  if (!proyecto || !analisis || !configuracion) {
    return (
      <div className={'panel portada' + (soltando ? ' soltar' : '')} {...arrastre}>
        <h1>card-lint · cuadro de mando</h1>
        <p>
          Abre la carpeta donde card-lint guarda el historial. Cada análisis que lanzas con <code>--historial</code> deja allí un JSON, y el panel
          los junta en una serie para que veas cómo evolucionan los avisos y dónde se concentran.
        </p>
        <div className="acciones">
          <BotonesAbrir alAbrir={abrir} principal />
        </div>
        <p>O suelta aquí la carpeta. Para empezar el historial de un proyecto:</p>
        <pre>card-lint.exe analizar F:\proyectos\monedero --salida informe.html --historial F:\proyectos\monedero-historial</pre>
        <p className="ayuda">Los ficheros se leen aquí mismo, en el navegador. No se envía nada a ningún sitio.</p>
        {errores.map((e) => <p key={e} className="errores">{e}</p>)}
      </div>
    );
  }

  const actual = recalcular(analisis, configuracion);
  const anterior = iAnalisis > 0 ? recalcular(proyecto.analisis[iAnalisis - 1], configuracion) : null;

  return (
    <div className={'panel' + (soltando ? ' soltar' : '')} {...arrastre}>
      <header className="cabecera">
        <h1>card-lint <span>· cuadro de mando</span></h1>
        {proyectos.length > 1 && (
          <label className="selector">
            Proyecto
            <select value={iProyecto} onChange={(e) => elegirProyecto(proyectos, Number(e.target.value))}>
              {proyectos.map((p, i) => <option key={p.carpeta} value={i}>{p.nombre}</option>)}
            </select>
          </label>
        )}
        <label className="selector" title={proyecto.carpeta}>
          {proyectos.length > 1 ? 'Análisis' : `Análisis de ${proyecto.nombre}`}
          <select value={iAnalisis} onChange={(e) => { setIAnalisis(Number(e.target.value)); setFiltro({}); }}>
            {proyecto.analisis.map((a, i) => <option key={a.fecha + i} value={i}>{fechaMedia(a.fecha)}</option>)}
          </select>
        </label>
        <span className={'gate ' + (actual.gatePasa ? 'pasa' : 'falla')}>
          {actual.gatePasa ? '✓ El quality gate pasa' : '✕ El quality gate no pasa'}
        </span>
        <span className="relleno" />
        <BotonesAbrir alAbrir={abrir} cortos />
      </header>

      {errores.length > 0 && <div className="notas">{errores.map((e) => <p key={e} className="errores">{e}</p>)}</div>}
      {analisis.notas.length > 0 && <div className="notas">{analisis.notas.map((n) => <p key={n}>{n}</p>)}</div>}

      <Kpis analisis={analisis} actual={actual} anterior={anterior} />

      <div className="tarjeta">
        <h2>Evolución</h2>
        <p className="ayuda">
          {serie.length === 1
            ? 'Solo hay un análisis de esta carpeta. Cada vez que card-lint vuelva a analizarla con la misma carpeta de --historial, saldrá aquí una columna más.'
            : 'Los avisos sin justificar de cada análisis, recalculados con la configuración de abajo. Pulsa una columna para ver ese análisis.'}
        </p>
        <Evolucion serie={serie} elegido={iAnalisis} alElegir={(i) => { setIAnalisis(i); setFiltro({}); }} />
      </div>

      <div className="rejilla dos seccion">
        <PorRegla avisos={actual.avisos} filtro={filtro} alFiltrar={setFiltro} />
        <MapaFicheros avisos={actual.avisos} filtro={filtro} alFiltrar={setFiltro} />
      </div>

      <Funciones funciones={analisis.funciones} configuracion={configuracion} javacard={analisis.umbralesJavaCard} />

      <ConfiguracionPanel
        configuracion={configuracion}
        original={analisis.configuracion}
        avisosAntes={analisis.avisos.length}
        avisosAhora={actual.avisos.length}
        alCambiar={setConfiguracion}
      />

      <TablaAvisos avisos={actual.avisos} justificados={actual.justificados} filtro={filtro} alFiltrar={setFiltro} />
    </div>
  );
}

function Kpis({ analisis, actual, anterior }: { analisis: Analisis; actual: Resultado; anterior: Resultado | null }) {
  const cuenta = contar(actual.avisos);
  const n = actual.avisos.length;
  const diferencia = anterior ? n - anterior.avisos.length : null;
  const duplicados = actual.avisos.filter((a) => a.regla === 'cpd:duplicado').length;

  return (
    <div className="kpis">
      <div className="kpi grande">
        <div className="etiqueta">Avisos sin justificar</div>
        <div className="cifra">{n}</div>
        <div className="pie">
          {diferencia === null ? (
            'Es el primer análisis de esta carpeta'
          ) : diferencia === 0 ? (
            'Los mismos que en el análisis anterior'
          ) : (
            <span className={diferencia < 0 ? 'baja' : 'sube'}>
              {diferencia < 0 ? '▼' : '▲'} {Math.abs(diferencia)} {diferencia < 0 ? 'menos' : 'más'} que en el anterior
            </span>
          )}
        </div>
      </div>
      {GRAVEDADES_DE_MAS_A_MENOS.map((g) => (
        <div className="kpi" key={g}>
          <div className="etiqueta"><i className="punto" style={{ background: COLOR_GRAVEDAD[g] }} />{PLURAL_GRAVEDAD[g]}</div>
          <div className="cifra">{cuenta[g]}</div>
        </div>
      ))}
      <div className="kpi">
        <div className="etiqueta">Código duplicado</div>
        <div className="cifra">{duplicados}</div>
        <div className="pie">{duplicados === 1 ? 'aviso de CPD' : 'avisos de CPD'}</div>
      </div>
      <div className="kpi">
        <div className="etiqueta">Contramedidas</div>
        <div className="cifra">{analisis.contramedidas.length}</div>
        <div className="pie">{plural(actual.justificados.length, 'aviso justificado', 'avisos justificados')}</div>
      </div>
    </div>
  );
}
