import { memo } from 'react'
import { appName, brand } from '../../config/brand'

export type EcosysLogoVariant = 'light' | 'dark' | 'icon' | 'darkPanel' | 'lightPanel'
export type EcosysLogoSize = 'sm' | 'md' | 'lg'

type EcosysLogoProps = {
  variant: EcosysLogoVariant
  size: EcosysLogoSize
  className?: string
  subtitle?: string
  title?: string
  subtitleClassName?: string
  imageClassName?: string
}

const sizeMap: Record<EcosysLogoSize, { icon: number; width: number; badge: string; wordmark: string; subtitle: string }> = {
  sm: { icon: 20, width: 132, badge: 'rounded-lg p-2', wordmark: 'text-[1rem] tracking-[0.26em]', subtitle: 'text-[0.62rem] tracking-[0.18em]' },
  md: { icon: 28, width: 176, badge: 'rounded-xl p-2.5', wordmark: 'text-[1.45rem] tracking-[0.28em]', subtitle: 'text-[0.72rem] tracking-[0.2em]' },
  lg: { icon: 38, width: 224, badge: 'rounded-2xl p-3', wordmark: 'text-[1.9rem] tracking-[0.3em]', subtitle: 'text-[0.82rem] tracking-[0.22em]' },
}

function getWordmarkColors(variant: Exclude<EcosysLogoVariant, 'icon' | 'darkPanel' | 'lightPanel'>) {
  if (variant === 'light') {
    return {
      wordmark: '#F7F8F6',
      tagline: '#F2F7F4',
      subtitleClassName: 'text-[#F2F7F4]',
    }
  }

  return {
    wordmark: '#0C2F33',
    tagline: '#214A4D',
    subtitleClassName: 'text-[#214A4D]',
  }
}

function EcosysIconSvg({ size, title, className = '' }: { size: number; title: string; className?: string }) {
  return (
    <svg
      viewBox="0 0 132 132"
      role="img"
      aria-label={title}
      className={`h-auto w-auto shrink-0 ${className}`.trim()}
      height={size}
      width={size}
      preserveAspectRatio="xMidYMid meet"
    >
      <title>{title}</title>
      <g fill="none" shapeRendering="geometricPrecision">
        <path d="M44 12H92L102 28H54Z" fill={brand.colors.darkTeal} />
        <path d="M18 48L34 22H48L32 48Z" fill={brand.colors.teal} />
        <path d="M30 54H74L84 70H40Z" fill={brand.colors.teal} />
        <path d="M18 76H32L48 102H34Z" fill={brand.colors.teal} />
        <path d="M54 96H102L92 112H44Z" fill={brand.colors.darkTeal} />
        <path d="M80 42H102L114 62L102 82H80L68 62Z" fill={brand.colors.lime} />
        <path d="M91 42H102L114 62H91Z" fill={brand.colors.green} />
        <path d="M68 62H91L80 82Z" fill={brand.colors.green} />
        <path d="M91 62H114L102 82H91Z" fill={brand.colors.green} />
      </g>
    </svg>
  )
}

