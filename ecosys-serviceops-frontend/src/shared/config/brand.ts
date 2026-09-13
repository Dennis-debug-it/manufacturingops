export const brand = {
  appName: 'ECOSYS',
  tagline: 'automate.rely.scale',
  // Public asset paths (served from /public). The rosette + wordmark lockup.
  // `Dark` variants are light-coloured, for use on dark surfaces (e.g. the sidebar); `Mono`
  // variants are flat white, for single-colour placement on any coloured background.
  assets: {
    lockup: '/logo.svg',
    lockupDark: '/logo-dark.svg',
    lockupMono: '/logo-mono.svg',
    mark: '/logo-icon.svg',
    markDark: '/logo-icon-dark.svg',
    markMono: '/logo-icon-mono.svg',
  },
  colors: {
    primary: '#15806A',
    primaryLight: '#2C9B6F',
    accent: '#3EAD73',
    ink: '#12211B',
    success: '#27AE60',
    warning: '#F39C12',
    danger: '#E74C3C',
    gray900: '#12211B',
    gray700: '#5E6B62',
    gray500: '#85929E',
    gray200: '#D5D8DC',
    gray50: '#F6F5EF',
  },
} as const
