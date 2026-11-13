import { useState } from 'react'
import toast from 'react-hot-toast'
import { Nfc, QrCode } from 'lucide-react'
import { cn } from '../../shared/utils/cn'
import { inputClass, secondaryButton } from '../factory/components/ui'
import { CameraScanner } from '../factory/components/CameraScanner'
import { nfcSupported, useNfcReader } from '../security/device'

/**
 * An access card: type its printed number, scan its QR with the camera, or tap its chip on the phone
 * (Chrome on Android). The value is whatever identifies it; the server works out which card it is.
 */
export function CardInput({ value, onChange, label = 'Access card' }: { value: string; onChange: (value: string) => void; label?: string }) {
  const [mode, setMode] = useState<'qr' | 'nfc' | null>(null)
  const [cameraOff, setCameraOff] = useState<string | null>(null)
  useNfcReader(mode === 'nfc', (serial) => { onChange(serial); setMode(null) }, (reason) => { toast.error(reason); setMode(null) })

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        <input className={cn(inputClass, 'font-mono uppercase')} aria-label={label} placeholder="Card number, e.g. V-012" value={value}
          onChange={(event) => onChange(event.target.value)} />
        <button type="button" className={secondaryButton} aria-label="Scan the card's QR" onClick={() => setMode(mode === 'qr' ? null : 'qr')}><QrCode className="h-4 w-4" /></button>
        {nfcSupported() ? (
          <button type="button" className={secondaryButton} aria-label="Tap the card" onClick={() => setMode(mode === 'nfc' ? null : 'nfc')}><Nfc className="h-4 w-4" /></button>
        ) : null}
      </div>
      {mode === 'qr' ? (cameraOff ? <p className="text-xs text-slate-500">{cameraOff}</p>
        : <CameraScanner onCode={(code) => { onChange(code); setMode(null) }} onUnavailable={setCameraOff} />) : null}
      {mode === 'nfc' ? <p className="text-xs text-slate-600">Hold the card against the back of the phone…</p> : null}
      {value.toUpperCase().startsWith('CARD:') ? <p className="text-xs text-emerald-700">Card scanned.</p> : null}
    </div>
  )
}