function EcosysWordmarkSvg({
  variant,
  width,
  title,
  className = '',
}: {
  variant: Exclude<EcosysLogoVariant, 'icon' | 'darkPanel' | 'lightPanel'>
  width: number
  title: string
  className?: string
}) {
  const colors = getWordmarkColors(variant)

  return (
    <svg
      viewBox="0 0 840 180"
      role="img"
      aria-label={title}
      className={`h-auto max-w-full shrink-0 ${className}`.trim()}
      style={{ width }}
      preserveAspectRatio="xMidYMid meet"
      data-ecosys-variant={variant}
    >
      <title>{title}</title>
      <g transform="translate(8 16)" fill="none" shapeRendering="geometricPrecision">
        <path d="M44 12H92L102 28H54Z" fill={brand.colors.darkTeal} />
        <path d="M18 48L34 22H48L32 48Z" fill={brand.colors.teal} />
        <path d="M30 54H74L84 70H40Z" fill={brand.colors.teal} />
        <path d="M18 76H32L48 102H34Z" fill={brand.colors.teal} />
        <path d="M54 96H102L92 112H44Z" fill={brand.colors.darkTeal} />
        <path d="M80 42H102L114 62L102 82H80L68 62Z" fill={brand.colors.lime} />
        <path d="M91 42H102L114 62H91Z" fill={brand.colors.green} />
        <path d="M68 62H91L80 82Z" fill={brand.colors.green} />
        <path d="M91 62H114L102 82H91Z" fill={brand.colors.green} />
      </g>
      <text x="178" y="92" fill={colors.wordmark} fontFamily="Montserrat, Avenir Next, Segoe UI, Arial, sans-serif" fontSize="78" fontWeight="600" letterSpacing="10">
        ECOSYS
      </text>
      <text x="182" y="136" fill={colors.tagline} fontFamily="Montserrat, Avenir Next, Segoe UI, Arial, sans-serif" fontSize="17" fontWeight="600" letterSpacing="7.5">
        CONNECT
      </text>
      <circle cx="315" cy="129" r="3.8" fill={brand.colors.lime} />
      <text x="338" y="136" fill={colors.tagline} fontFamily="Montserrat, Avenir Next, Segoe UI, Arial, sans-serif" fontSize="17" fontWeight="600" letterSpacing="7.5">
        AUTOMATE
      </text>
      <circle cx="517" cy="129" r="3.8" fill={brand.colors.lime} />
      <text x="540" y="136" fill={colors.tagline} fontFamily="Montserrat, Avenir Next, Segoe UI, Arial, sans-serif" fontSize="17" fontWeight="600" letterSpacing="7.5">
        SCALE
      </text>
      <circle cx="661" cy="129" r="3.8" fill={brand.colors.lime} />
      <text x="684" y="136" fill={colors.tagline} fontFamily="Montserrat, Avenir Next, Segoe UI, Arial, sans-serif" fontSize="17" fontWeight="600" letterSpacing="7.5">
        RELY
      </text>
    </svg>
  )
}

function CompactBrandBlock({
  size,
  title,
  className = '',
  variant,
  subtitle,
}: {
  size: EcosysLogoSize
  title: string
  className?: string
  variant: 'darkPanel' | 'lightPanel'
  subtitle?: string
}) {
  const dimensions = sizeMap[size]
  const isDarkPanel = variant === 'darkPanel'
  const wordmarkColor = isDarkPanel ? 'text-[#F7F8F6]' : 'text-[#0C2F33]'
  const subtitleColor = isDarkPanel ? 'text-[#F7F8F6]' : 'text-[#214A4D]'

  return (
    <div className={`flex min-w-0 items-center gap-4 ${className}`.trim()}>
      <div className={`flex shrink-0 items-center justify-center bg-[#F7F8F6] shadow-sm ring-1 ring-[rgba(12,47,51,0.08)] ${dimensions.badge}`}>
        <EcosysIconSvg size={dimensions.icon} title={title} />
      </div>
      <div className="min-w-0">
        <div className={`font-heading font-semibold uppercase leading-none ${dimensions.wordmark} ${wordmarkColor}`.trim()}>
          ECOSYS
        </div>
        <div className={`mt-2 font-semibold uppercase leading-none ${dimensions.subtitle} ${subtitleColor}`.trim()}>
          {subtitle || 'ServiceOps Suite'}
        </div>
      </div>
    </div>
  )
}

export const BrandLogo = memo(function BrandLogo({
  variant,
  size,
  className = '',
  subtitle = '',
  title = appName,
  subtitleClassName = '',
  imageClassName = '',
}: EcosysLogoProps) {
  const dimensions = sizeMap[size]

  if (variant === 'icon') {
    return <EcosysIconSvg size={dimensions.icon} title={title} className={className} />
  }

  if (variant === 'darkPanel' || variant === 'lightPanel') {
    return <CompactBrandBlock size={size} title={title} className={className} variant={variant} subtitle={subtitle} />
  }

  const colors = getWordmarkColors(variant)

  return (
    <div className={`flex min-w-0 flex-col items-start gap-2 ${className}`.trim()}>
      <EcosysWordmarkSvg variant={variant} width={dimensions.width} title={title} className={imageClassName} />
      {subtitle ? (
        <div
          className={`truncate text-[0.64rem] font-semibold uppercase tracking-[0.24em] sm:text-[0.68rem] ${colors.subtitleClassName} ${subtitleClassName}`.trim()}
        >
          {subtitle}
        </div>
      ) : null}
    </div>
  )
})

export const EcosysLogo = BrandLogo
