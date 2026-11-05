import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FileText, HardHat, Plus, Receipt, X } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission, usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useSuppliers } from '../../inventory/purchaseOrdersApi'
import { useOrgNodes } from '../api'
import { Field, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { AssetPicker, type PickedAsset } from '../planning/AssetPicker'
import {
  CONTRACTOR_DOCUMENT_TYPES, openAttachment, uploadAttachment, useAgreement, useAgreements, useContractor, useContractorAction, useContractorInvoice,
  useContractorInvoices, useContractorJobs, useContractors, useUnbilledJobs,
  type AgreementSummary, type ContractorDetail, type ContractorInvoiceSummary, type ContractorSummary,
} from './api'

const TABS = [{ id: 'register', label: 'Register' }, { id: 'agreements', label: 'Agreements' }, { id: 'jobs', label: 'Jobs' }, { id: 'invoices', label: 'Invoices' }] as const
type Tab = (typeof TABS)[number]['id']

const STATUS_TONES: Record<ContractorSummary['status'], BadgeTone> = { Approved: 'success', Pending: 'warning', Suspended: 'danger' }
const DOC_TONES: Record<'Valid' | 'Expiring' | 'Expired', BadgeTone> = { Valid: 'success', Expiring: 'warning', Expired: 'danger' }
const AGREEMENT_TONES: Record<AgreementSummary['status'], BadgeTone> = { Active: 'success', Expired: 'danger', Cancelled: 'neutral' }
const INVOICE_TONES: Record<ContractorInvoiceSummary['status'], BadgeTone> = { Received: 'info', Matched: 'success', Query: 'warning', Approved: 'success', Rejected: 'danger' }

const tableShell = 'overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm'
const th = 'px-4 py-2'

async function attempt(fn: () => Promise<unknown>, message: string) {
  try {
    await fn()
    toast.success(message)
    return true
  } catch {
    // The API client shows the error.
    return false
  }
}

/** Contractors: the register and their documents, maintenance agreements, the jobs given to them and their invoices. */
export function ContractorsPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'register') as Tab
  return (
    <div className="space-y-4">
      <PageHeader title="Contractors" subtitle="Who may work in the factory, the agreements they work under, the jobs given to them, and their invoices matched to the work done." />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'register' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'register' ? <RegisterTab /> : tab === 'agreements' ? <AgreementsTab /> : tab === 'jobs' ? <JobsTab /> : <InvoicesTab />}
    </div>
  )
}

/* ── Register ────────────────────────────────────────────────────────────── */

