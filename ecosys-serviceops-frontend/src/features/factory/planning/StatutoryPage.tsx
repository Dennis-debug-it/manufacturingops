import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FileCheck2, FileText, Plus, ShieldAlert } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { api } from '../../../shared/api/apiClient'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useOrgNodes } from '../api'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useTeamUsers } from '../../team/api'
import { AssetPicker, type PickedAsset } from './AssetPicker'
import { COMPLIANCE_KIND_LABELS, useComplianceAction, useComplianceItem, useComplianceItems, useComplianceSummary, type ComplianceItem, type ComplianceStatus } from './api'

const STATUS: Record<ComplianceStatus, { label: string; tone: BadgeTone }> = {
  Overdue: { label: 'Overdue', tone: 'danger' },
  DueSoon: { label: 'Due soon', tone: 'warning' },
  Ok: { label: 'In date', tone: 'success' },
  NoDate: { label: 'No date', tone: 'neutral' },
}

const FILTERS: { id: '' | ComplianceStatus; label: string }[] = [
  { id: '', label: 'All' }, { id: 'Overdue', label: 'Overdue' }, { id: 'DueSoon', label: 'Due in 30 days' }, { id: 'NoDate', label: 'No date' }, { id: 'Ok', label: 'In date' },
]

function dueText(item: ComplianceItem) {
  if (item.daysLeft == null) return 'Enter the last inspection'
  if (item.daysLeft < 0) return `${-item.daysLeft} days overdue`
  if (item.daysLeft === 0) return 'Due today'
  return `in ${item.daysLeft} days`
}

