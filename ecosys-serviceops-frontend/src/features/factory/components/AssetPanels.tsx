import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, Download, FileText, Gauge, Plus, Printer, QrCode, Upload } from 'lucide-react'
import { Badge } from '../../../shared/components/Badge'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import {
  downloadDocument,
  downloadLabels,
  useArchiveDocument,
  useAssetDocuments,
  useAssetTags,
  useComponents,
  useLifecycle,
  useMeterTypes,
  useMeters,
  useReadings,
  useRecordReading,
  useSaveMeter,
  useTagAction,
  useUploadDocument,
} from '../api'
import { DOCUMENT_TYPES, type AssetDocument, type FactoryAsset, type Meter } from '../types'
import { ReadingsChart } from './ReadingsChart'
import { useFactoryLinks } from '../links'
import { CriticalityBadge, Field, OperationalBadge, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from './ui'

/* ── Meters ───────────────────────────────────────────────────────────────── */

export function MetersPanel({ asset, canManage, canRecord }: { asset: FactoryAsset; canManage: boolean; canRecord: boolean }) {
  const { data: meters, isLoading } = useMeters(asset.id)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const selected = meters?.find((meter) => meter.id === selectedId) ?? meters?.[0]

  if (isLoading) return <p className="text-sm text-slate-500">Loading…</p>

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        {(meters ?? []).map((meter) => (
          <button
            key={meter.id}
            type="button"
            onClick={() => setSelectedId(meter.id)}
            className={cn('rounded-lg border px-3 py-2 text-left text-sm', selected?.id === meter.id ? 'border-[var(--app-primary)] bg-slate-50' : 'border-slate-200 bg-white hover:bg-slate-50')}
          >
            <span className="block font-medium text-slate-900">{meter.name}{meter.isPrimary ? <Badge size="sm" className="ml-2">Main</Badge> : null}</span>
            <span className="text-slate-600">{formatNumber(meter.currentValue, meter.unit)}</span>
          </button>
        ))}
        {canManage ? <button type="button" className={secondaryButton} onClick={() => setAdding(true)}><Plus className="h-4 w-4" />Add meter</button> : null}
      </div>
      {(meters ?? []).length === 0 ? <p className="text-sm text-slate-500">No meters yet. Add run hours, production, temperature or any other reading you track.</p> : null}
      {selected ? <MeterDetail asset={asset} meter={selected} canRecord={canRecord} /> : null}
      {adding ? <AddMeterDrawer assetId={asset.id} onClose={() => setAdding(false)} /> : null}
    </div>
  )
}

