import favicon from '../assets/brand/ecosys-favicon.svg'
import logoIcon from '../assets/brand/ecosys-icon.svg'
import logoHorizontalDark from '../assets/brand/ecosys-logo-horizontal-dark.svg'
import logoHorizontalLight from '../assets/brand/ecosys-logo-horizontal-light.svg'

export const colors = {
  darkTeal: '#0C2F33',
  teal: '#127A78',
  green: '#11A67A',
  lime: '#B7E26D',
  darkUi: '#1A1F23',
  lightBackground: '#F7F8F6',
} as const

export const appName = 'Ecosys'
export const tagline = 'Connect - Automate - Scale - Rely'

const logoHorizontal = logoHorizontalDark

export {
  favicon,
  logoHorizontal,
  logoHorizontalDark,
  logoHorizontalLight,
  logoIcon,
}

export const brand = {
  appName,
  tagline,
  logoHorizontal,
  logoHorizontalLight,
  logoHorizontalDark,
  logoIcon,
  favicon,
  colors,
} as const
