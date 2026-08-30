import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { ArrowUpRight, Check, Circle, Pencil, RotateCcw, Square, Trash2, Type, X } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'

type Tool = 'draw' | 'arrow' | 'circle' | 'rectangle' | 'text'

type Point = { x: number; y: number }

type Action =
  | { type: 'draw'; color: string; points: Point[] }
  | { type: 'arrow'; color: string; from: Point; to: Point }
  | { type: 'circle'; color: string; from: Point; to: Point }
  | { type: 'rectangle'; color: string; from: Point; to: Point }
  | { type: 'text'; color: string; x: number; y: number; text: string }

const colors = [
  { label: 'Red', value: '#ef4444' },
  { label: 'Yellow', value: '#eab308' },
  { label: 'White', value: '#ffffff' },
  { label: 'Black', value: '#000000' },
]

const tools: Array<{ id: Tool; label: string; icon: typeof Pencil }> = [
  { id: 'draw', label: 'Draw', icon: Pencil },
  { id: 'arrow', label: 'Arrow', icon: ArrowUpRight },
  { id: 'circle', label: 'Circle', icon: Circle },
  { id: 'rectangle', label: 'Rect', icon: Square },
  { id: 'text', label: 'Text', icon: Type },
]

function drawArrowHead(ctx: CanvasRenderingContext2D, from: Point, to: Point, lineWidth: number) {
  const headLength = Math.max(lineWidth * 4, 16)
  const angle = Math.atan2(to.y - from.y, to.x - from.x)
  ctx.beginPath()
  ctx.moveTo(to.x, to.y)
  ctx.lineTo(to.x - headLength * Math.cos(angle - Math.PI / 6), to.y - headLength * Math.sin(angle - Math.PI / 6))
  ctx.lineTo(to.x - headLength * Math.cos(angle + Math.PI / 6), to.y - headLength * Math.sin(angle + Math.PI / 6))
  ctx.closePath()
  ctx.fill()
}

function drawAction(ctx: CanvasRenderingContext2D, action: Action, canvasWidth: number) {
  const lineWidth = Math.max(canvasWidth / 250, 3)
  ctx.strokeStyle = action.color
  ctx.fillStyle = action.color
  ctx.lineWidth = lineWidth
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'

  switch (action.type) {
    case 'draw': {
      const [first, ...rest] = action.points
      if (!first || action.points.length < 2) return
      ctx.beginPath()
      ctx.moveTo(first.x, first.y)
      for (const point of rest) ctx.lineTo(point.x, point.y)
      ctx.stroke()
      return
    }
    case 'rectangle': {
      ctx.strokeRect(action.from.x, action.from.y, action.to.x - action.from.x, action.to.y - action.from.y)
      return
    }
    case 'circle': {
      const rx = Math.abs(action.to.x - action.from.x) / 2
      const ry = Math.abs(action.to.y - action.from.y) / 2
      const cx = (action.from.x + action.to.x) / 2
      const cy = (action.from.y + action.to.y) / 2
      ctx.beginPath()
      ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2)
      ctx.stroke()
      return
    }
    case 'arrow': {
      ctx.beginPath()
      ctx.moveTo(action.from.x, action.from.y)
      ctx.lineTo(action.to.x, action.to.y)
      ctx.stroke()
      drawArrowHead(ctx, action.from, action.to, lineWidth)
      return
    }
    case 'text': {
      const fontSize = Math.max(canvasWidth / 30, 22)
      ctx.font = `600 ${fontSize}px sans-serif`
      ctx.textBaseline = 'top'
      ctx.fillText(action.text, action.x, action.y)
    }
  }
}

function TextInputOverlay({ onCommit, onCancel }: { onCommit: (text: string) => void; onCancel: () => void }) {
  const [value, setValue] = useState('')
  return (
    <div className="absolute inset-x-0 bottom-0 flex items-center gap-2 border-t border-slate-700 bg-[var(--app-primary)] p-3">
      <input
        autoFocus
        value={value}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') onCommit(value)
          if (event.key === 'Escape') onCancel()
        }}
        placeholder="Enter text…"
        className="h-11 flex-1 rounded-lg border border-slate-700 bg-slate-800 px-3 text-sm text-white outline-none focus:border-emerald-400"
      />
      <button type="button" onClick={() => onCommit(value)} className="inline-flex h-11 items-center rounded-lg bg-emerald-500 px-4 text-sm font-semibold text-white hover:bg-emerald-600">
        Add
      </button>
      <button type="button" onClick={onCancel} className="inline-flex h-11 items-center rounded-lg border border-slate-700 px-3 text-sm text-white/80 hover:bg-white/10">
        Cancel
      </button>
    </div>
  )
}

