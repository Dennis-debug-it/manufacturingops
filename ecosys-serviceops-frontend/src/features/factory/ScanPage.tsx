import { useCallback, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, Camera, ClipboardList, Gauge, MapPin, ScanLine, WifiOff } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { useOfflineScanIndex, useScan } from './api'
import { useFactoryLinks } from './links'
import { findOffline, normalizeScannedCode } from './scan'
import { CameraScanner } from './components/CameraScanner'
import { CriticalityBadge, OperationalBadge, formatNumber, inputClass, primaryButton, secondaryButton } from './components/ui'

/**
 * Scan a tag (or type its number) to open the asset. Works with no signal from the copy of the
 * register kept on the phone; online it also shows open work orders and recent history.
 */
export function ScanPage() {
  const { code: routeCode } = useParams()
  const navigate = useNavigate()
  const links = useFactoryLinks()
  const code = routeCode ? normalizeScannedCode(routeCode) : ''
  const [typed, setTyped] = useState('')
  const [cameraOn, setCameraOn] = useState(!code)
  const [cameraProblem, setCameraProblem] = useState<string | null>(null)
  const index = useOfflineScanIndex()

  const open = useCallback((raw: string) => {
    const normalized = normalizeScannedCode(raw)
    if (!normalized) return
    setCameraOn(false)
    navigate(links.scan(normalized))
  }, [navigate, links])
  const onUnavailable = useCallback((reason: string) => {
    setCameraProblem(reason)
    setCameraOn(false)
  }, [])

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <PageHeader title="Scan" subtitle="Point the camera at an asset tag, or type its number." />

      {cameraOn ? <CameraScanner onCode={open} onUnavailable={onUnavailable} /> : null}
      {cameraProblem ? <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">{cameraProblem}</p> : null}

      <form className="flex gap-2" onSubmit={(event) => { event.preventDefault(); open(typed); setTyped('') }}>
        <input className={`${inputClass} h-12 font-mono`} value={typed} onChange={(event) => setTyped(event.target.value)} placeholder="TAG-000123 or asset number" aria-label="Tag or asset number" />
        <button type="submit" className={`${primaryButton} h-12`} disabled={!typed.trim()}>Open</button>
        {!cameraOn ? <button type="button" className={`${secondaryButton} h-12`} onClick={() => { setCameraProblem(null); setCameraOn(true) }} aria-label="Scan with camera"><Camera className="h-5 w-5" /></button> : null}
      </form>

      {code ? <ScanResultView code={code} /> : (
        <p className="flex items-center gap-2 text-xs text-slate-500">
          <ScanLine className="h-4 w-4" />
          {index.data ? `${index.data.length} assets available offline on this device.` : index.isError ? 'Could not download assets for offline use yet.' : 'Preparing assets for offline use…'}
        </p>
      )}
    </div>
  )
}

