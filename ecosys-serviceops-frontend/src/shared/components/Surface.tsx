import type { ElementType, ReactNode } from 'react'
import { cn } from '../utils/cn'
import { Crystal, type CrystalVariant } from './Crystal'

export interface SurfaceProps {
  variant?: 'glass' | 'solid'
  padding?: 'none' | 'sm' | 'md' | 'lg'
  crystal?: CrystalVariant | false
  as?: ElementType
  className?: string
  children: ReactNode
}

const paddingClasses: Record<NonNullable<SurfaceProps['padding']>, string> = {
  none: '',
  sm: 'p-4',
  md: 'p-4',
  lg: 'p-5',
}

// Shared card/panel surface. `solid` is the conventional-UI default used for tables,
// forms, and dense panels; `glass` is the restrained crystal treatment reserved for
// hero/KPI/section surfaces. `crystal` opts a surface into the decorative facet layer.
export function Surface({ variant = 'solid', padding = 'md', crystal = false, as, className, children }: SurfaceProps) {
  const Tag = as ?? 'div'
  return (
    <Tag
      className={cn(
        'relative overflow-hidden rounded-[var(--radius-card)] border shadow-[var(--shadow-card)]',
        variant === 'glass'
          ? 'border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)]'
          : 'border-[var(--app-border)] bg-white',
        paddingClasses[padding],
        className,
      )}
    >
      {crystal ? <Crystal variant={crystal} className="absolute inset-0" /> : null}
      <div className="relative">{children}</div>
    </Tag>
  )
}
