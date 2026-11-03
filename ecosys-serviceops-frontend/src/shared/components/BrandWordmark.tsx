import { brand } from '../config/brand'
import { Logo } from './Logo'
import { cn } from '../utils/cn'

export interface BrandWordmarkProps {
  tone?: 'light' | 'dark'
  markClassName?: string
  className?: string
}

// The lockup SVG bakes the tagline at a fixed proportion of the wordmark, so at typical UI
// logo sizes (~36–48px tall) it scales down to a few px and becomes illegible. This renders
// the rosette mark plus real HTML text instead, so the tagline stays legible at any icon size.
export function BrandWordmark({ tone = 'light', markClassName, className }: BrandWordmarkProps) {
  const isDark = tone === 'dark'
  return (
    <div className={cn('flex items-center gap-3', className)}>
      <Logo variant="mark" tone={tone} className={markClassName ?? 'h-10 w-10'} />
      <div>
        <div className={cn('text-lg font-semibold tracking-tight', isDark ? 'text-white' : 'text-slate-900')}>
          Ecosys<span className="text-[var(--app-lime)]">.</span>
        </div>
        <div className={cn('text-[11px] uppercase tracking-[0.2em]', isDark ? 'text-white/50' : 'text-slate-500')}>
          {brand.tagline}
        </div>
      </div>
    </div>
  )
}
