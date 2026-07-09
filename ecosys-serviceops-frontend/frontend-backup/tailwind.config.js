/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ["./index.html", "./src/**/*.{js,jsx,ts,tsx}"],
  theme: {
    extend: {
      colors: {
        app: {
          bg: 'var(--app-bg)',
          surface: 'var(--app-surface)',
          panel: 'var(--app-panel)',
          card: 'var(--app-card)',
          border: 'var(--app-border)',
          borderStrong: 'var(--app-border-strong)',
          text: 'var(--app-text)',
          muted: 'var(--app-muted)',
          secondary: 'var(--app-secondary)',
          primary: 'var(--app-primary)',
          primarySoft: 'var(--app-primary-soft)',
          lime: 'var(--app-lime)',
          gold: 'var(--app-gold)',
          success: 'var(--app-success)',
          warning: 'var(--app-warning)',
          danger: 'var(--app-danger)',
        },
      },
      fontFamily: {
        sans: ['Inter', 'Segoe UI', 'system-ui', 'sans-serif'],
        heading: ['Manrope', 'Inter', 'Segoe UI', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'SFMono-Regular', 'monospace'],
      },
      borderRadius: {
        app: 'var(--radius-app)',
        card: 'var(--radius-card)',
        control: 'var(--radius-control)',
      },
      boxShadow: {
        app: 'var(--shadow-app)',
        card: 'var(--shadow-card)',
        elevated: 'var(--shadow-elevated)',
      },
      maxWidth: {
        container: 'var(--content-max)',
      },
    },
  },
  plugins: [],
}
