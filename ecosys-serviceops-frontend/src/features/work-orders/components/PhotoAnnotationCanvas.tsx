import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { ArrowUpRight, Check, Circle, Move, Pencil, RotateCcw, Square, Trash2, Type, X } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'

type Tool = 'move' | 'draw' | 'arrow' | 'circle' | 'rectangle' | 'text'

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
  { id: 'move', label: 'Move', icon: Move },
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
      const fontSize = textFontSize(canvasWidth)
      ctx.font = `600 ${fontSize}px sans-serif`
      ctx.textBaseline = 'top'
      ctx.fillText(action.text, action.x, action.y)
    }
  }
}

const textFontSize = (canvasWidth: number) => Math.max(canvasWidth / 30, 22)

/** Axis-aligned box around an annotation, in canvas pixels — what a tap/drag in Move mode hits. */
function boundsOf(ctx: CanvasRenderingContext2D, action: Action, canvasWidth: number) {
  if (action.type === 'text') {
    const fontSize = textFontSize(canvasWidth)
    ctx.font = `600 ${fontSize}px sans-serif`
    return { left: action.x, top: action.y, right: action.x + ctx.measureText(action.text).width, bottom: action.y + fontSize * 1.2 }
  }
  const points = action.type === 'draw' ? action.points : [action.from, action.to]
  const xs = points.map((point) => point.x)
  const ys = points.map((point) => point.y)
  return { left: Math.min(...xs), top: Math.min(...ys), right: Math.max(...xs), bottom: Math.max(...ys) }
}

function translate(action: Action, dx: number, dy: number): Action {
  const shift = (point: Point) => ({ x: point.x + dx, y: point.y + dy })
  switch (action.type) {
    case 'draw':
      return { ...action, points: action.points.map(shift) }
    case 'text':
      return { ...action, x: action.x + dx, y: action.y + dy }
    default:
      return { ...action, from: shift(action.from), to: shift(action.to) }
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

/** Uploads are capped at 5 MB; stay comfortably under it. */
const MAX_UPLOAD_BYTES = 4.5 * 1024 * 1024

const toBlob = (canvas: HTMLCanvasElement, quality: number) =>
  new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality))

/**
 * The annotated photo as a JPEG small enough to upload. A full-resolution PNG of a phone photo
 * is often 10–20 MB, which the server rejects — so the annotation never reached the report.
 */
async function exportForUpload(source: HTMLCanvasElement): Promise<Blob | null> {
  let last: Blob | null = null
  for (const longEdge of [2560, 1920, 1280]) {
    const scale = Math.min(1, longEdge / Math.max(source.width, source.height))
    const target = document.createElement('canvas')
    target.width = Math.max(1, Math.round(source.width * scale))
    target.height = Math.max(1, Math.round(source.height * scale))
    const ctx = target.getContext('2d')
    if (!ctx) return null
    // JPEG has no transparency: start from white.
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, target.width, target.height)
    ctx.drawImage(source, 0, 0, target.width, target.height)
    for (const quality of [0.88, 0.75]) {
      last = await toBlob(target, quality)
      if (last && last.size <= MAX_UPLOAD_BYTES) return last
    }
  }
  return last
}

export function PhotoAnnotationCanvas({ file, onCancel, onDone }: { file: File; onCancel: () => void; onDone: (blob: Blob) => void }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const imageRef = useRef<HTMLImageElement | null>(null)
  const drawingRef = useRef<Action | null>(null)
  /** Move tool: which annotation is being dragged, where the drag started, and its original position. */
  const draggingRef = useRef<{ index: number; start: Point; original: Action; before: Action[] } | null>(null)
  const [imageLoaded, setImageLoaded] = useState(false)
  const [tool, setTool] = useState<Tool>('draw')
  const [color, setColor] = useState(colors[0]?.value ?? '#ef4444')
  const [actions, setActions] = useState<Action[]>([])
  // Snapshots for Undo, so moving or clearing can be undone as well as adding.
  const [history, setHistory] = useState<Action[][]>([])
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

  const commit = (next: Action[], before: Action[] = actions) => {
    setHistory((prev) => [...prev, before])
    setActions(next)
  }

  /** Topmost annotation under the point, with a finger-sized margin scaled to the displayed canvas. */
  const hitTest = (point: Point): number => {
    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (!canvas || !ctx) return -1
    const margin = 16 * (canvas.width / canvas.getBoundingClientRect().width)
    for (let index = actions.length - 1; index >= 0; index -= 1) {
      const box = boundsOf(ctx, actions[index]!, canvas.width)
      if (point.x >= box.left - margin && point.x <= box.right + margin && point.y >= box.top - margin && point.y <= box.bottom + margin) return index
    }
    return -1
  }

  const handlePointerDown = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (!imageLoaded) return
    const point = getCanvasPoint(event)

    if (tool === 'move') {
      const index = hitTest(point)
      if (index < 0) return
      event.currentTarget.setPointerCapture(event.pointerId)
      draggingRef.current = { index, start: point, original: actions[index]!, before: actions }
      return
    }

    if (tool === 'text') {
      setTextPoint(point)
      return
    }

    event.currentTarget.setPointerCapture(event.pointerId)
    drawingRef.current = tool === 'draw' ? { type: 'draw', color, points: [point] } : { type: tool, color, from: point, to: point }
  }

  const handlePointerMove = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const drag = draggingRef.current
    if (drag) {
      const point = getCanvasPoint(event)
      const moved = translate(drag.original, point.x - drag.start.x, point.y - drag.start.y)
      setActions((prev) => prev.map((item, index) => (index === drag.index ? moved : item)))
      return
    }

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
    const drag = draggingRef.current
    if (drag) {
      draggingRef.current = null
      // Only a real move is worth an Undo step.
      if (actions[drag.index] !== drag.original) setHistory((prev) => [...prev, drag.before])
      return
    }
    if (!drawingRef.current) return
    commit([...actions, drawingRef.current])
    drawingRef.current = null
  }

  const commitText = (text: string) => {
    if (textPoint && text.trim()) {
      commit([...actions, { type: 'text', color, x: textPoint.x, y: textPoint.y, text: text.trim() }])
    }
    setTextPoint(null)
  }

  const undo = () => {
    const previous = history[history.length - 1]
    if (!previous) return
    setHistory((prev) => prev.slice(0, -1))
    setActions(previous)
  }
  const clear = () => commit([])

  const handleDone = async () => {
    const canvas = canvasRef.current
    if (!canvas) return
    setSaving(true)
    try {
      const blob = await exportForUpload(canvas)
      if (blob) onDone(blob)
    } finally {
      setSaving(false)
    }
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
          onClick={() => void handleDone()}
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
          className={cn('touch-none', tool === 'move' && 'cursor-move')}
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
            disabled={history.length === 0}
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
