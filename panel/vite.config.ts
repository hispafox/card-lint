import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { viteSingleFile } from 'vite-plugin-singlefile';

// Un solo HTML con el JavaScript y el CSS dentro: Chrome no carga módulos desde file://, y el panel
// se abre sin servidor, como el informe de card-lint.
export default defineConfig({
  plugins: [react(), viteSingleFile()],
  test: {
    environment: 'node',
    include: ['pruebas/**/*.test.ts', 'pruebas/**/*.test.tsx'],
  },
});
