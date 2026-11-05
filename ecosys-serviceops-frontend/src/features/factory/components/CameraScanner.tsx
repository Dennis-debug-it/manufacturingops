import { useEffect, useRef, useState } from 'react'
import jsQR from 'jsqr'

interface DetectedBarcode { rawValue: string }
interface BarcodeDetectorLike { detect(source: CanvasImageSource): Promise<DetectedBarcode[]> }
type BarcodeDetectorConstructor = new (options?: { formats?: string[] }) => BarcodeDetectorLike

/**
 * Reads QR codes (and, where the browser supports it, common barcodes) from the back camera.
 * Uses the browser's BarcodeDetector when present and falls back to jsQR on older Android
 * browsers. Calls `onCode` once per distinct code.
 */
export function CameraScanner({ onCode, onUnavailable }: { onCode: (code: string) => void; onUnavailable: (reason: string) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const lastCode = useRef<string | null>(null)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    let stream: MediaStream | null = null
    let frame = 0
    let stopped = false
    const Detector = (window as unknown as { BarcodeDetector?: BarcodeDetectorConstructor }).BarcodeDetector
    const detector = Detector ? new Detector({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13', 'data_matrix'] }) : null

    const emit = (code: string | undefined) => {
      if (!code || code === lastCode.current) return
      lastCode.current = code
      navigator.vibrate?.(60)
      onCode(code)
    }

    const tick = async () => {
      const video = videoRef.current
      if (stopped || !video) return
      if (video.readyState >= 2) {
        try {
          if (detector) {
            const codes = await detector.detect(video)
            emit(codes[0]?.rawValue)
          } else {
            const canvas = canvasRef.current!
            const width = Math.min(video.videoWidth, 640)
            const height = Math.round((video.videoHeight / video.videoWidth) * width)
            canvas.width = width
            canvas.height = height
            const context = canvas.getContext('2d', { willReadFrequently: true })!
            context.drawImage(video, 0, 0, width, height)
            const image = context.getImageData(0, 0, width, height)
            emit(jsQR(image.data, width, height, { inversionAttempts: 'dontInvert' })?.data)
          }
        } catch {
          // A frame that can't be read is skipped.
        }
      }
      frame = window.setTimeout(() => void tick(), 200)
    }

    const start = async () => {
      if (!navigator.mediaDevices?.getUserMedia) {
        onUnavailable('This browser cannot use the camera. Type the tag number instead.')
        return
      }
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false })
        if (stopped) return
        const video = videoRef.current!
        video.srcObject = stream
        await video.play()
        setReady(true)
        void tick()
      } catch {
        onUnavailable('The camera could not be opened. Allow camera access, or type the tag number instead.')
      }
    }

    void start()
    return () => {
      stopped = true
      window.clearTimeout(frame)
      stream?.getTracks().forEach((track) => track.stop())
    }
  }, [onCode, onUnavailable])

  return (
    <div className="relative overflow-hidden rounded-xl bg-slate-900">
      <video ref={videoRef} className="aspect-square w-full object-cover sm:aspect-video" muted playsInline />
      <canvas ref={canvasRef} className="hidden" />
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
        <div className="h-3/5 w-3/5 max-w-xs rounded-2xl border-4 border-white/80" />
      </div>
      {!ready ? <p className="absolute inset-x-0 bottom-3 text-center text-sm text-white">Starting camera…</p> : null}
    </div>
  )
}
