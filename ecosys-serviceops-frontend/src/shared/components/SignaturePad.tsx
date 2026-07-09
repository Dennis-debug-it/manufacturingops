import { useEffect, useRef, useState, type PointerEvent } from 'react'

export interface SignaturePadProps {
  onChange: (dataUrl: string | null) => void
  className?: string
}

export function SignaturePad({ onChange, className }: SignaturePadProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const drawingRef = useRef(false)
  const [hasStroke, setHasStroke] = useState(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const context = canvas.getContext('2d')
    if (!context) return
    context.lineWidth = 2.5
    context.lineCap = 'round'
    context.strokeStyle = '#0f172a'
  }, [])

  const getPoint = (event: PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current
    if (!canvas) return { x: 0, y: 0 }
    const rect = canvas.getBoundingClientRect()
    return { x: event.clientX - rect.left, y: event.clientY - rect.top }
  }

  const start = (event: PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    drawingRef.current = true
    const point = getPoint(event)
    context.beginPath()
    context.moveTo(point.x, point.y)
  }

  const move = (event: PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current) return
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    const point = getPoint(event)
    context.lineTo(point.x, point.y)
    context.stroke()
    setHasStroke(true)
  }

  const end = () => {
    drawingRef.current = false
    const canvas = canvasRef.current
    if (canvas && hasStroke) {
      onChange(canvas.toDataURL('image/png'))
    }
  }

  const clear = () => {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!canvas || !context) return
    context.clearRect(0, 0, canvas.width, canvas.height)
    setHasStroke(false)
    onChange(null)
  }

  return (
    <div className={className}>
      <canvas
        ref={canvasRef}
        width={640}
        height={220}
        onPointerDown={start}
        onPointerMove={move}
        onPointerUp={end}
        onPointerLeave={end}
        className="w-full touch-none rounded-lg border border-slate-300 bg-white"
      />
      <button type="button" onClick={clear} className="mt-2 text-xs font-medium text-slate-500 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
        Clear signature
      </button>
    </div>
  )
}