export function PhotoAnnotationCanvas({ file, onCancel, onDone }: { file: File; onCancel: () => void; onDone: (blob: Blob) => void }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const imageRef = useRef<HTMLImageElement | null>(null)
  const drawingRef = useRef<Action | null>(null)
  const [imageLoaded, setImageLoaded] = useState(false)
  const [tool, setTool] = useState<Tool>('draw')
  const [color, setColor] = useState(colors[0]?.value ?? '#ef4444')
  const [actions, setActions] = useState<Action[]>([])
  const [textPoint, setTextPoint] = useState<Point | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const url = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => {
      imageRef.current = img
      const canvas = canvasRef.current
      if (canvas) {
        canvas.width = img.naturalWidth
        canvas.height = img.naturalHeight
      }
      setImageLoaded(true)
    }
    img.src = url
    return () => URL.revokeObjectURL(url)
  }, [file])

  const redraw = () => {
    const canvas = canvasRef.current
    const img = imageRef.current
    if (!canvas || !img) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height)
    for (const action of actions) {
      drawAction(ctx, action, canvas.width)
    }
  }

  useEffect(() => {
    redraw()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actions, imageLoaded])

  const getCanvasPoint = (event: ReactPointerEvent<HTMLCanvasElement>): Point => {
    const canvas = canvasRef.current
    if (!canvas) return { x: 0, y: 0 }
    const rect = canvas.getBoundingClientRect()
    const scaleX = canvas.width / rect.width
    const scaleY = canvas.height / rect.height
    return { x: (event.clientX - rect.left) * scaleX, y: (event.clientY - rect.top) * scaleY }
  }

  const handlePointerDown = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (!imageLoaded) return
    const point = getCanvasPoint(event)

    if (tool === 'text') {
      setTextPoint(point)
      return
    }

    event.currentTarget.setPointerCapture(event.pointerId)
    drawingRef.current = tool === 'draw' ? { type: 'draw', color, points: [point] } : { type: tool, color, from: point, to: point }
  }

  const handlePointerMove = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const action = drawingRef.current
    if (!action) return
    const point = getCanvasPoint(event)

    if (action.type === 'draw') {
      action.points.push(point)
    } else if (action.type !== 'text') {
      action.to = point
    }

    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (canvas && ctx) {
      redraw()
      drawAction(ctx, action, canvas.width)
    }
  }

  const handlePointerUp = () => {
    if (!drawingRef.current) return
    setActions((prev) => [...prev, drawingRef.current as Action])
    drawingRef.current = null
  }

  const commitText = (text: string) => {
    if (textPoint && text.trim()) {
      setActions((prev) => [...prev, { type: 'text', color, x: textPoint.x, y: textPoint.y, text: text.trim() }])
    }
    setTextPoint(null)
  }

  const undo = () => setActions((prev) => prev.slice(0, -1))
  const clear = () => setActions([])

  const handleDone = () => {
    const canvas = canvasRef.current
    if (!canvas) return
    setSaving(true)
    canvas.toBlob((blob) => {
      setSaving(false)
      if (blob) onDone(blob)
    }, 'image/png')
  }

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-slate-950">
      <div className="flex items-center justify-between border-b border-slate-800 px-4 py-3">
        <button type="button" onClick={onCancel} className="inline-flex h-10 items-center gap-1.5 rounded-lg px-3 text-sm font-medium text-white/80 hover:bg-white/10">
          <X className="h-4 w-4" /> Cancel
        </button>
        <p className="text-sm font-medium text-white">Annotate Photo</p>
        <button
          type="button"
          onClick={handleDone}
          disabled={!imageLoaded || saving}
          className="inline-flex h-10 items-center gap-1.5 rounded-lg bg-emerald-500 px-4 text-sm font-semibold text-white hover:bg-emerald-600 disabled:opacity-60"
        >
          <Check className="h-4 w-4" /> {saving ? 'Saving…' : 'Done'}
        </button>
      </div>

      <div className="relative flex flex-1 items-center justify-center overflow-hidden bg-black">
        {!imageLoaded ? <p className="text-sm text-white/70">Loading photo…</p> : null}
        <canvas
          ref={canvasRef}
          className="touch-none"
          style={{ maxWidth: '100%', maxHeight: '100%', display: imageLoaded ? 'block' : 'none' }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerLeave={handlePointerUp}
        />
        {textPoint ? <TextInputOverlay onCommit={commitText} onCancel={() => setTextPoint(null)} /> : null}
      </div>

      <div className="border-t border-slate-800 bg-[var(--app-primary)] px-3 py-3">
        <div className="mb-3 flex items-center justify-center gap-2">
          {colors.map((c) => (
            <button
              key={c.value}
              type="button"
              onClick={() => setColor(c.value)}
              aria-label={c.label}
              className={cn('h-8 w-8 rounded-full border-2', color === c.value ? 'border-emerald-400' : 'border-white/30')}
              style={{ backgroundColor: c.value }}
            />
          ))}
        </div>
        <div className="flex items-center justify-center gap-1 overflow-x-auto">
          {tools.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              type="button"
              onClick={() => setTool(id)}
              className={cn(
                'flex h-14 w-16 shrink-0 flex-col items-center justify-center gap-1 rounded-lg text-xs font-medium transition',
                tool === id ? 'bg-emerald-500 text-white' : 'text-white/70 hover:bg-white/10',
              )}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
          <button
            type="button"
            onClick={undo}
            disabled={actions.length === 0}
            className="flex h-14 w-16 shrink-0 flex-col items-center justify-center gap-1 rounded-lg text-xs font-medium text-white/70 transition hover:bg-white/10 disabled:opacity-40"
          >
            <RotateCcw className="h-4 w-4" />
            Undo
          </button>
          <button
            type="button"
            onClick={clear}
            disabled={actions.length === 0}
            className="flex h-14 w-16 shrink-0 flex-col items-center justify-center gap-1 rounded-lg text-xs font-medium text-white/70 transition hover:bg-white/10 disabled:opacity-40"
          >
            <Trash2 className="h-4 w-4" />
            Clear
          </button>
        </div>
      </div>
    </div>
  )
}
