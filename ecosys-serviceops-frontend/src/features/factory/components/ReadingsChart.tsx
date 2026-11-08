import { useMemo, useRef, useState } from 'react'
import { format } from 'date-fns'
import type { ConditionLevel } from '../types'
import { formatNumber } from './ui'

const WIDTH = 640
const HEIGHT = 200
const PAD = { top: 12, right: 12, bottom: 24, left: 64 }

/** The fields the chart draws; meter readings and condition points both have them. */
export interface ChartReading {
  id?: string
  readAt: string
  value: number
  isOutOfRange?: boolean
  condition?: ConditionLevel
}

interface Limits {
  min: number | null
  max: number | null
  warnMin?: number | null
  warnMax?: number | null
}

/**
 * One meter's readings over time. A single series, so the panel title names it and there is no
 * legend; the readings table beside it is the table view. Alarm limits are grey dashed reference
 * lines and the warning band amber dotted ones; readings past an alarm limit get a red marker and
 * those in the warning band an amber one (the table says so in words too).
 */
export function ReadingsChart({ readings, unit, min, max, warnMin = null, warnMax = null }: { readings: ChartReading[]; unit: string } & Limits) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [hover, setHover] = useState<number | null>(null)
  const points = useMemo(() => [...readings].sort((a, b) => a.readAt.localeCompare(b.readAt)), [readings])

  if (points.length < 2) {
    return <p className="rounded-lg bg-slate-50 px-3 py-6 text-center text-sm text-slate-500">The chart appears once there are two readings.</p>
  }

  const series = points.map((reading) => ({ reading, t: new Date(reading.readAt).getTime() }))
  const first = series[0]!
  const last = series[series.length - 1]!
  const values = series.map((p) => p.reading.value)
  const limits = [min, max, warnMin, warnMax].filter((x): x is number => x !== null)
  const lo = Math.min(...values, ...limits)
  const hi = Math.max(...values, ...limits)
  const span = hi - lo || Math.abs(hi) || 1
  const yMin = lo - span * 0.08
  const yMax = hi + span * 0.08
  const t0 = first.t
  const t1 = last.t === t0 ? t0 + 1 : last.t
  const x = (t: number) => PAD.left + ((t - t0) / (t1 - t0)) * (WIDTH - PAD.left - PAD.right)
  const y = (v: number) => PAD.top + (1 - (v - yMin) / (yMax - yMin)) * (HEIGHT - PAD.top - PAD.bottom)
  const path = series.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(p.t).toFixed(1)},${y(p.reading.value).toFixed(1)}`).join(' ')
  const ticks = niceTicks(yMin, yMax)

  const onMove = (event: React.PointerEvent<SVGSVGElement>) => {
    const box = svgRef.current?.getBoundingClientRect()
    if (!box) return
    const px = ((event.clientX - box.left) / box.width) * WIDTH
    let nearest = 0
    series.forEach((p, i) => {
      if (Math.abs(x(p.t) - px) < Math.abs(x(series[nearest]!.t) - px)) nearest = i
    })
    setHover(nearest)
  }

  const active = hover !== null ? series[hover] : undefined

  return (
    <div className="relative">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="h-auto w-full touch-none select-none"
        role="img"
        aria-label={`Readings from ${format(new Date(t0), 'd MMM yyyy')} to ${format(new Date(last.t), 'd MMM yyyy')}, latest ${formatNumber(last.reading.value, unit)}`}
        onPointerMove={onMove}
        onPointerLeave={() => setHover(null)}
      >
        {ticks.map((tick) => (
          <g key={tick}>
            <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y(tick)} y2={y(tick)} stroke="#e2e8f0" strokeWidth={1} />
            <text x={PAD.left - 6} y={y(tick)} textAnchor="end" dominantBaseline="middle" className="fill-slate-500 text-[10px]">{formatNumber(tick)}</text>
          </g>
        ))}
        {([[min, 'Alarm low', false], [max, 'Alarm high', false], [warnMin, 'Warning low', true], [warnMax, 'Warning high', true]] as const).map(([limit, label, warn]) => limit === null ? null : (
          <g key={label}>
            <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y(limit)} y2={y(limit)} stroke={warn ? '#d97706' : '#94a3b8'} strokeWidth={1} strokeDasharray={warn ? '2 3' : '4 4'} />
            <text x={WIDTH - PAD.right} y={y(limit) - 4} textAnchor="end" className={warn ? 'fill-amber-700 text-[10px]' : 'fill-slate-500 text-[10px]'}>{label}</text>
          </g>
        ))}
        <text x={PAD.left} y={HEIGHT - 6} className="fill-slate-500 text-[10px]">{format(new Date(t0), 'd MMM')}</text>
        <text x={WIDTH - PAD.right} y={HEIGHT - 6} textAnchor="end" className="fill-slate-500 text-[10px]">{format(new Date(last.t), 'd MMM')}</text>
        <path d={path} fill="none" stroke="var(--app-primary)" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        {series.map((p, i) => {
          const level = levelOf(p.reading)
          return level === 'Normal' ? null : (
            <circle key={p.reading.id ?? i} cx={x(p.t)} cy={y(p.reading.value)} r={5} fill={level === 'Alarm' ? '#e11d48' : '#d97706'} stroke="white" strokeWidth={2} />
          )
        })}
        {active ? (
          <g pointerEvents="none">
            <line x1={x(active.t)} x2={x(active.t)} y1={PAD.top} y2={HEIGHT - PAD.bottom} stroke="#94a3b8" strokeWidth={1} />
            <circle cx={x(active.t)} cy={y(active.reading.value)} r={4} fill="var(--app-primary)" stroke="white" strokeWidth={2} />
          </g>
        ) : null}
      </svg>
      {active ? (
        <div
          className="pointer-events-none absolute top-0 z-10 rounded-md border border-slate-200 bg-white px-2 py-1 text-xs shadow"
          style={{ left: `${(x(active.t) / WIDTH) * 100}%`, transform: x(active.t) > WIDTH / 2 ? 'translateX(-105%)' : 'translateX(5%)' }}
        >
          <div className="font-medium text-slate-900">{formatNumber(active.reading.value, unit)}</div>
          <div className="text-slate-500">{format(new Date(active.reading.readAt), 'd MMM yyyy HH:mm')}</div>
          {levelOf(active.reading) === 'Alarm' ? <div className="text-rose-700">Past the alarm limit</div> : levelOf(active.reading) === 'Warning' ? <div className="text-amber-700">In the warning band</div> : null}
        </div>
      ) : null}
    </div>
  )
}

export function levelOf(reading: ChartReading): ConditionLevel {
  return reading.condition ?? (reading.isOutOfRange ? 'Alarm' : 'Normal')
}

/** Three to five round values inside the range (1, 2 or 5 × a power of ten apart). */
export function niceTicks(min: number, max: number): number[] {
  const rough = (max - min) / 5
  const magnitude = 10 ** Math.floor(Math.log10(rough || 1))
  const step = [1, 2, 5, 10].map((m) => m * magnitude).find((candidate) => candidate >= rough) ?? 10 * magnitude
  const ticks: number[] = []
  for (let value = Math.ceil(min / step) * step; value <= max; value += step) ticks.push(Number(value.toPrecision(12)))
  return ticks
}
