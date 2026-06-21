import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
// PWA plugin disabled in sandbox build — re-enable on deploy
// import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
  ],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
  },
  preview: {
    host: '127.0.0.1',
    port: 4173,
    strictPort: true,
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return
          }

          if (id.includes('react-router')) {
            return 'router'
          }

          if (id.includes('recharts')) {
            return 'charts'
          }

          if (id.includes('lucide-react')) {
            return 'icons'
          }

          if (id.includes('axios')) {
            return 'network'
          }

          if (id.includes('zustand')) {
            return 'state'
          }

          if (id.includes('date-fns')) {
            return 'date-utils'
          }

          if (id.includes('react') || id.includes('react-dom')) {
            return 'react-vendor'
          }

          return 'vendor'
        },
      },
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
})
