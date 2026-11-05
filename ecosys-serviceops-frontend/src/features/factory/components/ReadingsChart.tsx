import { useMemo, useRef, useState } from 'react'
import { format } from 'date-fns'
import type { MeterReading } from '../types'
import { formatNumber } from './ui'

const WIDTH = 640
const HEIGHT = 200
const PAD = { top: 12, right: 12, bottom: 24, left: 64 }

/**
 * One meter's readings over time. A single series, so the panel title names it and there is no
 * legend; the readings table beside it is the table view. Limits are dashed reference lines, and
 * readings outside them get a ring marker (the table flags them in words too).
 */
export function ReadingsChart({ readings, unit, min, max }: { readings: MeterReading[]; unit: string; min: number | null; max: number | null }) {
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
  const lo = Math.min(...values, ...(min !== null ? [min] : []))
  const hi = Math.max(...values, ...(max !== null ? [max] : []))
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
        {[min, max].map((limit, i) => limit === null ? null : (
          <g key={i}>
            <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y(limit)} y2={y(limit)} stroke="#94a3b8" strokeWidth={1} strokeDasharray="4 4" />
            <text x={WIDTH - PAD.right} y={y(limit) - 4} textAnchor="end" className="fill-slate-500 text-[10px]">{i === 0 ? 'Low limit' : 'High limit'}</text>
          </g>
        ))}
        <text x={PAD.left} y={HEIGHT - 6} className="fill-slate-500 text-[10px]">{format(new Date(t0), 'd MMM')}</text>
        <text x={WIDTH - PAD.right} y={HEIGHT - 6} textAnchor="end" className="fill-slate-500 text-[10px]">{format(new Date(last.t), 'd MMM')}</text>
        <path d={path} fill="none" stroke="var(--app-primary)" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        {series.map((p) => p.reading.isOutOfRange ? (
          <circle key={p.reading.id} cx={x(p.t)} cy={y(p.reading.value)} r={5} fill="#e11d48" stroke="white" strokeWidth={2} />
        ) : null)}
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
          {active.reading.isOutOfRange ? <div className="text-rose-700">Outside limits</div> : null}
        </div>
      ) : null}
    </div>
  )
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
