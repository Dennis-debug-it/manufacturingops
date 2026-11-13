import { useEffect, useRef, useState } from 'react'

export interface Position { latitude: number; longitude: number; accuracyM: number; at: number }

/**
 * The phone's position while a patrol is in progress (never in the background or between patrols, as
 * the guards are told). Returns the latest fix, or null when location is off or not yet known.
 */
export function useGeoWatch(active: boolean): { position: Position | null; error: string | null } {
  const [position, setPosition] = useState<Position | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (!active || !('geolocation' in navigator)) return
    const id = navigator.geolocation.watchPosition(
      (fix) => { setPosition({ latitude: fix.coords.latitude, longitude: fix.coords.longitude, accuracyM: fix.coords.accuracy, at: fix.timestamp }); setError(null) },
      (failure) => setError(failure.code === failure.PERMISSION_DENIED ? 'Location is off for this app.' : 'No GPS fix yet.'),
      { enableHighAccuracy: true, maximumAge: 15_000, timeout: 20_000 },
    )
    return () => navigator.geolocation.clearWatch(id)
  }, [active])
  return { position: active ? position : null, error: active ? error : null }
}

/** A position only if it is recent enough to stand for "where the guard is now". */
export function freshPosition(position: Position | null, maxAgeMs = 60_000): Position | null {
  return position && Date.now() - position.at <= maxAgeMs ? position : null
}

interface NdefReadingEvent extends Event { serialNumber: string }
interface NdefReaderLike extends EventTarget { scan(options?: { signal?: AbortSignal }): Promise<void> }
type NdefReaderConstructor = new () => NdefReaderLike

/** Web NFC is in Chrome on Android only; elsewhere the guard scans the QR label. */
export function nfcSupported(): boolean {
  return typeof window !== 'undefined' && 'NDEFReader' in window
}

/**
 * Listens for NFC tags while `active`, calling `onSerial` with each tag's serial number. Reports why it
 * cannot listen (unsupported browser, permission refused) through `onError`.
 */
export function useNfcReader(active: boolean, onSerial: (serial: string) => void, onError: (reason: string) => void) {
  const handlers = useRef({ onSerial, onError })
  useEffect(() => { handlers.current = { onSerial, onError } })
  useEffect(() => {
    if (!active) return
    if (!nfcSupported()) { handlers.current.onError('This phone or browser cannot read NFC tags. Scan the QR label instead.'); return }
    const Reader = (window as unknown as { NDEFReader: NdefReaderConstructor }).NDEFReader
    const reader = new Reader()
    const abort = new AbortController()
    let last = ''
    let lastAt = 0
    const onReading = (event: Event) => {
      const serial = (event as NdefReadingEvent).serialNumber
      // The same tap fires more than once; ignore repeats within a few seconds.
      if (!serial || (serial === last && Date.now() - lastAt < 4000)) return
      last = serial
      lastAt = Date.now()
      navigator.vibrate?.(80)
      handlers.current.onSerial(serial)
    }
    reader.addEventListener('reading', onReading)
    reader.scan({ signal: abort.signal }).catch((error: unknown) => {
      if (!abort.signal.aborted) handlers.current.onError(error instanceof Error && error.name === 'NotAllowedError' ? 'NFC permission was refused.' : 'NFC could not start; is it switched on?')
    })
    return () => { abort.abort(); reader.removeEventListener('reading', onReading) }
  }, [active])
}
