import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    react(),
    // Precache-only PWA. `generateSW` (the default strategy) emits a Workbox service worker that
    // precaches the built shell and serves it as the navigation fallback, so the app opens without
    // a signal. Offline *data and writes* are not the service worker's job: the technician app
    // keeps its jobs in IndexedDB and queues its actions in an outbox it replays when back online
    // (src/features/tech/offline). Office screens still need a connection. Push notifications are
    // handled by public/push-sw.js, which the generated worker imports.
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      includeAssets: [],
      manifest: {
        id: '/',
        name: 'ECOSYS',
        short_name: 'Ecosys',
        description: 'Field service operations: work orders, technicians, assets and billing.',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        orientation: 'portrait',
        background_color: '#F6F5EF',
        theme_color: '#0F766E',
        // Chrome on Android only offers to install (and builds the home-screen app) from PNG icons
        // of at least 192px and 512px; an SVG-only manifest is not installable there. The PNGs are
        // rendered from logo-icon.svg; the maskable one keeps the mark inside the 80% safe zone.
        icons: [
          { src: '/pwa-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: '/pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: '/pwa-maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          { src: '/logo-icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,svg,png,ico}'],
        // Shows "new job assigned" push notifications and opens the job when one is tapped.
        importScripts: ['push-sw.js'],
        // The main bundle is over Workbox's 2 MiB default; without it in the precache the installed
        // app cannot open offline at all, which technicians on the factory floor rely on.
        maximumFileSizeToCacheInBytes: 4 * 1024 * 1024,
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
    // Emit hashed bundles under dist/static/ instead of the default dist/assets/. The SPA has an
    // `/assets` route (the Assets module); with the default 'assets' dir, nginx `try_files` resolves
    // `/assets` to the real static directory and returns 403 on refresh/deep-link. 'static' avoids
    // the collision so `/assets` falls through to index.html.
    assetsDir: 'static',
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
