import { memo } from 'react'
import { appName } from '../../config/brand'
import { EcosysLogo } from './EcosysLogo'

type BrandTone = 'auto' | 'light' | 'dark'

type PoweredByEcosysProps = {
  className?: string
  tone?: BrandTone
  minimal?: boolean
}

function resolveTone(tone: BrandTone): Exclude<BrandTone, 'auto'> {
  if (tone !== 'auto') {
    return tone
  }

  if (typeof document === 'undefined') {
    return 'dark'
  }

  return document.documentElement.dataset.theme === 'light' ? 'light' : 'dark'
}

export const EcosysIcon = memo(function EcosysIcon({
  className = '',
  size = 40,
  title = appName,
}: {
  className?: string
  size?: number
  title?: string
}) {
  return <EcosysLogo variant="icon" size={size <= 24 ? 'sm' : size <= 32 ? 'md' : 'lg'} className={className} title={title} />
})

export const PoweredByEcosys = memo(function PoweredByEcosys({
  className = '',
  tone = 'auto',
  minimal = false,
}: PoweredByEcosysProps) {
  const nextTone = resolveTone(tone)

  if (minimal) {
    return (
      <div
        className={`inline-flex items-center gap-2 text-[0.68rem] font-semibold uppercase tracking-[0.2em] ${className}`.trim()}
        style={{ color: nextTone === 'light' ? '#214A4D' : '#F2F7F4' }}
      >
        <EcosysLogo variant="icon" size="sm" className="shrink-0" />
        <span>Powered by {appName}</span>
      </div>
    )
  }

  return (
    <div
      className={`inline-flex items-center gap-2 rounded-full px-3 py-2 text-xs font-semibold uppercase tracking-[0.18em] ${className}`.trim()}
      style={{
        background: nextTone === 'light' ? '#F7F8F6' : 'rgba(247, 248, 246, 0.08)',
        color: nextTone === 'light' ? '#214A4D' : '#F2F7F4',
        border: `1px solid ${nextTone === 'light' ? 'rgba(12, 47, 51, 0.14)' : 'rgba(247, 248, 246, 0.18)'}`,
      }}
    >
      <EcosysLogo variant="icon" size="sm" className="shrink-0" />
      <span>Powered by {appName}</span>
    </div>
  )
})
