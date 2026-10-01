import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Forward API calls to the ScrapGo.Core.Api backend during development.
    // This keeps the browser on a single origin, so the backend's CORS
    // allow-list (which only permits http://localhost:3000) is never hit.
    proxy: {
      '/api': {
        target: 'http://localhost:5141',
        changeOrigin: true,
      },
    },
  },
})
