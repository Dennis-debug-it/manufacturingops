import { cn } from '../utils/cn'

export type CrystalVariant = 'dashboard' | 'assets' | 'work-orders' | 'command-centre' | 'analytics' | 'kpi'

interface Facet {
  points: string
  fill: 'teal' | 'lime'
  opacity: number
}

interface Line {
  x1: number
  y1: number
  x2: number
  y2: number
}

interface CrystalDef {
  viewBox: string
  blur: number
  facets: Facet[]
  lines?: Line[]
}

// Decorative faceted-polygon backgrounds. Each screen gets its own formation so the
// crystal motif reads as an Ecosys visual identity rather than one repeated asset.
const DEFS: Record<CrystalVariant, CrystalDef> = {
  dashboard: {
    viewBox: '0 0 800 500',
    blur: 18,
    facets: [
      { points: '0,0 320,0 120,500 0,420', fill: 'teal', opacity: 0.05 },
      { points: '820,0 820,300 560,500 380,500', fill: 'teal', opacity: 0.04 },
      { points: '650,0 820,0 820,180', fill: 'lime', opacity: 0.06 },
    ],
    lines: [
      { x1: 320, y1: 0, x2: 120, y2: 500 },
      { x1: 560, y1: 500, x2: 820, y2: 300 },
    ],
  },
  assets: {
    viewBox: '0 0 800 500',
    blur: 10,
    facets: [
      { points: '600,0 800,0 800,120', fill: 'teal', opacity: 0.05 },
      { points: '700,0 800,0 800,60 750,120 650,80', fill: 'lime', opacity: 0.05 },
      { points: '0,400 100,500 0,500', fill: 'teal', opacity: 0.04 },
    ],
    lines: [
      { x1: 600, y1: 0, x2: 800, y2: 120 },
      { x1: 650, y1: 80, x2: 750, y2: 120 },
    ],
  },
  'work-orders': {
    viewBox: '0 0 800 500',
    blur: 14,
    facets: [
      { points: '0,120 220,0 340,0 60,260', fill: 'teal', opacity: 0.045 },
      { points: '800,380 620,500 800,500', fill: 'lime', opacity: 0.05 },
      { points: '500,0 800,0 800,220 640,120', fill: 'teal', opacity: 0.04 },
    ],
    lines: [
      { x1: 220, y1: 0, x2: 60, y2: 260 },
      { x1: 640, y1: 120, x2: 800, y2: 220 },
    ],
  },
  'command-centre': {
    viewBox: '0 0 1000 600',
    blur: 26,
    facets: [
      { points: '0,0 420,0 180,600 0,600', fill: 'teal', opacity: 0.05 },
      { points: '1000,0 1000,420 700,600 460,600 780,180', fill: 'teal', opacity: 0.045 },
      { points: '760,0 1000,0 1000,220', fill: 'lime', opacity: 0.06 },
    ],
    lines: [
      { x1: 420, y1: 0, x2: 180, y2: 600 },
      { x1: 780, y1: 180, x2: 460, y2: 600 },
      { x1: 760, y1: 0, x2: 1000, y2: 220 },
    ],
  },
  analytics: {
    viewBox: '0 0 800 500',
    blur: 12,
    facets: [
      { points: '0,500 120,320 260,420 160,500', fill: 'teal', opacity: 0.045 },
      { points: '340,500 460,260 560,380 480,500', fill: 'lime', opacity: 0.05 },
      { points: '600,500 700,180 800,300 800,500', fill: 'teal', opacity: 0.04 },
    ],
  },
  kpi: {
    viewBox: '0 0 160 120',
    blur: 6,
    facets: [
      { points: '160,0 160,90 90,120 40,60', fill: 'teal', opacity: 0.06 },
      { points: '160,0 160,40 120,10', fill: 'lime', opacity: 0.08 },
    ],
  },
}

const FILL_VAR: Record<Facet['fill'], string> = {
  teal: 'var(--crystal-teal)',
  lime: 'var(--glass-lime)',
}

export interface CrystalProps {
  variant: CrystalVariant
  className?: string
}

// Pure decoration: never affects layout, focus order, or text contrast — always painted
// behind content and hidden from assistive tech.
export function Crystal({ variant, className }: CrystalProps) {
  const def = DEFS[variant]
  const filterId = `crystal-blur-${variant}`
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      className={cn('pointer-events-none h-full w-full', className)}
      viewBox={def.viewBox}
      preserveAspectRatio="xMidYMid slice"
    >
      <defs>
        <filter id={filterId} x="-20%" y="-20%" width="140%" height="140%">
          <feGaussianBlur stdDeviation={def.blur} />
        </filter>
      </defs>
      <g filter={`url(#${filterId})`}>
        {def.facets.map((facet, index) => (
          <polygon key={index} points={facet.points} fill={FILL_VAR[facet.fill]} fillOpacity={facet.opacity} />
        ))}
      </g>
      {def.lines?.map((line, index) => (
        <line
          key={index}
          x1={line.x1}
          y1={line.y1}
          x2={line.x2}
          y2={line.y2}
          stroke="rgba(7,63,67,0.06)"
          strokeWidth={1}
        />
      ))}
    </svg>
  )
}
