import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// One origin: in development Vite (5173) forwards /api to the ASP.NET Core API (5080), so the browser
// only ever talks to one site and no CORS setup is needed. For the demo, `npm run build` writes the
// finished app into the API's wwwroot folder and the API serves it itself.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: { '/api': 'http://localhost:5080' },
  },
  build: {
    outDir: '../src/Ims.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
  },
})
