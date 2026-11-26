import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { App } from '../src/App';
import { leerHistorial } from '../src/datos';
import { ejemplos } from './apoyo';

// El panel entero pintado en el servidor, sin navegador: lo justo para ver que cada parte sale con los datos.
const texto = (html: string) => html.replace(/<[^>]+>/g, ' ').replace(/&#x27;/g, "'").replace(/\s+/g, ' ');

describe('App', () => {
  it('sin datos explica cómo empezar el historial', () => {
    const html = texto(renderToString(<App />));

    expect(html).toContain('Abrir la carpeta del historial');
    expect(html).toContain('--historial');
  });

  it('con el historial de la 1.4 enseña el último análisis, la evolución y sus partes', () => {
    const { proyectos } = leerHistorial(ejemplos);
    const html = texto(renderToString(<App inicial={proyectos} />));

    // Cinco carpetas: hay selector de proyecto, y se abre la primera (la 1.4), en su último análisis.
    expect(html).toContain('Proyecto');
    expect(html).toContain('El quality gate pasa');
    expect(html).toContain('Avisos sin justificar 10');
    expect(html).toContain('73 menos que en el anterior');
    for (const titulo of ['Evolución', 'Avisos por regla', 'Dónde se concentran', 'Funciones', 'Configuración', 'card-lint.json']) {
      expect(html).toContain(titulo);
    }
  });

  it('con un gate que falla lo dice arriba', () => {
    const { proyectos } = leerHistorial(ejemplos.filter((f) => f.nombre.includes('2.3')));
    const html = texto(renderToString(<App inicial={proyectos} />));

    expect(html).toContain('El quality gate no pasa');
    expect(html).toContain('Solo hay un análisis de esta carpeta');
  });
});
