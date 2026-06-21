import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'

type Props = {
  value?: string | null
  onChange: (value: string) => void
  height?: number
  dataTestId?: string
}

export function WorkOrderSignaturePad({ value, onChange, height = 160, dataTestId }: Props) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const drawingRef = useRef(false)
  const [hasDrawn, setHasDrawn] = useState(Boolean(value))

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas || !value) return

    const context = canvas.getContext('2d')
    if (!context) return

    const image = new Image()
    image.onload = () => {
      context.clearRect(0, 0, canvas.width, canvas.height)
      context.drawImage(image, 0, 0, canvas.width, canvas.height)
      setHasDrawn(true)
    }
    image.src = value
  }, [value])

  function syncValue() {
    const canvas = canvasRef.current
    if (!canvas) return
    onChange(canvas.toDataURL('image/png'))
  }

  function getPoint(event: ReactPointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current
    if (!canvas) return { x: 0, y: 0 }
    const rect = canvas.getBoundingClientRect()
    return {
      x: ((event.clientX - rect.left) / rect.width) * canvas.width,
      y: ((event.clientY - rect.top) / rect.height) * canvas.height,
    }
  }

  function startDrawing(event: ReactPointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return

    const point = getPoint(event)
    drawingRef.current = true
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.strokeStyle = '#0f172a'
    context.lineWidth = 2.5
    context.beginPath()
    context.moveTo(point.x, point.y)
    setHasDrawn(true)
  }

  function draw(event: ReactPointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current) return
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    const point = getPoint(event)
    context.lineTo(point.x, point.y)
    context.stroke()
  }

  function stopDrawing() {
    if (!drawingRef.current) return
    drawingRef.current = false
    syncValue()
  }

  function clearPad() {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    context.clearRect(0, 0, canvas.width, canvas.height)
    setHasDrawn(false)
    onChange('')
  }

  return (
    <div className="space-y-3">
      <canvas
        ref={canvasRef}
        width={640}
        height={height * 2}
        data-testid={dataTestId}
        className="w-full rounded-2xl border border-slate-300 bg-white shadow-sm touch-none"
        style={{ height }}
        onPointerDown={startDrawing}
        onPointerMove={draw}
        onPointerUp={stopDrawing}
        onPointerLeave={stopDrawing}
      />
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs text-muted">{hasDrawn ? 'Signature captured.' : 'Draw the signature in the box above.'}</p>
        <button type="button" className="button-secondary" onClick={clearPad}>
          Clear signature
        </button>
      </div>
    </div>
  )
}
