import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      srcDir: 'src/pwa',
      filename: 'sw.ts',
      includeAssets: [],
      manifest: {
        name: 'ECOSYS',
        short_name: 'Ecosys',
        start_url: '/',
        display: 'standalone',
        orientation: 'portrait',
        background_color: '#F6F5EF',
        theme_color: '#15806A',
        icons: [
          { src: '/ecosys-rosette.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
          { src: '/ecosys-icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'maskable' },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,svg,png,ico}'],
      },
    }),
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
            return undefined
          }
          if (id.includes('react-router')) return 'router'
          if (id.includes('@tanstack/react-query') || id.includes('@tanstack/react-virtual')) return 'query'
          if (id.includes('lucide-react')) return 'icons'
          if (id.includes('date-fns')) return 'date'
          if (id.includes('axios')) return 'network'
          if (id.includes('idb')) return 'offline'
          if (id.includes('react-hook-form') || id.includes('zod') || id.includes('@hookform/resolvers')) return 'forms'
          if (id.includes('react-hot-toast')) return 'toast'
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
