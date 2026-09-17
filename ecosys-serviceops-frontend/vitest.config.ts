import { defineConfig } from 'vitest/config'

// Separate from vite.config.ts on purpose: these are plain unit tests for pure logic modules
// (dedup/error-classification/state stores), not component tests, so no React/jsdom/PWA plugin is
// needed and the production build config stays untouched.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
