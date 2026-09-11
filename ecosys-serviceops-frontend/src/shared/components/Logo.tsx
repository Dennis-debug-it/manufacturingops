import { brand } from '../config/brand'

type LogoVariant = 'lockup' | 'mark'
type LogoTone = 'light' | 'dark' | 'mono'

export interface LogoProps {
  /** `lockup` shows the rosette + wordmark; `mark` shows the rosette only. */
  variant?: LogoVariant
  /** `light` for light surfaces, `dark` for dark surfaces, `mono` for single-colour. */
  tone?: LogoTone
  className?: string
}

function src(variant: LogoVariant, tone: LogoTone): string {
  if (variant === 'mark') {
    return tone === 'mono' ? brand.assets.markMono : brand.assets.mark
  }
  if (tone === 'dark') return brand.assets.lockupDark
  if (tone === 'mono') return brand.assets.lockupMono
  return brand.assets.lockup
}

/** Ecosys brand logo. Defaults to the supplied full-colour lockup. */
export function Logo({ variant = 'lockup', tone = 'light', className }: LogoProps) {
  return <img src={src(variant, tone)} alt={brand.appName} className={className} draggable={false} />
}
