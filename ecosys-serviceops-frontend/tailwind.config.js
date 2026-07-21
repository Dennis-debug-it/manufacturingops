/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: { DEFAULT: '#15806A', light: '#2C9B6F' },
        accent: '#3EAD73',
        success: '#27AE60',
        warning: '#F39C12',
        danger: '#E74C3C',
        gray: {
          900: '#2C3E50',
          700: '#5D6D7E',
          500: '#85929E',
          200: '#D5D8DC',
          50: '#F8F9FA',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
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