function MeterDetail({ asset, meter, canRecord }: { asset: FactoryAsset; meter: Meter; canRecord: boolean }) {
  const { data: readings } = useReadings(meter.id)
  const record = useRecordReading(asset.id)
  const [value, setValue] = useState('')
  const [readAt, setReadAt] = useState('')
  const [isReset, setIsReset] = useState(false)

  const submit = async () => {
    try {
      const reading = await record.mutateAsync({ meterId: meter.id, value: Number(value), readAt: readAt || null, isReset })
      toast[reading.isOutOfRange ? 'error' : 'success'](reading.isOutOfRange ? `Recorded. ${formatNumber(reading.value, meter.unit)} is outside the limits; asset managers have been told.` : 'Reading recorded')
      setValue('')
      setReadAt('')
      setIsReset(false)
    } catch {
      // The API client shows the error (e.g. a counter going down).
    }
  }

  const limits = [meter.minThreshold, meter.maxThreshold].some((x) => x !== null)
    ? `Limits ${formatNumber(meter.minThreshold)} to ${formatNumber(meter.maxThreshold)} ${meter.unit}`
    : null

  return (
    <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="flex items-center gap-2 font-semibold text-slate-900"><Gauge className="h-4 w-4 text-slate-400" />{meter.name}</h3>
        <span className="text-xs text-slate-500">{meter.isCumulative ? 'Counter' : 'Measurement'} · {meter.unit}{limits ? ` · ${limits}` : ''}</span>
      </div>
      <ReadingsChart readings={readings ?? []} unit={meter.unit} min={meter.minThreshold} max={meter.maxThreshold} />
      {canRecord ? (
        <div className="grid gap-3 sm:grid-cols-[1fr_1fr_auto_auto] sm:items-end">
          <Field label={`New reading (${meter.unit})`}><input type="number" inputMode="decimal" className={inputClass} value={value} onChange={(event) => setValue(event.target.value)} /></Field>
          <Field label="Taken at" hint="Blank means now."><input type="datetime-local" className={inputClass} value={readAt} onChange={(event) => setReadAt(event.target.value)} /></Field>
          {meter.isCumulative ? (
            <label className="inline-flex h-10 items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={isReset} onChange={(event) => setIsReset(event.target.checked)} />Meter was reset</label>
          ) : <span />}
          <button type="button" className={primaryButton} disabled={value === '' || record.isPending} onClick={() => void submit()}>Record</button>
        </div>
      ) : null}
      <div className="max-h-72 overflow-y-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-white text-left text-xs text-slate-500">
            <tr><th className="py-1">When</th><th>Reading</th>{meter.isCumulative ? <th>Change</th> : null}<th>By</th><th /></tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {(readings ?? []).map((reading) => (
              <tr key={reading.id}>
                <td className="py-1.5 text-slate-600">{formatDateTime(reading.readAt)}</td>
                <td className="font-medium text-slate-900">{formatNumber(reading.value, meter.unit)}</td>
                {meter.isCumulative ? <td className="text-slate-600">{reading.isReset ? 'Reset' : reading.delta !== null ? `+${formatNumber(reading.delta)}` : '—'}</td> : null}
                <td className="text-slate-600">{reading.recordedByName ?? reading.source}</td>
                <td>{reading.isOutOfRange ? <Badge tone="danger" size="sm"><AlertTriangle className="h-3 w-3" />Outside limits</Badge> : null}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function AddMeterDrawer({ assetId, onClose }: { assetId: string; onClose: () => void }) {
  const { data: types } = useMeterTypes()
  const save = useSaveMeter(assetId)
  const [form, setForm] = useState({ name: '', meterType: 'RunHours', unit: '', isPrimary: false, minThreshold: '', maxThreshold: '', initialValue: '' })
  const type = types?.find((item) => item.meterType === form.meterType)
  const set = (key: keyof typeof form, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }))

  const submit = async () => {
    try {
      await save.mutateAsync({
        payload: {
          name: form.name,
          meterType: form.meterType,
          unit: form.unit || null,
          isPrimary: form.isPrimary,
          minThreshold: form.minThreshold === '' ? null : Number(form.minThreshold),
          maxThreshold: form.maxThreshold === '' ? null : Number(form.maxThreshold),
          initialValue: form.initialValue === '' ? null : Number(form.initialValue),
        },
      })
      toast.success('Meter added')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open title="Add meter" onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!form.name.trim() || save.isPending} onClick={() => void submit()}>Add meter</button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(event) => set('name', event.target.value)} placeholder="e.g. Bearing temperature" /></Field>
        <Field label="Kind">
          <select className={inputClass} value={form.meterType} onChange={(event) => set('meterType', event.target.value)}>
            {(types ?? []).map((item) => <option key={item.meterType} value={item.meterType}>{item.meterType.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}
          </select>
        </Field>
        <Field label="Unit" hint={type ? `Blank uses ${type.defaultUnit || 'no unit'}.` : undefined}><input className={inputClass} value={form.unit} onChange={(event) => set('unit', event.target.value)} /></Field>
        <Field label="First reading"><input type="number" className={inputClass} value={form.initialValue} onChange={(event) => set('initialValue', event.target.value)} /></Field>
        <Field label="Low limit" hint="Readings below it alert asset managers."><input type="number" className={inputClass} value={form.minThreshold} onChange={(event) => set('minThreshold', event.target.value)} /></Field>
        <Field label="High limit"><input type="number" className={inputClass} value={form.maxThreshold} onChange={(event) => set('maxThreshold', event.target.value)} /></Field>
        {type?.isCumulative ? (
          <label className="inline-flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
            <input type="checkbox" checked={form.isPrimary} onChange={(event) => set('isPrimary', event.target.checked)} />
            Main meter (used for meter-based maintenance)
          </label>
        ) : null}
      </div>
    </FormDrawer>
  )
}

/* ── Documents ────────────────────────────────────────────────────────────── */

export function DocumentsPanel({ asset, canManage }: { asset: FactoryAsset; canManage: boolean }) {
  const [showArchived, setShowArchived] = useState(false)
  const { data: documents, isLoading } = useAssetDocuments(asset.id, showArchived)
  const archive = useArchiveDocument(asset.id)
  const [upload, setUpload] = useState<{ document?: AssetDocument | undefined } | null>(null)

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <label className="inline-flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={showArchived} onChange={(event) => setShowArchived(event.target.checked)} />Show archived</label>
        {canManage ? <button type="button" className={secondaryButton} onClick={() => setUpload({})}><Upload className="h-4 w-4" />Add document</button> : null}
      </div>
      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : (documents ?? []).length === 0 ? <p className="text-sm text-slate-500">No manuals, drawings or certificates yet.</p> : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
          {(documents ?? []).map((document) => {
            const current = document.versions[0]
            return (
              <li key={document.id} className={cn('flex flex-wrap items-center gap-3 px-4 py-3', document.isArchived && 'opacity-60')}>
                <FileText className="h-5 w-5 shrink-0 text-slate-400" />
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-slate-900">{document.title}</p>
                  <p className="text-xs text-slate-500">
                    {document.documentType} · version {document.currentVersion}{current ? ` · ${formatDate(current.uploadedAt)}${current.uploadedByName ? ` by ${current.uploadedByName}` : ''}` : ''}
                    {document.expiryDate ? ` · valid until ${formatDate(document.expiryDate)}` : ''}
                  </p>
                  {current?.notes ? <p className="text-xs text-slate-600">{current.notes}</p> : null}
                </div>
                {document.isExpired ? <Badge tone="danger" size="sm">Expired</Badge> : null}
                {current ? <button type="button" className={secondaryButton} onClick={() => void downloadDocument(current.downloadUrl, current.fileName)}><Download className="h-4 w-4" />Open</button> : null}
                {document.versions.length > 1 ? (
                  <select aria-label="Earlier versions" className={cn(inputClass, 'w-auto')} value="" onChange={(event) => {
                    const version = document.versions.find((item) => String(item.versionNumber) === event.target.value)
                    if (version) void downloadDocument(version.downloadUrl, version.fileName)
                  }}>
                    <option value="">Earlier versions</option>
                    {document.versions.slice(1).map((version) => <option key={version.versionNumber} value={version.versionNumber}>v{version.versionNumber} · {formatDate(version.uploadedAt)}</option>)}
                  </select>
                ) : null}
                {canManage ? (
                  <>
                    {!document.isArchived ? <button type="button" className="text-sm font-medium text-[var(--app-primary)]" onClick={() => setUpload({ document })}>New version</button> : null}
                    <button type="button" className="text-sm text-slate-500 hover:text-slate-800" onClick={() => void archive.mutateAsync({ id: document.id, archived: !document.isArchived }).catch(() => undefined)}>
                      {document.isArchived ? 'Restore' : 'Archive'}
                    </button>
                  </>
                ) : null}
              </li>
            )
          })}
        </ul>
      )}
      {upload ? <UploadDocumentDrawer assetId={asset.id} document={upload.document} onClose={() => setUpload(null)} /> : null}
    </div>
  )
}

function UploadDocumentDrawer({ assetId, document, onClose }: { assetId: string; document?: AssetDocument | undefined; onClose: () => void }) {
  const upload = useUploadDocument(assetId)
  const [title, setTitle] = useState('')
  const [documentType, setDocumentType] = useState<string>('Manual')
  const [expiryDate, setExpiryDate] = useState(document?.expiryDate?.slice(0, 10) ?? '')
  const [notes, setNotes] = useState('')
  const [file, setFile] = useState<File | null>(null)

  const submit = async () => {
    if (!file) return
    const form = new FormData()
    form.append('File', file)
    if (!document) {
      form.append('Title', title)
      form.append('DocumentType', documentType)
    }
    if (notes) form.append('Notes', notes)
    if (expiryDate) form.append('ExpiryDate', expiryDate)
    try {
      await upload.mutateAsync({ documentId: document?.id, form })
      toast.success(document ? `Version ${document.currentVersion + 1} uploaded` : 'Document added')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open title={document ? `New version of ${document.title}` : 'Add document'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!file || (!document && !title.trim()) || upload.isPending} onClick={() => void submit()}>{upload.isPending ? 'Uploading…' : 'Upload'}</button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        {!document ? (
          <>
            <Field label="Title" required><input className={inputClass} value={title} onChange={(event) => setTitle(event.target.value)} placeholder="e.g. O&M manual" /></Field>
            <Field label="Type">
              <select className={inputClass} value={documentType} onChange={(event) => setDocumentType(event.target.value)}>
                {DOCUMENT_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
              </select>
            </Field>
          </>
        ) : null}
        <Field label="File" required hint="PDF, Word, Excel or image, up to 25 MB." className="sm:col-span-2">
          <input type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.csv,.jpg,.jpeg,.png,.webp" onChange={(event) => setFile(event.target.files?.[0] ?? null)} className="block w-full text-sm" />
        </Field>
        <Field label="Valid until" hint="For certificates."><input type="date" className={inputClass} value={expiryDate} onChange={(event) => setExpiryDate(event.target.value)} /></Field>
        <Field label="What changed" className="sm:col-span-2"><textarea className={textareaClass} value={notes} onChange={(event) => setNotes(event.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Tags ─────────────────────────────────────────────────────────────────── */

export function TagsPanel({ asset, canManage }: { asset: FactoryAsset; canManage: boolean }) {
  const { data: tags } = useAssetTags(asset.id)
  const action = useTagAction(asset.id)
  const [prePrinted, setPrePrinted] = useState('')
  const active = tags?.find((tag) => tag.status === 'Active')
  const retired = asset.status === 'Inactive'

  const run = async (request: Parameters<typeof action.mutateAsync>[0], message: string) => {
    try {
      await action.mutateAsync(request)
      toast.success(message)
      setPrePrinted('')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <div className="space-y-4">
      {active ? (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 bg-white p-4">
          <QrCode className="h-8 w-8 text-slate-500" />
          <div className="flex-1">
            <p className="font-mono text-lg font-semibold text-slate-900">{active.tagNumber}</p>
            <p className="text-xs text-slate-500">{active.symbology} · issued {formatDate(active.issuedAt)} · printed {active.printCount} time{active.printCount === 1 ? '' : 's'}</p>
          </div>
          {canManage ? (
            <>
              <button type="button" className={secondaryButton} onClick={() => void downloadLabels({ tagIds: [active.id], layout: 'a4-10' })}><Printer className="h-4 w-4" />Print label</button>
              <button type="button" className={secondaryButton} disabled={action.isPending} onClick={() => {
                const reason = window.prompt('Why is the tag being replaced? (for example damaged, faded or missing)')
                if (reason?.trim()) void run({ action: 'replace', tagId: active.id, reason }, 'New tag issued. Print and fix it, then remove the old one.')
              }}>Replace tag</button>
            </>
          ) : null}
        </div>
      ) : (
        <div className="space-y-3 rounded-lg border border-dashed border-slate-300 bg-white p-4">
          <p className="text-sm text-slate-600">{retired ? 'Retired assets are not tagged.' : 'This asset has no tag yet.'}</p>
          {canManage && !retired ? (
            <div className="flex flex-wrap items-end gap-2">
              <button type="button" className={primaryButton} disabled={action.isPending} onClick={() => void run({ action: 'issue' }, 'Tag issued')}>Issue next tag number</button>
              <span className="text-sm text-slate-500">or register a pre-printed tag:</span>
              <input className={cn(inputClass, 'w-48 font-mono uppercase')} placeholder="Tag number" value={prePrinted} onChange={(event) => setPrePrinted(event.target.value.toUpperCase())} />
              <button type="button" className={secondaryButton} disabled={!prePrinted.trim() || action.isPending} onClick={() => void run({ action: 'issue', tagNumber: prePrinted }, 'Tag registered')}>Register</button>
            </div>
          ) : null}
        </div>
      )}
      {(tags ?? []).some((tag) => tag.status !== 'Active') ? (
        <div>
          <h4 className="mb-2 text-sm font-semibold text-slate-900">Earlier tags</h4>
          <ul className="space-y-1 text-sm text-slate-600">
            {(tags ?? []).filter((tag) => tag.status !== 'Active').map((tag) => (
              <li key={tag.id}><span className="font-mono">{tag.tagNumber}</span> · {tag.status.toLowerCase()} {tag.retiredAt ? formatDate(tag.retiredAt) : ''}{tag.retiredReason ? `: ${tag.retiredReason}` : ''}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}

/* ── History and components ───────────────────────────────────────────────── */

const EVENT_LABELS: Record<string, string> = {
  Registered: 'Registered',
  Commissioned: 'Commissioned',
  Tagged: 'Tagged',
  Transferred: 'Transferred',
  ComponentAttached: 'Fitted as a component',
  ComponentDetached: 'Removed as a component',
  StatusChanged: 'Condition changed',
  Decommissioned: 'Retired',
  Reactivated: 'Back in service',
  Disposed: 'Disposed of',
}

export function HistoryPanel({ assetId }: { assetId: string }) {
  const { data: events, isLoading } = useLifecycle(assetId)
  if (isLoading) return <p className="text-sm text-slate-500">Loading…</p>
  return (
    <ol className="space-y-3 border-l border-slate-200 pl-4">
      {(events ?? []).map((event) => (
        <li key={event.id} className="relative">
          <span className="absolute -left-[1.3rem] top-1.5 h-2.5 w-2.5 rounded-full border-2 border-white bg-[var(--app-primary)]" />
          <p className="text-sm font-medium text-slate-900">
            {EVENT_LABELS[event.eventType] ?? event.eventType}
            {event.fromValue || event.toValue ? <span className="font-normal text-slate-600"> {event.fromValue ? `${event.fromValue} → ` : ''}{event.toValue}</span> : null}
          </p>
          <p className="text-xs text-slate-500">{formatDateTime(event.occurredAt)}{event.performedByName ? ` · ${event.performedByName}` : ''}</p>
          {event.notes ? <p className="text-sm text-slate-600">{event.notes}</p> : null}
        </li>
      ))}
    </ol>
  )
}

export function ComponentsPanel({ asset, canManage, onAdd }: { asset: FactoryAsset; canManage: boolean; onAdd: () => void }) {
  const { data: components, isLoading } = useComponents(asset.id)
  const links = useFactoryLinks()
  const depthOf = (id: string | null, guard = 0): number => {
    const parent = components?.find((item) => item.id === id)
    return parent && guard < 10 ? 1 + depthOf(parent.parentAssetId, guard + 1) : 0
  }
  return (
    <div className="space-y-3">
      {canManage && asset.status !== 'Inactive' ? <button type="button" className={secondaryButton} onClick={onAdd}><Plus className="h-4 w-4" />Add component</button> : null}
      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : (components ?? []).length === 0 ? <p className="text-sm text-slate-500">No components recorded. Add parts you want history on, such as motors, gearboxes or roller segments.</p> : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
          {(components ?? []).map((component) => (
            <li key={component.id} className="flex items-center gap-3 py-2 pr-4" style={{ paddingLeft: `${1 + depthOf(component.parentAssetId) * 1.25}rem` }}>
              <Link to={links.asset(component.id)} className="flex-1 text-sm font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{component.assetName}</Link>
              <span className="font-mono text-xs text-slate-500">{component.assetCode}</span>
              <CriticalityBadge value={component.criticality} />
              <OperationalBadge value={component.operationalStatus} retired={component.status === 'Inactive'} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