/** Statutory inspections, calibrations, fire safety and certificates: what is due, what has lapsed, and the evidence. */
export function StatutoryPage() {
  const [params, setParams] = useSearchParams()
  const status = (FILTERS.find((x) => x.id === params.get('status'))?.id ?? '') as '' | ComplianceStatus
  const canManage = usePermission('work-orders.assign')
  const [orgNodeId, setOrgNodeId] = useState('')
  const [kind, setKind] = useState('')
  const summary = useComplianceSummary(orgNodeId || undefined)
  const items = useComplianceItems({ orgNodeId: orgNodeId || undefined, status: status || undefined, kind: kind || undefined })
  const [openId, setOpenId] = useState<string | null>(null)
  const [editing, setEditing] = useState<ComplianceItem | 'new' | null>(null)
  const [installOpen, setInstallOpen] = useState(false)

  return (
    <div className="space-y-4">
      <PageHeader title="Statutory and calibration" subtitle="Boilers, pressure vessels, lifting gear, weighing, fire safety and licences: due dates, certificates and owners."
        primaryAction={canManage ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add item</button> : undefined}
        secondaryActions={canManage ? <button type="button" className={secondaryButton} onClick={() => setInstallOpen(true)}>Add the standard list</button> : undefined} />

      {summary.data ? (
        <div className="grid gap-3 sm:grid-cols-4">
          <SummaryTile label="Overdue" value={summary.data.overdue} tone="danger" onClick={() => setParams({ status: 'Overdue' })} />
          <SummaryTile label="Due in 30 days" value={summary.data.dueSoon} tone="warning" onClick={() => setParams({ status: 'DueSoon' })} />
          <SummaryTile label="No date yet" value={summary.data.noDate} tone="neutral" onClick={() => setParams({ status: 'NoDate' })} />
          <SummaryTile label="In date" value={summary.data.ok} tone="success" onClick={() => setParams({ status: 'Ok' })} />
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {FILTERS.map((item) => (
            <button key={item.id} type="button" role="tab" aria-selected={status === item.id} onClick={() => setParams(item.id ? { status: item.id } : {}, { replace: true })}
              className={cn('h-9 rounded-md px-3 text-sm font-medium', status === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
          ))}
        </div>
        <select aria-label="Kind" className={cn(inputClass, 'w-full sm:w-52')} value={kind} onChange={(event) => setKind(event.target.value)}>
          <option value="">Any kind</option>
          {Object.entries(COMPLIANCE_KIND_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <div className="w-full sm:w-60"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>
      </div>

      {items.isLoading ? <Spinner /> : (items.data ?? []).length === 0 ? (
        <EmptyState icon={FileCheck2} title={status ? 'Nothing here' : 'No statutory items yet'} description={status ? 'No items match this filter.' : 'Add the standard list for a factory to start.'} />
      ) : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Item</th><th className="px-4 py-2">Where</th><th className="px-4 py-2">Due</th><th className="px-4 py-2">Owner</th><th className="px-4 py-2">Certificate</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(items.data ?? []).map((item) => (
                <tr key={item.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpenId(item.id)}>
                  <td className="px-4 py-2">
                    <span className="font-medium text-slate-900">{item.name}</span>
                    <span className="block text-xs text-slate-500">{COMPLIANCE_KIND_LABELS[item.kind] ?? item.kind}{item.regulation ? ` · ${item.regulation}` : ''}</span>
                  </td>
                  <td className="px-4 py-2 text-slate-600">{item.assetCode ? <span className="font-mono text-xs">{item.assetCode}</span> : item.orgNodeName}</td>
                  <td className="whitespace-nowrap px-4 py-2">
                    <Badge size="sm" tone={STATUS[item.status].tone}>{STATUS[item.status].label}</Badge>
                    <span className={cn('block text-xs', item.status === 'Overdue' ? 'font-medium text-rose-700' : 'text-slate-500')}>{item.dueDate ? `${formatDate(item.dueDate)} · ` : ''}{dueText(item)}</span>
                  </td>
                  <td className="px-4 py-2 text-slate-600">{item.ownerName ?? <span className="text-slate-400">—</span>}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{item.lastCertificateNumber ?? (item.lastCertificateId ? 'On file' : '—')}{item.openWorkOrderNumber ? <span className="block">Job {item.openWorkOrderNumber}</span> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ItemDetailDrawer id={openId} canManage={canManage} onClose={() => setOpenId(null)} onEdit={(item) => { setOpenId(null); setEditing(item) }} />
      <ItemFormDrawer item={editing} onClose={() => setEditing(null)} />
      <InstallDrawer open={installOpen} onClose={() => setInstallOpen(false)} />
    </div>
  )
}

function SummaryTile({ label, value, tone, onClick }: { label: string; value: number; tone: BadgeTone; onClick: () => void }) {
  const colour = tone === 'danger' && value > 0 ? 'text-rose-700' : tone === 'warning' && value > 0 ? 'text-amber-700' : 'text-slate-900'
  return (
    <button type="button" onClick={onClick} className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 text-left shadow-sm hover:border-slate-300">
      <p className="flex items-center gap-1.5 text-sm text-slate-600">{tone === 'danger' && value > 0 ? <ShieldAlert className="h-4 w-4 text-rose-600" /> : null}{label}</p>
      <p className={cn('mt-1 text-2xl font-semibold tabular-nums', colour)}>{value}</p>
    </button>
  )
}

/** Opens a certificate with the signed-in user's token (a plain link would not carry it). */
async function openCertificate(id: string, fileName: string | null) {
  try {
    const blob = await api.get<Blob>(`/api/factory/compliance/certificates/${id}`, undefined, { responseType: 'blob' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.target = '_blank'
    link.rel = 'noopener'
    if (fileName && !blob.type.startsWith('image/') && blob.type !== 'application/pdf') link.download = fileName
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch {
    // The API client shows the error.
  }
}

function ItemDetailDrawer({ id, canManage, onClose, onEdit }: { id: string | null; canManage: boolean; onClose: () => void; onEdit: (item: ComplianceItem) => void }) {
  const links = useFactoryLinks()
  const canRecord = usePermission('work-orders.complete') || canManage
  const { data } = useComplianceItem(id)
  const action = useComplianceAction()
  const [recording, setRecording] = useState(false)
  const [form, setForm] = useState({ doneAt: '', result: 'Pass', validUntil: '', certificateNumber: '', examiner: '', notes: '' })
  const [file, setFile] = useState<File | null>(null)
  useEffect(() => { setRecording(false); setFile(null); setForm({ doneAt: new Date().toISOString().slice(0, 10), result: 'Pass', validUntil: '', certificateNumber: '', examiner: '', notes: '' }) }, [id])

  const record = async () => {
    if (!id) return
    const body = new FormData()
    body.append('doneAt', form.doneAt)
    body.append('result', form.result)
    if (form.validUntil) body.append('validUntil', form.validUntil)
    if (form.certificateNumber) body.append('certificateNumber', form.certificateNumber)
    if (form.examiner) body.append('examiner', form.examiner)
    if (form.notes) body.append('notes', form.notes)
    if (file) body.append('certificate', file)
    try {
      await action.mutateAsync({ path: `/items/${id}/records`, body })
      toast.success('Recorded')
      setRecording(false)
      setFile(null)
    } catch {
      // The API client shows the error.
    }
  }

  const item = data?.item
  return (
    <FormDrawer open={Boolean(id)} title={item?.name ?? 'Statutory item'} onClose={onClose} footer={item ? (
      <div className="flex justify-end gap-2">
        {canManage ? <button type="button" className={secondaryButton} onClick={() => onEdit(item)}>Edit</button> : null}
        {canRecord && !recording ? <button type="button" className={primaryButton} onClick={() => setRecording(true)}>Record inspection</button> : null}
      </div>
    ) : undefined}>
      {!data || !item ? <Spinner /> : (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap items-center gap-2"><Badge tone={STATUS[item.status].tone}>{STATUS[item.status].label}</Badge><span className={cn(item.status === 'Overdue' && 'font-medium text-rose-700')}>{item.dueDate ? `Due ${formatDate(item.dueDate)} · ` : ''}{dueText(item)}</span></div>
          <dl className="grid grid-cols-[minmax(0,8rem)_1fr] gap-x-4 gap-y-2">
            <dt className="text-slate-500">Kind</dt><dd>{COMPLIANCE_KIND_LABELS[item.kind] ?? item.kind}</dd>
            {item.regulation ? <><dt className="text-slate-500">Rule</dt><dd>{item.regulation}</dd></> : null}
            <dt className="text-slate-500">Where</dt><dd>{item.assetId ? <Link className="underline" to={links.asset(item.assetId)}>{item.assetCode} · {item.assetName}</Link> : item.orgNodeName}</dd>
            <dt className="text-slate-500">Every</dt><dd>{item.intervalMonths} months</dd>
            <dt className="text-slate-500">Owner</dt><dd>{item.ownerName ?? '—'}</dd>
            {item.provider ? <><dt className="text-slate-500">Provider</dt><dd>{item.provider}</dd></> : null}
            <dt className="text-slate-500">Job</dt><dd>{item.openWorkOrderId ? <Link className="underline" to={links.job(item.openWorkOrderId)}>{item.openWorkOrderNumber}</Link> : item.raiseJobDaysBefore != null ? `Raised ${item.raiseJobDaysBefore} days before it is due` : 'No job is raised'}</dd>
          </dl>
          {item.notes ? <p className="whitespace-pre-wrap text-slate-700">{item.notes}</p> : null}

          {recording ? (
            <div className="grid gap-3 rounded-lg border border-slate-200 p-3 sm:grid-cols-2">
              <Field label="Done on" required><input type="date" className={inputClass} value={form.doneAt} onChange={(event) => setForm({ ...form, doneAt: event.target.value })} /></Field>
              <Field label="Result">
                <select className={inputClass} value={form.result} onChange={(event) => setForm({ ...form, result: event.target.value })}>
                  <option value="Pass">Pass</option><option value="Conditional">Pass with conditions</option><option value="Fail">Fail</option>
                </select>
              </Field>
              <Field label="Certificate number"><input className={inputClass} value={form.certificateNumber} onChange={(event) => setForm({ ...form, certificateNumber: event.target.value })} /></Field>
              <Field label="Valid until" hint={`Blank: ${item.intervalMonths} months on.`}><input type="date" className={inputClass} value={form.validUntil} onChange={(event) => setForm({ ...form, validUntil: event.target.value })} disabled={form.result === 'Fail'} /></Field>
              <Field label="Examiner or lab" className="sm:col-span-2"><input className={inputClass} value={form.examiner} onChange={(event) => setForm({ ...form, examiner: event.target.value })} /></Field>
              <Field label="Certificate (PDF or photo)" className="sm:col-span-2"><input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" className="block w-full text-sm" onChange={(event) => setFile(event.target.files?.[0] ?? null)} /></Field>
              <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} placeholder={form.result === 'Fail' ? 'What failed, and what must be fixed' : undefined} /></Field>
              {form.result === 'Fail' ? <p className="rounded-lg bg-rose-50 p-2 text-xs text-rose-800 sm:col-span-2">A failed examination leaves the item due now, until a pass is recorded.</p> : null}
              <div className="flex justify-end gap-2 sm:col-span-2">
                <button type="button" className={secondaryButton} onClick={() => setRecording(false)}>Back</button>
                <button type="button" className={primaryButton} disabled={!form.doneAt || action.isPending} onClick={() => void record()}>Save</button>
              </div>
            </div>
          ) : null}

          <section>
            <h3 className="mb-2 font-semibold text-slate-800">Inspections and certificates</h3>
            {data.records.length === 0 ? <p className="text-slate-500">None recorded yet.</p> : (
              <ol className="space-y-2">
                {data.records.map((r) => (
                  <li key={r.id} className="rounded-lg border border-slate-200 p-3">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{formatDate(r.doneAt)}</span>
                      <Badge size="sm" tone={r.result === 'Pass' ? 'success' : r.result === 'Fail' ? 'danger' : 'warning'}>{r.result === 'Conditional' ? 'Conditions' : r.result}</Badge>
                      {r.validUntil ? <span className="text-xs text-slate-500">valid to {formatDate(r.validUntil)}</span> : null}
                    </div>
                    <p className="text-xs text-slate-600">{[r.certificateNumber, r.examiner, r.recordedByName ? `recorded by ${r.recordedByName}` : null].filter(Boolean).join(' · ')}</p>
                    {r.notes ? <p className="mt-1 text-xs text-slate-700">{r.notes}</p> : null}
                    {r.certificateId ? <button type="button" className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-[var(--app-primary)] underline" onClick={() => void openCertificate(r.certificateId!, r.certificateFileName)}><FileText className="h-3.5 w-3.5" />{r.certificateFileName ?? 'Certificate'}</button> : null}
                  </li>
                ))}
              </ol>
            )}
          </section>
        </div>
      )}
    </FormDrawer>
  )
}

function ItemFormDrawer({ item, onClose }: { item: ComplianceItem | 'new' | null; onClose: () => void }) {
  const action = useComplianceAction()
  const users = useTeamUsers()
  const existing = item && item !== 'new' ? item : null
  const [asset, setAsset] = useState<PickedAsset | null>(null)
  const [form, setForm] = useState({ name: '', kind: 'StatutoryInspection', regulation: '', interval: '12', orgNodeId: '', lastDone: '', due: '', owner: '', provider: '', lead: '30', notes: '', active: true })
  useEffect(() => {
    if (!item) return
    setAsset(existing?.assetId ? { id: existing.assetId, code: existing.assetCode ?? '', name: existing.assetName ?? '', categoryId: null } : null)
    setForm(existing ? {
      name: existing.name, kind: existing.kind, regulation: existing.regulation ?? '', interval: String(existing.intervalMonths), orgNodeId: existing.orgNodeId,
      lastDone: existing.lastDoneAt?.slice(0, 10) ?? '', due: existing.dueDate?.slice(0, 10) ?? '', owner: existing.ownerUserId ?? '', provider: existing.provider ?? '',
      lead: existing.raiseJobDaysBefore?.toString() ?? '', notes: existing.notes ?? '', active: existing.isActive,
    } : { name: '', kind: 'StatutoryInspection', regulation: '', interval: '12', orgNodeId: '', lastDone: '', due: '', owner: '', provider: '', lead: '30', notes: '', active: true })
  }, [item, existing])

  const submit = async () => {
    const body = {
      name: form.name, kind: form.kind, regulation: form.regulation || null, intervalMonths: Number(form.interval) || 12, orgNodeId: asset ? null : form.orgNodeId || null, assetId: asset?.id ?? null,
      lastDoneAt: form.lastDone || null, dueDate: form.due || null, ownerUserId: form.owner || null, provider: form.provider || null,
      raiseJobDaysBefore: form.lead === '' ? null : Number(form.lead), notes: form.notes || null, isActive: form.active,
    }
    try {
      await action.mutateAsync(existing ? { method: 'put', path: `/items/${existing.id}`, body } : { path: '/items', body })
      toast.success('Saved')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={item !== null} title={existing ? `Edit · ${existing.name}` : 'New statutory item'} onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={action.isPending || !form.name.trim() || (!asset && !form.orgNodeId)} onClick={() => void submit()}>Save</button></div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2"><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="Boiler examination – BLR-01" /></Field>
        <Field label="Kind">
          <select className={inputClass} value={form.kind} onChange={(event) => setForm({ ...form, kind: event.target.value })}>
            {Object.entries(COMPLIANCE_KIND_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </Field>
        <Field label="Every (months)"><input type="number" min={1} max={120} className={inputClass} value={form.interval} onChange={(event) => setForm({ ...form, interval: event.target.value })} /></Field>
        <Field label="Rule" className="sm:col-span-2"><input className={inputClass} value={form.regulation} onChange={(event) => setForm({ ...form, regulation: event.target.value })} placeholder="OSHA 2007 s.67 (DOSH)" /></Field>
        <Field label="Asset" className="sm:col-span-2"><AssetPicker value={asset} onChange={setAsset} /></Field>
        {!asset ? <Field label="Or where it applies" className="sm:col-span-2"><LocationSelect value={form.orgNodeId} onChange={(value) => setForm({ ...form, orgNodeId: value })} includeGroups /></Field> : null}
        <Field label="Last done"><input type="date" className={inputClass} value={form.lastDone} onChange={(event) => setForm({ ...form, lastDone: event.target.value })} /></Field>
        <Field label="Due" hint="Blank: last done + interval."><input type="date" className={inputClass} value={form.due} onChange={(event) => setForm({ ...form, due: event.target.value })} /></Field>
        <Field label="Owner">
          <select className={inputClass} value={form.owner} onChange={(event) => setForm({ ...form, owner: event.target.value })}>
            <option value="">—</option>
            {(users.data ?? []).filter((u) => u.isActive).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
          </select>
        </Field>
        <Field label="Provider"><input className={inputClass} value={form.provider} onChange={(event) => setForm({ ...form, provider: event.target.value })} placeholder="Examiner, lab or service company" /></Field>
        <Field label="Raise a job" hint="Days before it is due; blank for none."><input type="number" min={0} max={180} className={inputClass} value={form.lead} onChange={(event) => setForm({ ...form, lead: event.target.value })} /></Field>
        <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></Field>
        {existing ? <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={form.active} onChange={(event) => setForm({ ...form, active: event.target.checked })} />Active</label> : null}
      </div>
    </FormDrawer>
  )
}

function InstallDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const nodes = useOrgNodes()
  const factories = useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory' && x.isActive), [nodes.data])
  const action = useComplianceAction()
  const [factoryId, setFactoryId] = useState('')
  useEffect(() => { if (open && factories.length === 1) setFactoryId(factories[0]!.id) }, [open, factories])
  return (
    <FormDrawer open={open} title="Add the standard statutory list" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!factoryId || action.isPending}
          onClick={() => void action.mutateAsync({ path: '/install', body: { factoryId } }).then((result) => { toast.success(`${(result as { itemsAdded: number }).itemsAdded} items added`); onClose() }).catch(() => undefined)}>Add</button></div>
    }>
      <div className="space-y-3 text-sm">
        <p className="text-slate-600">Adds the usual Kenyan tea factory items: boiler examination and hydraulic test per boiler, air receivers per compressor, lifting appliances and tackle, weighbridge and scales, metal detector, fire extinguishers and fire audit, OSH audit, workplace registration and medical examinations. Items already there are skipped. Then enter each one's last inspection date.</p>
        <Field label="Factory" required>
          <select className={inputClass} value={factoryId} onChange={(event) => setFactoryId(event.target.value)}>
            <option value="">Choose</option>
            {factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
        </Field>
      </div>
    </FormDrawer>
  )
}