function ScanResultView({ code }: { code: string }) {
  const links = useFactoryLinks()
  const online = useScan(code)
  const index = useOfflineScanIndex()
  const offline = findOffline(index.data, code)
  const notFoundOnline = online.isError && (online.error as { response?: { status?: number } })?.response?.status === 404

  if (online.isLoading && !offline) return <p className="text-sm text-slate-500">Looking up {code}…</p>

  if (online.data) {
    const result = online.data
    return (
      <article className="space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        {result.warning ? <p className="flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{result.warning}</p> : null}
        <AssetHeading id={result.assetId} name={result.assetName} code={result.assetCode} tag={result.activeTagNumber} location={result.location} criticality={result.criticality} status={result.operationalStatus} retired={result.status === 'Inactive'} />
        {result.parent ? <p className="text-sm text-slate-600">Part of <Link className="underline" to={links.asset(result.parent.id)}>{result.parent.assetCode} · {result.parent.assetName}</Link></p> : null}
        {[result.manufacturer, result.model, result.serialNumber].some(Boolean) ? <p className="text-sm text-slate-600">{[result.manufacturer, result.model, result.serialNumber && `S/N ${result.serialNumber}`].filter(Boolean).join(' · ')}</p> : null}
        <Meters meters={result.meters} assetId={result.assetId} />
        <section>
          <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold text-slate-900"><ClipboardList className="h-4 w-4 text-slate-400" />Open work orders</h3>
          {result.openWorkOrders.length === 0 ? <p className="text-sm text-slate-500">None.</p> : (
            <ul className="space-y-1 text-sm">
              {result.openWorkOrders.map((wo) => (
                <li key={wo.id}><Link className="underline" to={links.workOrder(wo.id)}>{wo.workOrderNumber}</Link> {wo.title} <span className="text-slate-500">· {wo.status}{wo.dueDate ? ` · due ${formatDate(wo.dueDate)}` : ''}</span></li>
              ))}
            </ul>
          )}
        </section>
        {result.components.length > 0 ? (
          <section>
            <h3 className="mb-1 text-sm font-semibold text-slate-900">Components</h3>
            <ul className="flex flex-wrap gap-2 text-sm">
              {result.components.map((component) => <li key={component.id}><Link className="rounded-md bg-slate-100 px-2 py-1 hover:bg-slate-200" to={links.asset(component.id)}>{component.assetName}</Link></li>)}
            </ul>
          </section>
        ) : null}
        {result.recentHistory.length > 0 ? (
          <section>
            <h3 className="mb-1 text-sm font-semibold text-slate-900">Recent history</h3>
            <ul className="space-y-1 text-sm text-slate-600">
              {result.recentHistory.map((item, i) => <li key={i}>{formatDate(item.occurredAt)} · {item.eventType}{item.toValue ? `: ${item.toValue}` : ''}</li>)}
            </ul>
          </section>
        ) : null}
      </article>
    )
  }

  if (offline && !notFoundOnline) {
    const { entry, warning } = offline
    return (
      <article className="space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <p className="flex items-center gap-2 rounded-lg bg-slate-100 px-3 py-2 text-sm text-slate-700"><WifiOff className="h-4 w-4" />No connection: showing the copy saved on this device.</p>
        {warning ? <p className="flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{warning}</p> : null}
        <AssetHeading id={entry.assetId} name={entry.assetName} code={entry.assetCode} tag={entry.tagNumber} location={entry.location} criticality={entry.criticality} status={entry.operationalStatus} />
        {entry.parentAssetCode ? <p className="text-sm text-slate-600">Part of {entry.parentAssetCode}</p> : null}
        {[entry.manufacturer, entry.model, entry.serialNumber].some(Boolean) ? <p className="text-sm text-slate-600">{[entry.manufacturer, entry.model, entry.serialNumber && `S/N ${entry.serialNumber}`].filter(Boolean).join(' · ')}</p> : null}
        <Meters meters={entry.meters} assetId={entry.assetId} />
      </article>
    )
  }

  return (
    <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-800">
      {notFoundOnline || index.data ? `No asset found for ${code}. Check the number, or the tag may belong to a factory you don't have access to.` : 'Could not look this up: no connection and no saved copy on this device yet.'}
    </p>
  )
}

function AssetHeading({ id, name, code, tag, location, criticality, status, retired }: {
  id: string; name: string; code: string; tag: string | null; location: string | null
  criticality: 'A' | 'B' | 'C' | null; status: 'Operational' | 'Standby' | 'Down'; retired?: boolean | undefined
}) {
  const links = useFactoryLinks()
  return (
    <header className="space-y-1">
      <div className="flex flex-wrap items-center gap-2">
        <Link to={links.asset(id)} className="text-xl font-semibold text-slate-900 underline decoration-slate-300 underline-offset-4">{name}</Link>
        <CriticalityBadge value={criticality} />
        <OperationalBadge value={status} retired={retired} />
      </div>
      <p className="font-mono text-sm text-slate-600">{code}{tag && tag !== code ? ` · ${tag}` : ''}</p>
      {location ? <p className="flex items-center gap-1 text-sm text-slate-600"><MapPin className="h-4 w-4" />{location}</p> : null}
    </header>
  )
}

function Meters({ meters, assetId }: { meters: { id: string; name: string; unit: string; currentValue: number | null; lastReadingAt: string | null }[]; assetId: string }) {
  const links = useFactoryLinks()
  if (meters.length === 0) return null
  return (
    <section>
      <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold text-slate-900"><Gauge className="h-4 w-4 text-slate-400" />Meters</h3>
      <ul className="grid gap-2 sm:grid-cols-2">
        {meters.map((meter) => (
          <li key={meter.id} className="rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <span className="block text-slate-600">{meter.name}</span>
            <span className="font-semibold text-slate-900">{formatNumber(meter.currentValue, meter.unit)}</span>
            {meter.lastReadingAt ? <span className="block text-xs text-slate-500">{formatDateTime(meter.lastReadingAt)}</span> : null}
          </li>
        ))}
      </ul>
      <Link to={links.asset(assetId, 'meters')} className="mt-2 inline-block text-sm font-medium text-[var(--app-primary)]">Record a reading</Link>
    </section>
  )
}