function RegisterTab() {
  const contractors = useContractors(true)
  const canManage = useAnyPermission('work-orders.assign', 'settings.manage')
  const [open, setOpen] = useState<string | 'new' | null>(null)
  if (contractors.isLoading) return <Spinner />
  const rows = contractors.data ?? []
  return (
    <div className="space-y-4">
      {canManage ? <div className="flex justify-end"><button type="button" className={primaryButton} onClick={() => setOpen('new')}><Plus className="h-4 w-4" />Add contractor</button></div> : null}
      {rows.length === 0 ? <EmptyState icon={HardHat} title="No contractors" description="Register the firms that work in the factory, with their insurance and tax compliance." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Contractor</th><th className={th}>Does</th><th className={th}>Status</th><th className={th}>Documents</th><th className={`${th} text-right`}>Agreements</th><th className={`${th} text-right`}>Open jobs</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((c) => (
                <tr key={c.id} className={cn('cursor-pointer hover:bg-slate-50', !c.isActive && 'opacity-60')} onClick={() => setOpen(c.id)}>
                  <td className="px-4 py-2"><span className="font-medium text-slate-900">{c.name}</span><span className="block text-xs text-slate-500">{c.contactPerson ?? ''}{c.phone ? ` · ${c.phone}` : ''}</span></td>
                  <td className="px-4 py-2 text-slate-600">{c.services ?? '—'}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={STATUS_TONES[c.status]}>{c.status}</Badge>{!c.canTakeWork ? <span className="block text-xs text-rose-700">Can't take work</span> : null}</td>
                  <td className="px-4 py-2 text-xs">{c.documentsExpired ? <span className="text-rose-700">{c.documentsExpired} expired </span> : null}{c.documentsExpiring ? <span className="text-amber-700">{c.documentsExpiring} expiring</span> : null}{!c.documentsExpired && !c.documentsExpiring ? <span className="text-slate-500">{c.blockers.length ? 'Missing' : 'Current'}</span> : null}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{c.activeAgreements}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{c.openJobs}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <ContractorDrawer id={open} onClose={() => setOpen(null)} onCreated={(id) => setOpen(id)} />
    </div>
  )
}

const emptyContractor = { name: '', services: '', contactPerson: '', phone: '', email: '', registrationNumber: '', taxPin: '', status: 'Approved', supplierId: '', notes: '', isActive: true }

function ContractorDrawer({ id, onClose, onCreated }: { id: string | 'new' | null; onClose: () => void; onCreated: (id: string) => void }) {
  const canManage = useAnyPermission('work-orders.assign', 'settings.manage')
  const detail = useContractor(id && id !== 'new' ? id : null)
  const suppliers = useSuppliers()
  const action = useContractorAction()
  const links = useFactoryLinks()
  const [form, setForm] = useState(emptyContractor)
  const [doc, setDoc] = useState({ documentType: 'Insurance', reference: '', issuedOn: '', expiresOn: '' })
  const [file, setFile] = useState<File | null>(null)
  const d = detail.data

  useEffect(() => {
    if (id === 'new') setForm(emptyContractor)
    else if (d) setForm({ name: d.name, services: d.services ?? '', contactPerson: d.contactPerson ?? '', phone: d.phone ?? '', email: d.email ?? '', registrationNumber: d.registrationNumber ?? '', taxPin: d.taxPin ?? '', status: d.status, supplierId: d.supplierId ?? '', notes: d.notes ?? '', isActive: d.isActive })
  }, [id, d])

  const save = async () => {
    const body = Object.fromEntries(Object.entries(form).map(([k, v]) => [k, v === '' ? null : v]))
    try {
      const saved = (await action.mutateAsync(id === 'new' ? { path: '', body } : { method: 'put', path: `/${id}`, body })) as ContractorDetail
      toast.success('Saved')
      if (id === 'new') onCreated(saved.id)
    } catch {
      // The API client shows the error.
    }
  }

  const addDocument = async () => {
    if (!d) return
    try {
      const attachmentId = file ? await uploadAttachment('Contractor', d.id, file) : null
      await action.mutateAsync({ path: `/${d.id}/documents`, body: { documentType: doc.documentType, reference: doc.reference || null, issuedOn: doc.issuedOn || null, expiresOn: doc.expiresOn || null, attachmentId } })
      toast.success('Document added')
      setDoc({ documentType: 'Insurance', reference: '', issuedOn: '', expiresOn: '' })
      setFile(null)
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={id !== null} title={id === 'new' ? 'New contractor' : d?.name ?? ''} onClose={onClose} footer={canManage ? (
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Close</button><button type="button" className={primaryButton} disabled={!form.name || action.isPending} onClick={() => void save()}>Save</button></div>
    ) : undefined}>
      <div className="space-y-4">
        {d && d.blockers.length > 0 ? <ul className="space-y-1 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{d.blockers.map((b) => <li key={b}>{b}</li>)}</ul> : null}
        <fieldset disabled={!canManage} className="space-y-3">
          <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="What they do"><input className={inputClass} value={form.services} placeholder="Boiler servicing, rewinds, re-cutting…" onChange={(e) => setForm({ ...form, services: e.target.value })} /></Field>
          <div className="grid grid-cols-2 gap-2">
            <Field label="Contact"><input className={inputClass} value={form.contactPerson} onChange={(e) => setForm({ ...form, contactPerson: e.target.value })} /></Field>
            <Field label="Phone"><input className={inputClass} value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></Field>
            <Field label="Email"><input className={inputClass} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
            <Field label="Status"><select className={inputClass} value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}><option>Approved</option><option>Pending</option><option>Suspended</option></select></Field>
            <Field label="Registration no."><input className={inputClass} value={form.registrationNumber} onChange={(e) => setForm({ ...form, registrationNumber: e.target.value })} /></Field>
            <Field label="Tax PIN"><input className={inputClass} value={form.taxPin} onChange={(e) => setForm({ ...form, taxPin: e.target.value })} /></Field>
          </div>
          <Field label="Supplier record" hint="Used to raise purchase orders to them"><select className={inputClass} value={form.supplierId} onChange={(e) => setForm({ ...form, supplierId: e.target.value })}><option value="">None</option>{(suppliers.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}</select></Field>
          <Field label="Notes"><textarea className={textareaClass} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field>
          {id !== 'new' ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
        </fieldset>

        {d ? (
          <section className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Documents</h3>
            {d.documents.length === 0 ? <p className="text-sm text-slate-500">None yet. Insurance and tax compliance are needed before they can take work.</p> : (
              <ul className="space-y-1.5 text-sm">
                {d.documents.map((x) => (
                  <li key={x.id} className="flex items-center justify-between gap-2">
                    <span>{x.documentType}{x.required ? <span className="text-xs text-slate-500"> (required)</span> : null}{x.reference ? <span className="text-xs text-slate-500"> · {x.reference}</span> : null}
                      {x.attachmentId ? <button type="button" className="ml-1 text-xs text-[var(--app-primary)] underline" onClick={() => void openAttachment(x.attachmentId!)}>file</button> : null}</span>
                    <span className="flex items-center gap-2 text-xs">{x.expiresOn ? `to ${formatDate(x.expiresOn)}` : 'no expiry'} <Badge size="sm" tone={DOC_TONES[x.status]}>{x.status}</Badge>
                      {canManage ? <button type="button" aria-label={`Remove ${x.documentType}`} className="text-slate-400 hover:text-rose-600" onClick={() => void attempt(() => action.mutateAsync({ method: 'delete', path: `/documents/${x.id}` }), 'Removed')}><X className="h-4 w-4" /></button> : null}</span>
                  </li>
                ))}
              </ul>
            )}
            {canManage ? (
              <div className="mt-3 grid grid-cols-2 gap-2 rounded-lg bg-slate-50 p-3">
                <Field label="Type"><select className={inputClass} value={doc.documentType} onChange={(e) => setDoc({ ...doc, documentType: e.target.value })}>{CONTRACTOR_DOCUMENT_TYPES.map((t) => <option key={t}>{t}</option>)}</select></Field>
                <Field label="Reference"><input className={inputClass} value={doc.reference} onChange={(e) => setDoc({ ...doc, reference: e.target.value })} /></Field>
                <Field label="Issued"><input type="date" className={inputClass} value={doc.issuedOn} onChange={(e) => setDoc({ ...doc, issuedOn: e.target.value })} /></Field>
                <Field label="Expires"><input type="date" className={inputClass} value={doc.expiresOn} onChange={(e) => setDoc({ ...doc, expiresOn: e.target.value })} /></Field>
                <Field label="Copy" className="col-span-2"><input type="file" accept=".pdf,image/*" className="text-sm" onChange={(e) => setFile(e.target.files?.[0] ?? null)} /></Field>
                <button type="button" className={cn(secondaryButton, 'col-span-2')} onClick={() => void addDocument()}>Add document</button>
              </div>
            ) : null}
          </section>
        ) : null}

        {d && d.jobs.length > 0 ? (
          <section className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Recent jobs</h3>
            <ul className="space-y-1 text-sm">{d.jobs.slice(0, 10).map((j) => <li key={j.id} className="flex justify-between gap-2"><Link className="text-[var(--app-primary)] underline" to={links.job(j.id)}>{j.workOrderNumber} {j.title}</Link><Badge size="sm">{j.status}</Badge></li>)}</ul>
          </section>
        ) : null}
      </div>
    </FormDrawer>
  )
}

/* ── Agreements ──────────────────────────────────────────────────────────── */

function AgreementsTab() {
  const [status, setStatus] = useState('Active')
  const agreements = useAgreements(status)
  const canManage = useAnyPermission('work-orders.assign', 'settings.manage')
  const action = useContractorAction()
  const [open, setOpen] = useState<string | 'new' | null>(null)
  const [{ today, soon }] = useState(() => ({ today: new Date().toISOString().slice(0, 10), soon: new Date(Date.now() + 60 * 864e5).toISOString().slice(0, 10) }))
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Show" className="w-40"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="Active">In force</option><option value="Expired">Expired</option><option value="Cancelled">Cancelled</option><option value="">All</option></select></Field>
        <div className="ml-auto flex gap-2">
          {canManage ? <button type="button" className={secondaryButton} onClick={() => void attempt(() => action.mutateAsync({ path: '/run' }), 'Alerts sent and due visits raised')}>Raise due visits now</button> : null}
          {canManage ? <button type="button" className={primaryButton} onClick={() => setOpen('new')}><Plus className="h-4 w-4" />New agreement</button> : null}
        </div>
      </div>
      {agreements.isLoading ? <Spinner /> : (agreements.data ?? []).length === 0 ? <EmptyState icon={FileText} title="No agreements" description="Record annual maintenance contracts: scope, SLA, visit schedule and renewal." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[860px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Agreement</th><th className={th}>Contractor</th><th className={th}>Term</th><th className={th}>Visits</th><th className={`${th} text-right`}>Value</th><th className={`${th} text-right`}>Assets</th><th className={th}>Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(agreements.data ?? []).map((a) => {
                const ending = a.status === 'Active' && a.endDate.slice(0, 10) <= soon
                return (
                  <tr key={a.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(a.id)}>
                    <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{a.agreementNumber}</span> <span className="font-medium text-slate-900">{a.title}</span>{a.factoryName ? <span className="block text-xs text-slate-500">{a.factoryName}</span> : null}</td>
                    <td className="px-4 py-2">{a.contractorName}</td>
                    <td className={cn('px-4 py-2 text-xs', ending ? 'font-medium text-amber-700' : 'text-slate-600')}>{formatDate(a.startDate)} – {formatDate(a.endDate)}{ending ? ' · renew soon' : ''}</td>
                    <td className="px-4 py-2 text-xs text-slate-600">{a.visitEveryMonths ? `Every ${a.visitEveryMonths} mo${a.nextVisitDue ? `, next ${formatDate(a.nextVisitDue)}` : ''}` : 'Call-out'}{a.nextVisitDue && a.nextVisitDue.slice(0, 10) < today ? <span className="text-rose-700"> (due)</span> : null}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{a.value != null ? `${a.currency} ${formatNumber(a.value)}` : '—'}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{a.assets}</td>
                    <td className="px-4 py-2"><Badge size="sm" tone={AGREEMENT_TONES[a.status]}>{a.status}</Badge></td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      <AgreementDrawer id={open} onClose={() => setOpen(null)} />
    </div>
  )
}

const emptyAgreement = { contractorId: '', title: '', scope: '', factoryId: '', startDate: '', endDate: '', value: '', currency: '', responseHours: '', repairHours: '', visitEveryMonths: '', firstVisitDue: '', renewalNoticeDays: '60' }

function AgreementDrawer({ id, onClose }: { id: string | 'new' | null; onClose: () => void }) {
  const canManage = useAnyPermission('work-orders.assign', 'settings.manage')
  const detail = useAgreement(id && id !== 'new' ? id : null)
  const contractors = useContractors()
  const nodes = useOrgNodes()
  const factories = useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory'), [nodes.data])
  const action = useContractorAction()
  const links = useFactoryLinks()
  const [form, setForm] = useState(emptyAgreement)
  const [assets, setAssets] = useState<PickedAsset[]>([])
  const [renewTo, setRenewTo] = useState('')
  const d = detail.data

  useEffect(() => {
    if (id === 'new') { setForm(emptyAgreement); setAssets([]) }
    else if (d) {
      const a = d.agreement
      setForm({ contractorId: a.contractorId, title: a.title, scope: d.scope ?? '', factoryId: a.factoryId ?? '', startDate: a.startDate.slice(0, 10), endDate: a.endDate.slice(0, 10), value: a.value?.toString() ?? '', currency: a.currency,
        responseHours: d.responseHours?.toString() ?? '', repairHours: d.repairHours?.toString() ?? '', visitEveryMonths: a.visitEveryMonths?.toString() ?? '', firstVisitDue: '', renewalNoticeDays: String(d.renewalNoticeDays) })
      setAssets(d.assets.map((x) => ({ id: x.assetId, code: x.assetCode, name: x.assetName, categoryId: null })))
    }
  }, [id, d])

  const num = (v: string) => (v ? Number(v) : null)
  const save = async () => {
    const body = { contractorId: form.contractorId, title: form.title, scope: form.scope || null, factoryId: form.factoryId || null, startDate: form.startDate, endDate: form.endDate, value: num(form.value), currency: form.currency || null,
      responseHours: num(form.responseHours), repairHours: num(form.repairHours), visitEveryMonths: num(form.visitEveryMonths), firstVisitDue: form.firstVisitDue || null, renewalNoticeDays: num(form.renewalNoticeDays), assetIds: assets.map((a) => a.id) }
    if (await attempt(() => action.mutateAsync(id === 'new' ? { path: '/agreements', body } : { method: 'put', path: `/agreements/${id}`, body }), 'Agreement saved') && id === 'new') onClose()
  }

  return (
    <FormDrawer open={id !== null} title={id === 'new' ? 'New maintenance agreement' : d ? `${d.agreement.agreementNumber} ${d.agreement.title}` : ''} onClose={onClose} footer={canManage ? (
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Close</button>
        <button type="button" className={primaryButton} disabled={!form.contractorId || !form.title || !form.startDate || !form.endDate || action.isPending} onClick={() => void save()}>Save</button></div>
    ) : undefined}>
      <div className="space-y-4">
        {d ? <p className="text-sm text-slate-600"><Badge size="sm" tone={AGREEMENT_TONES[d.agreement.status]}>{d.agreement.status}</Badge> {d.jobsFinished} job{d.jobsFinished === 1 ? '' : 's'} done under it, {d.jobsWithinSla} within the agreed times.</p> : null}
        <fieldset disabled={!canManage} className="space-y-3">
          <Field label="Contractor" required><select className={inputClass} value={form.contractorId} onChange={(e) => setForm({ ...form, contractorId: e.target.value })}><option value="">Choose</option>{(contractors.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
          <Field label="Title" required><input className={inputClass} value={form.title} placeholder="Boiler AMC 2027" onChange={(e) => setForm({ ...form, title: e.target.value })} /></Field>
          <Field label="Scope"><textarea className={textareaClass} value={form.scope} onChange={(e) => setForm({ ...form, scope: e.target.value })} /></Field>
          <div className="grid grid-cols-2 gap-2">
            <Field label="Starts" required><input type="date" className={inputClass} value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></Field>
            <Field label="Ends" required><input type="date" className={inputClass} value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></Field>
            <Field label="Value"><input type="number" min={0} className={inputClass} value={form.value} onChange={(e) => setForm({ ...form, value: e.target.value })} /></Field>
            <Field label="Currency"><input className={inputClass} maxLength={3} placeholder="KES" value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></Field>
            <Field label="Respond within (h)"><input type="number" min={0} className={inputClass} value={form.responseHours} onChange={(e) => setForm({ ...form, responseHours: e.target.value })} /></Field>
            <Field label="Fix within (h)"><input type="number" min={0} className={inputClass} value={form.repairHours} onChange={(e) => setForm({ ...form, repairHours: e.target.value })} /></Field>
            <Field label="Visit every (months)" hint="Blank for call-out only"><input type="number" min={1} max={24} className={inputClass} value={form.visitEveryMonths} onChange={(e) => setForm({ ...form, visitEveryMonths: e.target.value })} /></Field>
            <Field label={d?.agreement.nextVisitDue ? `Next visit (now ${formatDate(d.agreement.nextVisitDue)})` : 'First visit'}><input type="date" className={inputClass} value={form.firstVisitDue} onChange={(e) => setForm({ ...form, firstVisitDue: e.target.value })} /></Field>
            <Field label="Factory"><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}><option value="">Any</option>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
            <Field label="Renewal notice (days)"><input type="number" min={1} className={inputClass} value={form.renewalNoticeDays} onChange={(e) => setForm({ ...form, renewalNoticeDays: e.target.value })} /></Field>
          </div>
          <Field label="Assets covered">
            <div className="space-y-2">
              {assets.length ? <ul className="flex flex-wrap gap-1">{assets.map((a) => <li key={a.id}><span className="inline-flex items-center gap-1 rounded bg-slate-100 px-2 py-0.5 text-xs">{a.code}{canManage ? <button type="button" aria-label={`Remove ${a.code}`} onClick={() => setAssets(assets.filter((x) => x.id !== a.id))}><X className="h-3 w-3" /></button> : null}</span></li>)}</ul> : null}
              {canManage ? <AssetPicker value={null} onChange={(a) => { if (a && !assets.some((x) => x.id === a.id)) setAssets([...assets, a]) }} /> : null}
            </div>
          </Field>
        </fieldset>
        {d && canManage && d.agreement.status !== 'Cancelled' ? (
          <section className="flex flex-wrap items-end gap-2 border-t border-slate-100 pt-3">
            <Field label="Renew to" className="w-44"><input type="date" className={inputClass} value={renewTo} onChange={(e) => setRenewTo(e.target.value)} /></Field>
            <button type="button" className={secondaryButton} disabled={!renewTo} onClick={() => void attempt(() => action.mutateAsync({ path: `/agreements/${d.agreement.id}/renew`, body: { endDate: renewTo } }), 'Renewed')}>Renew</button>
            <button type="button" className={cn(secondaryButton, 'ml-auto')} onClick={() => void attempt(() => action.mutateAsync({ path: `/agreements/${d.agreement.id}/cancel` }), 'Cancelled')}>Cancel agreement</button>
          </section>
        ) : null}
        {d && d.jobs.length > 0 ? (
          <section className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Jobs under it</h3>
            <ul className="space-y-1 text-sm">{d.jobs.slice(0, 15).map((j) => <li key={j.id} className="flex justify-between gap-2"><Link className="text-[var(--app-primary)] underline" to={links.job(j.id)}>{j.workOrderNumber} {j.title}</Link><span className="text-xs">{j.repairBreached ? <Badge size="sm" tone="danger">Late</Badge> : null} <Badge size="sm">{j.status}</Badge></span></li>)}</ul>
          </section>
        ) : null}
      </div>
    </FormDrawer>
  )
}

/* ── Jobs ────────────────────────────────────────────────────────────────── */

function JobsTab() {
  const [openOnly, setOpenOnly] = useState(true)
  const jobs = useContractorJobs(openOnly)
  const links = useFactoryLinks()
  return (
    <div className="space-y-4">
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />Open jobs only</label>
      {jobs.isLoading ? <Spinner /> : (jobs.data ?? []).length === 0 ? <EmptyState icon={HardHat} title="No contractor jobs" description="Give a job to a contractor from the job page." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Job</th><th className={th}>Contractor</th><th className={th}>Asset</th><th className={th}>Fix by</th><th className={th}>Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(jobs.data ?? []).map((j) => (
                <tr key={j.id}>
                  <td className="px-4 py-2"><Link className="text-[var(--app-primary)] underline" to={links.job(j.id)}><span className="font-mono text-xs">{j.workOrderNumber}</span> {j.title}</Link></td>
                  <td className="px-4 py-2">{j.contractorName}{j.agreementNumber ? <span className="block text-xs text-slate-500">{j.agreementNumber}</span> : null}</td>
                  <td className="px-4 py-2 text-slate-600">{j.assetCode ?? '—'}</td>
                  <td className={cn('px-4 py-2 text-xs', j.repairBreached ? 'font-medium text-rose-700' : 'text-slate-600')}>{j.repairDeadline ? formatDate(j.repairDeadline) : j.dueDate ? formatDate(j.dueDate) : '—'}</td>
                  <td className="px-4 py-2"><Badge size="sm">{j.status}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

/* ── Invoices ────────────────────────────────────────────────────────────── */

function InvoicesTab() {
  const [status, setStatus] = useState('')
  const invoices = useContractorInvoices(status)
  const canRecord = usePermission('work-orders.assign')
  const [open, setOpen] = useState<string | null>(null)
  const [recording, setRecording] = useState(false)
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Show" className="w-40"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="">All</option><option value="Query">Query</option><option value="Matched">Matched</option><option value="Approved">Approved</option><option value="Rejected">Rejected</option></select></Field>
        {canRecord ? <button type="button" className={cn(primaryButton, 'ml-auto')} onClick={() => setRecording(true)}><Receipt className="h-4 w-4" />Record an invoice</button> : null}
      </div>
      {invoices.isLoading ? <Spinner /> : (invoices.data ?? []).length === 0 ? <EmptyState icon={Receipt} title="No contractor invoices" description="Record an invoice against the jobs it bills; it is matched to the work done." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Invoice</th><th className={th}>Contractor</th><th className={th}>Date</th><th className={`${th} text-right`}>Amount</th><th className={`${th} text-right`}>Lines</th><th className={th}>Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(invoices.data ?? []).map((i) => (
                <tr key={i.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(i.id)}>
                  <td className="px-4 py-2 font-mono text-xs">{i.invoiceNumber}</td>
                  <td className="px-4 py-2">{i.contractorName}</td>
                  <td className="px-4 py-2 text-slate-600">{formatDate(i.invoiceDate)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{i.currency} {formatNumber(i.amount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{i.lines}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={INVOICE_TONES[i.status]}>{i.status}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <InvoiceDrawer id={open} onClose={() => setOpen(null)} />
      <RecordInvoiceDrawer open={recording} onClose={() => setRecording(false)} onRecorded={(id) => { setRecording(false); setOpen(id) }} />
    </div>
  )
}

function RecordInvoiceDrawer({ open, onClose, onRecorded }: { open: boolean; onClose: () => void; onRecorded: (id: string) => void }) {
  const contractors = useContractors()
  const action = useContractorAction()
  const [contractorId, setContractorId] = useState('')
  const unbilled = useUnbilledJobs(contractorId || null)
  const [form, setForm] = useState({ invoiceNumber: '', invoiceDate: new Date().toISOString().slice(0, 10), currency: '', amount: '' })
  const [amounts, setAmounts] = useState<Record<string, string>>({})
  const [file, setFile] = useState<File | null>(null)
  const lines = Object.entries(amounts).filter(([, v]) => Number(v) > 0).map(([workOrderId, v]) => ({ workOrderId, amount: Number(v) }))
  const close = () => { setContractorId(''); setAmounts({}); setFile(null); setForm({ invoiceNumber: '', invoiceDate: new Date().toISOString().slice(0, 10), currency: '', amount: '' }); onClose() }
  const submit = async () => {
    try {
      const created = (await action.mutateAsync({ path: '/invoices', body: { contractorId, invoiceNumber: form.invoiceNumber, invoiceDate: form.invoiceDate, currency: form.currency || null, amount: form.amount ? Number(form.amount) : null, lines } })) as { invoice: { id: string; status: string } }
      if (file) await uploadAttachment('ContractorInvoice', created.invoice.id, file)
      toast.success(created.invoice.status === 'Matched' ? 'Recorded: matches the work done' : 'Recorded: needs checking')
      setAmounts({})
      onRecorded(created.invoice.id)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title="Record a contractor invoice" onClose={close} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={close}>Cancel</button><button type="button" className={primaryButton} disabled={!contractorId || !form.invoiceNumber || lines.length === 0 || action.isPending} onClick={() => void submit()}>Record and match</button></div>
    )}>
      <div className="space-y-3">
        <Field label="Contractor" required><select className={inputClass} value={contractorId} onChange={(e) => { setContractorId(e.target.value); setAmounts({}) }}><option value="">Choose</option>{(contractors.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
        <div className="grid grid-cols-2 gap-2">
          <Field label="Invoice number" required><input className={inputClass} value={form.invoiceNumber} onChange={(e) => setForm({ ...form, invoiceNumber: e.target.value })} /></Field>
          <Field label="Date" required><input type="date" className={inputClass} value={form.invoiceDate} onChange={(e) => setForm({ ...form, invoiceDate: e.target.value })} /></Field>
          <Field label="Invoice total" hint="Blank: the sum of the lines"><input type="number" min={0} className={inputClass} value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
          <Field label="Currency"><input className={inputClass} maxLength={3} placeholder="KES" value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></Field>
        </div>
        <Field label="Copy of the invoice"><input type="file" accept=".pdf,image/*" className="text-sm" onChange={(e) => setFile(e.target.files?.[0] ?? null)} /></Field>
        {contractorId ? (
          <section>
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Completed jobs not yet billed</h3>
            {(unbilled.data ?? []).length === 0 ? <p className="text-sm text-slate-500">None. Only jobs given to this contractor and completed can be billed.</p> : (
              <ul className="space-y-1.5">{(unbilled.data ?? []).map((j) => (
                <li key={j.id} className="flex items-center justify-between gap-2 text-sm"><span>{j.workOrderNumber} {j.title}<span className="block text-xs text-slate-500">{j.completedAt ? `Done ${formatDate(j.completedAt)}` : j.status}</span></span>
                  <input type="number" min={0} aria-label={`Amount for ${j.workOrderNumber}`} className={cn(inputClass, 'h-8 w-28 text-right')} placeholder="Amount" value={amounts[j.id] ?? ''} onChange={(e) => setAmounts({ ...amounts, [j.id]: e.target.value })} /></li>
              ))}</ul>
            )}
            <p className="mt-2 text-right text-sm">Lines: <span className="font-semibold tabular-nums">{formatNumber(lines.reduce((s, l) => s + l.amount, 0))}</span></p>
          </section>
        ) : null}
      </div>
    </FormDrawer>
  )
}

function InvoiceDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const detail = useContractorInvoice(id)
  const action = useContractorAction()
  const canApprove = usePermission('materials.approve')
  const canRecord = usePermission('work-orders.assign')
  const links = useFactoryLinks()
  const [note, setNote] = useState('')
  const d = detail.data
  const open = d && !['Approved', 'Rejected'].includes(d.invoice.status)
  const act = (verb: string, message: string, body?: unknown) => attempt(() => action.mutateAsync({ path: `/invoices/${id}/${verb}`, body }), message)
  return (
    <FormDrawer open={id !== null} title={d ? `${d.invoice.contractorName} · ${d.invoice.invoiceNumber}` : ''} onClose={onClose} footer={open ? (
      <div className="flex flex-wrap justify-end gap-2">
        {canRecord ? <button type="button" className={secondaryButton} onClick={() => void act('match', 'Checked again')}>Check again</button> : null}
        {canRecord ? <button type="button" className={secondaryButton} disabled={!note.trim()} onClick={() => void act('reject', 'Rejected', { reason: note })}>Reject</button> : null}
        {canApprove ? <button type="button" className={primaryButton} disabled={d?.invoice.status !== 'Matched' && !note.trim()} onClick={() => void act('approve', 'Approved for payment', { reason: note || null })}>Approve</button> : null}
      </div>
    ) : undefined}>
      {!d ? <Spinner /> : (
        <div className="space-y-4">
          <p className="text-sm text-slate-600"><Badge size="sm" tone={INVOICE_TONES[d.invoice.status]}>{d.invoice.status}</Badge> {d.invoice.currency} {formatNumber(d.invoice.amount)} dated {formatDate(d.invoice.invoiceDate)}.{d.invoice.decidedByName ? ` Decided by ${d.invoice.decidedByName}.` : ''}</p>
          {d.attachmentId ? <button type="button" className="text-sm text-[var(--app-primary)] underline" onClick={() => void openAttachment(d.attachmentId!)}>Open the invoice</button> : null}
          {d.issues.length > 0 ? <ul className="space-y-1 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{d.issues.map((x) => <li key={x}>{x}</li>)}</ul> : null}
          {d.decisionNote ? <p className="rounded-lg bg-slate-50 p-2 text-sm">{d.decisionNote}</p> : null}
          <ul className="divide-y divide-slate-100 text-sm">
            {d.lines.map((l) => <li key={l.id} className="flex justify-between gap-2 py-1.5"><span>{l.workOrderId ? <Link className="text-[var(--app-primary)] underline" to={links.job(l.workOrderId)}>{l.workOrderNumber}</Link> : null} {l.description}{l.workOrderStatus ? <span className="block text-xs text-slate-500">{l.workOrderStatus}</span> : null}</span><span className="tabular-nums">{formatNumber(l.amount)}</span></li>)}
          </ul>
          {open ? <Field label={d.invoice.status === 'Matched' ? 'Note' : 'Reason (to reject, or to approve despite the issues)'}><input className={inputClass} value={note} onChange={(e) => setNote(e.target.value)} /></Field> : null}
        </div>
      )}
    </FormDrawer>
  )
}
