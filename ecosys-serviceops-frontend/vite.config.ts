import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    react(),
    // Precache-only PWA. `generateSW` (the default strategy) emits a Workbox service worker that
    // precaches the built shell and serves it as the navigation fallback — a real load-time win,
    // and it keeps working. It does NOT give the app offline *writes*: every mutation goes
    // straight to the API. There used to be a hand-written `src/pwa/sw.ts` pointed at by
    // `srcDir`/`filename`; those options only apply to the `injectManifest` strategy, so that file
    // was never in the bundle — and had it been, its blanket `fetch` pass-through would have
    // disabled precaching entirely. Both it and the never-imported IndexedDB sync layer are gone.
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      includeAssets: [],
      manifest: {
        name: 'ECOSYS',
        short_name: 'Ecosys',
        start_url: '/',
        display: 'standalone',
        orientation: 'portrait',
    background_color: '#F6F5EF',
    theme_color: '#0F766E',
        icons: [
          { src: '/logo-icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
          { src: '/logo-icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'maskable' },
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
