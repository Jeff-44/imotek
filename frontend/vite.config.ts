import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const api = { target: 'http://localhost:5056', changeOrigin: true }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Same routes as nginx in production (frontend/nginx/default.conf).
    proxy: {
      '/api': api,
      '/media': api,
      '^/health(/db)?$': api,
    },
  },
})
