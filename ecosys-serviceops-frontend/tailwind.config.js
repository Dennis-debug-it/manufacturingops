/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        teal: {
          DEFAULT: '#073F43',
          50: '#EEF3F2',
          100: '#DCEAE8',
          600: '#0B6665',
          700: '#0A5654',
          900: '#073F43',
          950: '#052D30',
        },
        lime: {
          DEFAULT: '#79C900',
          50: '#F4FBE8',
          600: '#5FA000',
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
