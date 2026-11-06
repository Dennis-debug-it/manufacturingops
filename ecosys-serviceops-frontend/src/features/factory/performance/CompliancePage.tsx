import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, FileDown, Paperclip, ShieldCheck, XCircle } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission, usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { api } from '../../../shared/api/apiClient'
import { useOrgNodes } from '../api'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { AssetPicker, type PickedAsset } from '../planning/AssetPicker'
import { useContractors } from '../stores/api'
import {
  AUDIT_KINDS, WASTE_TYPES, downloadFile, fwPath, periodDates, useAudits, useConsents, useDataSubjects, useEvidence, useEvidenceSources, useFactoryAction,
  useFoodSafety, useGlassRegister, useNonConformities, useStandardTemplates, useStandards, useWaste,
  type NonConformity, type NonConformityStatus, type Requirement, type Standard,
} from './api'

const TABS = [
  { id: 'standards', label: 'Standards and evidence' }, { id: 'findings', label: 'Findings' }, { id: 'audits', label: 'Audit calendar' },
  { id: 'registers', label: 'Registers' }, { id: 'privacy', label: 'Data protection' },
] as const
type Tab = (typeof TABS)[number]['id']

const NC_TONES: Record<NonConformityStatus, BadgeTone> = { Open: 'danger', InProgress: 'warning', Resolved: 'info', Closed: 'success' }
const NC_LABELS: Record<NonConformityStatus, string> = { Open: 'Open', InProgress: 'In progress', Resolved: 'Resolved, to verify', Closed: 'Closed' }

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

function useFactories() {
  const nodes = useOrgNodes()
  return useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory' && x.isActive), [nodes.data])
}

/** Compliance: standards mapped to evidence and one-click evidence packs, findings, the audit calendar, the waste and food safety registers, and data protection. */
export function CompliancePage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'standards') as Tab
  const isAdmin = usePermission('settings.manage')
  return (
    <div className="space-y-4">
      <PageHeader title="Compliance and audits" subtitle="Each standard is a list of requirements mapped to the records that prove them. Pick a factory and period to see the evidence and the gaps, then download the pack for the auditor." />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.filter((t) => t.id !== 'privacy' || isAdmin).map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'standards' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'standards' ? <StandardsTab /> : tab === 'findings' ? <FindingsTab /> : tab === 'audits' ? <AuditsTab /> : tab === 'registers' ? <RegistersTab /> : <PrivacyTab />}
    </div>
  )
}

/* ── Standards and evidence ──────────────────────────────────────────────── */

function StandardsTab() {
  const canManage = useAnyPermission('settings.manage', 'work-orders.assign')
  const standards = useStandards()
  const templates = useStandardTemplates()
  const factories = useFactories()
  const action = useFactoryAction()
  const [standardId, setStandardId] = useState('')
  const [factoryId, setFactoryId] = useState('')
  const [range, setRange] = useState(() => periodDates('last12', { from: '', to: '' }))
  const [editing, setEditing] = useState<Requirement | 'new' | null>(null)
  const standard = standards.data?.find((x) => x.id === standardId) ?? standards.data?.[0]
  const factory = factoryId || factories[0]?.id || ''
  const evidence = useEvidence(standard?.id ?? null, factory, range.from, range.to)
  const installed = new Set((standards.data ?? []).map((s) => s.templateKey))

  if (standards.isLoading) return <Spinner />
  return (
    <div className="space-y-4">
      {canManage ? (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="text-slate-600">Add a standard:</span>
          {(templates.data ?? []).filter((t) => !installed.has(t.key)).map((t) => (
            <button key={t.key} type="button" className={cn(secondaryButton, 'h-8 px-3 text-xs')} title={t.description} onClick={() => void attempt(() => action.mutateAsync({ path: `${fwPath}/standards/install`, body: { templateKey: t.key } }), `${t.name} added`)}>{t.code}</button>
          ))}
        </div>
      ) : null}
      {(standards.data ?? []).length === 0 ? <EmptyState icon={ShieldCheck} title="No standards yet" description="Add the standards the factory is audited against, then see which records prove each requirement." /> : (
        <>
          <div className="flex flex-wrap items-end gap-2">
            <Field label="Standard" className="w-80"><select className={inputClass} value={standard?.id ?? ''} onChange={(e) => setStandardId(e.target.value)}>{(standards.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
            <Field label="Factory" className="w-48"><select className={inputClass} value={factory} onChange={(e) => setFactoryId(e.target.value)}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
            <Field label="From" className="w-40"><input type="date" className={inputClass} value={range.from} onChange={(e) => setRange({ ...range, from: e.target.value })} /></Field>
            <Field label="To" className="w-40"><input type="date" className={inputClass} value={range.to} onChange={(e) => setRange({ ...range, to: e.target.value })} /></Field>
            <button type="button" className={cn(primaryButton, 'ml-auto')} disabled={!standard || !factory}
              onClick={() => void downloadFile(`${fwPath}/standards/${standard!.id}/evidence-pack`, { factoryId: factory, from: range.from, to: range.to }, 'evidence-pack.pdf').catch(() => undefined)}><FileDown className="h-4 w-4" />Evidence pack (PDF)</button>
          </div>
          {evidence.isLoading ? <Spinner label="Gathering the evidence" /> : evidence.data ? (
            <div className="space-y-3">
              <p className="text-sm text-slate-700"><span className="font-semibold">{evidence.data.covered} of {evidence.data.requirements.length}</span> requirements have evidence for {evidence.data.factoryName} in the period.</p>
              {evidence.data.requirements.map((r) => {
                const req = standard?.requirements.find((x) => x.id === r.requirementId)
                return (
                  <details key={r.requirementId} className="rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
                    <summary className="flex cursor-pointer items-center justify-between gap-2 p-3 text-sm">
                      <span className="flex items-center gap-2">{r.hasEvidence ? <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600" /> : <XCircle className="h-4 w-4 shrink-0 text-rose-600" />}<span className="font-mono text-xs text-slate-500">{r.clause}</span> <span className="font-medium text-slate-900">{r.title}</span></span>
                      <span className="flex shrink-0 gap-1">{r.openNonConformities ? <Badge size="sm" tone="danger">{r.openNonConformities} open finding{r.openNonConformities === 1 ? '' : 's'}</Badge> : null}{!r.hasEvidence ? <Badge size="sm" tone="danger">Gap</Badge> : null}</span>
                    </summary>
                    <div className="space-y-3 border-t border-slate-100 p-3">
                      {r.guidance ? <p className="text-xs text-slate-500">{r.guidance}</p> : null}
                      {r.sections.map((s) => (
                        <div key={s.source}>
                          <p className="text-sm font-medium text-slate-800">{s.title}</p>
                          <p className="text-xs text-slate-600">{s.summary}</p>
                          {s.rows.length > 0 ? (
                            <div className="mt-1 max-h-64 overflow-auto">
                              <table className="w-full text-xs"><thead className="text-left text-slate-500"><tr>{s.headers.map((h) => <th key={h} className="py-1 pr-2">{h}</th>)}</tr></thead>
                                <tbody className="divide-y divide-slate-100">{s.rows.slice(0, 25).map((row, i) => <tr key={i}>{row.map((c, j) => <td key={j} className="py-1 pr-2">{c}</td>)}</tr>)}</tbody></table>
                              {s.rows.length > 25 ? <p className="text-xs text-slate-500">{s.count} in all; the PDF lists them.</p> : null}
                            </div>
                          ) : null}
                        </div>
                      ))}
                      <RequirementDocuments requirementId={r.requirementId} documents={r.documents} canAttach={canManage} />
                      {canManage && req ? <button type="button" className="text-xs text-[var(--app-primary)] underline" onClick={() => setEditing(req)}>Edit requirement</button> : null}
                    </div>
                  </details>
                )
              })}
              {canManage && standard ? <button type="button" className={secondaryButton} onClick={() => setEditing('new')}>Add a requirement</button> : null}
            </div>
          ) : null}
        </>
      )}
      {standard ? <RequirementDrawer standard={standard} requirement={editing} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}

function RequirementDocuments({ requirementId, documents, canAttach }: { requirementId: string; documents: string[]; canAttach: boolean }) {
  const queryClient = useQueryClient()
  const upload = async (file: File | undefined) => {
    if (!file) return
    const form = new FormData()
    form.append('entityType', 'StandardRequirement')
    form.append('entityId', requirementId)
    form.append('file', file)
    if (await attempt(() => api.post('/api/attachments/upload', form), 'Document attached')) void queryClient.invalidateQueries({ queryKey: ['factory', 'framework', 'evidence'] })
  }
  return (
    <div className="text-sm">
      {documents.length ? <p className="text-xs text-slate-600"><Paperclip className="mr-1 inline h-3 w-3" />{documents.join(', ')}</p> : null}
      {canAttach ? <label className="mt-1 inline-flex cursor-pointer items-center gap-1 text-xs text-[var(--app-primary)] underline"><Paperclip className="h-3 w-3" />Attach a document (policy, certificate, report)<input type="file" className="hidden" onChange={(e) => void upload(e.target.files?.[0])} /></label> : null}
    </div>
  )
}

function RequirementDrawer({ standard, requirement, onClose }: { standard: Standard; requirement: Requirement | 'new' | null; onClose: () => void }) {
  const sources = useEvidenceSources()
  const action = useFactoryAction()
  const [form, setForm] = useState({ clause: '', title: '', guidance: '', sources: [] as string[] })
  useEffect(() => {
    if (requirement === 'new') setForm({ clause: '', title: '', guidance: '', sources: ['documents'] })
    else if (requirement) setForm({ clause: requirement.clause, title: requirement.title, guidance: requirement.guidance ?? '', sources: requirement.evidenceSources.split(',').filter(Boolean) })
  }, [requirement])
  const save = async () => {
    const body = { clause: form.clause, title: form.title, guidance: form.guidance || null, evidenceSources: form.sources }
    if (await attempt(() => action.mutateAsync(requirement === 'new' ? { path: `${fwPath}/standards/${standard.id}/requirements`, body } : { method: 'put', path: `${fwPath}/requirements/${(requirement as Requirement).id}`, body }), 'Requirement saved')) onClose()
  }
  const remove = async () => {
    if (requirement && requirement !== 'new' && await attempt(() => action.mutateAsync({ method: 'delete', path: `${fwPath}/requirements/${requirement.id}` }), 'Requirement removed')) onClose()
  }
  return (
    <FormDrawer open={requirement !== null} title={requirement === 'new' ? `New requirement · ${standard.code}` : 'Edit requirement'} onClose={onClose} footer={(
      <div className="flex justify-between gap-2">
        {requirement !== 'new' ? <button type="button" className={secondaryButton} onClick={() => void remove()}>Remove</button> : <span />}
        <div className="flex gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={!form.clause || !form.title || form.sources.length === 0} onClick={() => void save()}>Save</button></div>
      </div>
    )}>
      <div className="space-y-3">
        <div className="grid grid-cols-[8rem_1fr] gap-2">
          <Field label="Clause" required><input className={inputClass} value={form.clause} onChange={(e) => setForm({ ...form, clause: e.target.value })} /></Field>
          <Field label="Requirement" required><input className={inputClass} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></Field>
        </div>
        <Field label="What the auditor looks for"><textarea className={textareaClass} value={form.guidance} onChange={(e) => setForm({ ...form, guidance: e.target.value })} /></Field>
        <Field label="Proved by">
          <div className="grid gap-1 sm:grid-cols-2">
            {Object.entries(sources.data ?? {}).map(([key, label]) => (
              <label key={key} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.sources.includes(key)} onChange={(e) => setForm({ ...form, sources: e.target.checked ? [...form.sources, key] : form.sources.filter((x) => x !== key) })} />{label}</label>
            ))}
          </div>
        </Field>
      </div>
    </FormDrawer>
  )
}

/* ── Findings ────────────────────────────────────────────────────────────── */

function FindingsTab() {
  const [status, setStatus] = useState('open')
  const ncs = useNonConformities(status)
  const canManage = useAnyPermission('settings.manage', 'work-orders.assign')
  const [open, setOpen] = useState<NonConformity | 'new' | null>(null)
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Show" className="w-40"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="open">Open</option><option value="Closed">Closed</option><option value="">All</option></select></Field>
        {canManage ? <button type="button" className={cn(primaryButton, 'ml-auto')} onClick={() => setOpen('new')}>Log a finding</button> : null}
      </div>
      {ncs.isLoading ? <Spinner /> : (ncs.data ?? []).length === 0 ? <EmptyState icon={ShieldCheck} title="No findings" description="Log audit findings and internal non-conformities as corrective actions with an owner and due date." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">No.</th><th className="px-4 py-2">Finding</th><th className="px-4 py-2">Standard</th><th className="px-4 py-2">Owner</th><th className="px-4 py-2">Due</th><th className="px-4 py-2">Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(ncs.data ?? []).map((n) => {
                const late = n.dueDate && n.status !== 'Closed' && n.dueDate.slice(0, 10) < new Date().toISOString().slice(0, 10)
                return (
                  <tr key={n.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(n)}>
                    <td className="px-4 py-2 font-mono text-xs">{n.number}<Badge size="sm" tone={n.severity === 'Major' ? 'danger' : n.severity === 'Minor' ? 'warning' : 'neutral'}>{n.severity}</Badge></td>
                    <td className="px-4 py-2">{n.finding}<span className="block text-xs text-slate-500">{n.factoryName} · {n.source}</span></td>
                    <td className="px-4 py-2 text-slate-600">{n.standardName ?? '—'}{n.requirementClause ? <span className="block text-xs">{n.requirementClause}</span> : null}</td>
                    <td className="px-4 py-2 text-slate-600">{n.ownerName ?? '—'}</td>
                    <td className={cn('px-4 py-2', late ? 'font-medium text-rose-700' : 'text-slate-600')}>{n.dueDate ? formatDate(n.dueDate) : '—'}</td>
                    <td className="px-4 py-2"><Badge size="sm" tone={NC_TONES[n.status]}>{NC_LABELS[n.status]}</Badge></td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      <FindingDrawer finding={open} onClose={() => setOpen(null)} />
    </div>
  )
}

function FindingDrawer({ finding, onClose }: { finding: NonConformity | 'new' | null; onClose: () => void }) {
  const canManage = useAnyPermission('settings.manage', 'work-orders.assign')
  const canWork = useAnyPermission('work-orders.assign', 'work-orders.complete')
  const factories = useFactories()
  const standards = useStandards()
  const audits = useAudits()
  const action = useFactoryAction()
  const links = useFactoryLinks()
  const users = useUsersForOwner()
  const empty = { factoryId: '', requirementId: '', auditEventId: '', source: 'Audit', severity: 'Minor', finding: '', rootCause: '', correctiveAction: '', ownerUserId: '', dueDate: '' }
  const [form, setForm] = useState(empty)
  const [evidence, setEvidence] = useState('')
  const [asset, setAsset] = useState<PickedAsset | null>(null)
  useEffect(() => {
    if (finding === 'new') setForm({ ...empty, factoryId: factories[0]?.id ?? '' })
    else if (finding) setForm({ factoryId: finding.factoryId, requirementId: finding.requirementId ?? '', auditEventId: finding.auditEventId ?? '', source: finding.source, severity: finding.severity, finding: finding.finding,
      rootCause: finding.rootCause ?? '', correctiveAction: finding.correctiveAction ?? '', ownerUserId: finding.ownerUserId ?? '', dueDate: finding.dueDate?.slice(0, 10) ?? '' })
    setEvidence('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [finding, factories])
  const isNew = finding === 'new'
  const nc = isNew ? null : finding
  const body = () => ({ factoryId: form.factoryId, requirementId: form.requirementId || null, auditEventId: form.auditEventId || null, source: form.source, severity: form.severity, finding: form.finding,
    rootCause: form.rootCause || null, correctiveAction: form.correctiveAction || null, ownerUserId: form.ownerUserId || null, dueDate: form.dueDate || null })
  const save = async () => {
    if (await attempt(() => action.mutateAsync(isNew ? { path: `${fwPath}/non-conformities`, body: body() } : { method: 'put', path: `${fwPath}/non-conformities/${nc!.id}`, body: body() }), 'Saved') && isNew) onClose()
  }
  const act = async (verb: string, message: string, extra?: unknown) => { if (await attempt(() => action.mutateAsync({ path: `${fwPath}/non-conformities/${nc!.id}/${verb}`, body: extra ?? {} }), message)) onClose() }
  const requirements = (standards.data ?? []).flatMap((s) => s.requirements.map((r) => ({ id: r.id, label: `${s.code} ${r.clause} · ${r.title}` })))
  const editable = isNew || (nc && nc.status !== 'Closed')
  return (
    <FormDrawer open={finding !== null} title={isNew ? 'Log a finding' : `${nc?.number} · ${nc ? NC_LABELS[nc.status] : ''}`} onClose={onClose} footer={editable && (canManage || canWork) ? (
      <div className="flex flex-wrap justify-end gap-2">
        {nc?.status === 'Open' && canWork ? <button type="button" className={secondaryButton} onClick={() => void act('start', 'Started')}>Start</button> : null}
        {nc && !nc.workOrderId && canManage ? <button type="button" className={secondaryButton} onClick={() => void act('job', 'Corrective job raised', { assetId: asset?.id ?? null })}>Raise a corrective job</button> : null}
        {nc?.status === 'Resolved' && canManage ? <button type="button" className={primaryButton} onClick={() => void act('close', 'Closed')}>Verify and close</button> : null}
        <button type="button" className={nc?.status === 'Resolved' ? secondaryButton : primaryButton} disabled={!form.finding || !form.factoryId} onClick={() => void save()}>Save</button>
      </div>
    ) : nc?.status === 'Closed' && canManage ? <div className="flex justify-end"><button type="button" className={secondaryButton} onClick={() => void act('reopen', 'Reopened')}>Reopen</button></div> : undefined}>
      <div className="space-y-3">
        {nc?.workOrderId ? <p className="text-sm">Corrective job <Link className="text-[var(--app-primary)] underline" to={links.job(nc.workOrderId)}>{nc.workOrderNumber}</Link></p> : null}
        {nc?.closureEvidence ? <p className="rounded-lg bg-emerald-50 p-2 text-sm text-emerald-900">Evidence: {nc.closureEvidence}{nc.closedByName ? ` · closed by ${nc.closedByName} ${formatDate(nc.closedAt!)}` : ''}</p> : null}
        <fieldset disabled={!editable || !canManage} className="space-y-3">
          <div className="grid grid-cols-2 gap-2">
            <Field label="Factory" required><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
            <Field label="Severity"><select className={inputClass} value={form.severity} onChange={(e) => setForm({ ...form, severity: e.target.value })}><option>Major</option><option>Minor</option><option>Observation</option></select></Field>
            <Field label="Source"><select className={inputClass} value={form.source} onChange={(e) => setForm({ ...form, source: e.target.value })}><option>Audit</option><option>Self-assessment</option><option>Internal inspection</option><option>Customer</option><option>Other</option></select></Field>
            <Field label="From audit"><select className={inputClass} value={form.auditEventId} onChange={(e) => setForm({ ...form, auditEventId: e.target.value })}><option value="">—</option>{(audits.data ?? []).map((a) => <option key={a.id} value={a.id}>{a.kind} {formatDate(a.scheduledFor)}</option>)}</select></Field>
          </div>
          <Field label="Requirement"><select className={inputClass} value={form.requirementId} onChange={(e) => setForm({ ...form, requirementId: e.target.value })}><option value="">—</option>{requirements.map((r) => <option key={r.id} value={r.id}>{r.label}</option>)}</select></Field>
          <Field label="Finding" required><textarea className={textareaClass} value={form.finding} onChange={(e) => setForm({ ...form, finding: e.target.value })} /></Field>
          <Field label="Root cause"><textarea className={textareaClass} value={form.rootCause} onChange={(e) => setForm({ ...form, rootCause: e.target.value })} /></Field>
          <Field label="Corrective action"><textarea className={textareaClass} value={form.correctiveAction} onChange={(e) => setForm({ ...form, correctiveAction: e.target.value })} /></Field>
          <div className="grid grid-cols-2 gap-2">
            <Field label="Owner"><select className={inputClass} value={form.ownerUserId} onChange={(e) => setForm({ ...form, ownerUserId: e.target.value })}><option value="">—</option>{users.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>
            <Field label="Due"><input type="date" className={inputClass} value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} /></Field>
          </div>
          {nc && !nc.workOrderId ? <Field label="Asset for the corrective job"><AssetPicker value={asset} onChange={setAsset} /></Field> : null}
        </fieldset>
        {nc && (nc.status === 'Open' || nc.status === 'InProgress') && canWork ? (
          <div className="space-y-2 rounded-lg bg-slate-50 p-3">
            <Field label="Evidence it is corrected" required><textarea className={textareaClass} value={evidence} placeholder="What was done and where the proof is (photo, record, document)" onChange={(e) => setEvidence(e.target.value)} /></Field>
            <button type="button" className={primaryButton} disabled={!evidence.trim() || !(form.correctiveAction || nc.correctiveAction)} onClick={() => void act('resolve', 'Marked resolved', { correctiveAction: form.correctiveAction || null, evidence })}>Mark resolved</button>
          </div>
        ) : null}
      </div>
    </FormDrawer>
  )
}

/** People who can own a corrective action (the list needs settings rights; others pick no owner). */
function useUsersForOwner() {
  const subjects = useDataSubjects(usePermission('settings.manage'))
  return useMemo(() => (subjects.data ?? []).filter((x) => x.isActive && x.userId).map((x) => ({ id: x.userId!, name: x.name })), [subjects.data])
}

/* ── Audit calendar ──────────────────────────────────────────────────────── */

function AuditsTab() {
  const audits = useAudits()
  const standards = useStandards()
  const factories = useFactories()
  const action = useFactoryAction()
  const canManage = useAnyPermission('settings.manage', 'work-orders.assign')
  const empty = { factoryId: '', standardId: '', kind: 'Surveillance audit', scheduledFor: '', auditor: '', notes: '' }
  const [form, setForm] = useState(empty)
  const plan = async () => {
    if (await attempt(() => action.mutateAsync({ path: `${fwPath}/audits`, body: { ...form, factoryId: form.factoryId || factories[0]?.id, standardId: form.standardId || null, auditor: form.auditor || null, notes: form.notes || null } }), 'Audit planned')) setForm(empty)
  }
  const mark = (a: { id: string; factoryId: string; standardId: string | null; kind: string; scheduledFor: string; auditor: string | null; notes: string | null }, status: string, outcome?: string) =>
    attempt(() => action.mutateAsync({ method: 'put', path: `${fwPath}/audits/${a.id}`, body: { factoryId: a.factoryId, standardId: a.standardId, kind: a.kind, scheduledFor: a.scheduledFor.slice(0, 10), auditor: a.auditor, notes: a.notes, status, outcome: outcome ?? null } }), status === 'Done' ? 'Marked done' : 'Updated')
  return (
    <div className="space-y-4">
      {audits.isLoading ? <Spinner /> : (audits.data ?? []).length === 0 ? <EmptyState icon={ShieldCheck} title="No audits planned" description="Put self-assessments, surveillance audits and recertifications on the calendar; supervisors are reminded 30 and 7 days ahead." /> : (
        <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          {(audits.data ?? []).map((a) => (
            <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 p-3 text-sm">
              <span><span className="font-medium text-slate-900">{formatDate(a.scheduledFor, 'EEE dd MMM yyyy')}</span> · {a.kind}{a.standardName ? ` · ${a.standardName}` : ''}<span className="block text-xs text-slate-500">{a.factoryName}{a.auditor ? ` · ${a.auditor}` : ''}{a.findings ? ` · ${a.findings} findings` : ''}{a.outcome ? ` · ${a.outcome}` : ''}</span></span>
              <span className="flex items-center gap-2"><Badge size="sm" tone={a.status === 'Done' ? 'success' : a.status === 'Cancelled' ? 'neutral' : 'info'}>{a.status}</Badge>
                {canManage && a.status === 'Planned' ? <><button type="button" className="text-xs text-[var(--app-primary)] underline" onClick={() => void mark(a, 'Done', window.prompt('Outcome (e.g. certified, 2 minor findings)') ?? undefined)}>Done</button><button type="button" className="text-xs text-slate-500 underline" onClick={() => void mark(a, 'Cancelled')}>Cancel</button></> : null}</span>
            </li>
          ))}
        </ul>
      )}
      {canManage ? (
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-3">
          <Field label="Factory"><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          <Field label="Standard"><select className={inputClass} value={form.standardId} onChange={(e) => setForm({ ...form, standardId: e.target.value })}><option value="">—</option>{(standards.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
          <Field label="Kind"><select className={inputClass} value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>{AUDIT_KINDS.map((k) => <option key={k}>{k}</option>)}</select></Field>
          <Field label="Date"><input type="date" className={inputClass} value={form.scheduledFor} onChange={(e) => setForm({ ...form, scheduledFor: e.target.value })} /></Field>
          <Field label="Auditor"><input className={inputClass} value={form.auditor} onChange={(e) => setForm({ ...form, auditor: e.target.value })} /></Field>
          <div className="flex items-end"><button type="button" className={primaryButton} disabled={!form.scheduledFor} onClick={() => void plan()}>Plan audit</button></div>
        </div>
      ) : null}
    </div>
  )
}

/* ── Registers ───────────────────────────────────────────────────────────── */

function RegistersTab() {
  const [register, setRegister] = useState<'waste' | 'food' | 'glass'>('waste')
  return (
    <div className="space-y-4">
      <div className="inline-flex gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {([['waste', 'Waste disposal'], ['food', 'Food safety checks'], ['glass', 'Glass and hard plastic']] as const).map(([id, label]) => (
          <button key={id} type="button" className={cn('h-8 rounded-md px-3 text-sm', register === id ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50')} onClick={() => setRegister(id)}>{label}</button>
        ))}
      </div>
      {register === 'waste' ? <WasteRegister /> : register === 'food' ? <FoodSafetyRegister /> : <GlassRegister />}
    </div>
  )
}

function WasteRegister() {
  const range = useMemo(() => periodDates('last12', { from: '', to: '' }), [])
  const waste = useWaste(range.from, range.to)
  const factories = useFactories()
  const contractors = useContractors()
  const action = useFactoryAction()
  const canWork = useAnyPermission('work-orders.assign', 'work-orders.complete')
  const empty = { factoryId: '', disposedOn: new Date().toISOString().slice(0, 10), wasteType: 'Used oil', quantity: '', unit: 'litres', contractorId: '', handlerName: '', licenceNumber: '', consignmentNote: '' }
  const [form, setForm] = useState(empty)
  const save = async () => {
    if (await attempt(() => action.mutateAsync({ path: `${fwPath}/waste`, body: { ...form, factoryId: form.factoryId || factories[0]?.id, quantity: Number(form.quantity), contractorId: form.contractorId || null, handlerName: form.handlerName || null, licenceNumber: form.licenceNumber || null, consignmentNote: form.consignmentNote || null } }), 'Disposal recorded')) setForm(empty)
  }
  return (
    <div className="space-y-4">
      {(waste.data ?? []).length === 0 ? <p className="text-sm text-slate-500">No disposals in the last 12 months.</p> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Date</th><th className="px-4 py-2">Waste</th><th className="px-4 py-2 text-right">Quantity</th><th className="px-4 py-2">Handler</th><th className="px-4 py-2">Licence</th><th className="px-4 py-2">Consignment note</th></tr></thead>
            <tbody className="divide-y divide-slate-100">{(waste.data ?? []).map((w) => <tr key={w.id}><td className="px-4 py-2">{formatDate(w.disposedOn)}<span className="block text-xs text-slate-500">{w.factoryName}</span></td><td className="px-4 py-2">{w.wasteType}</td><td className="px-4 py-2 text-right tabular-nums">{w.quantity} {w.unit}</td><td className="px-4 py-2">{w.handlerName}</td><td className="px-4 py-2">{w.licenceNumber ?? <span className="text-rose-700">None</span>}</td><td className="px-4 py-2">{w.consignmentNote ?? '—'}</td></tr>)}</tbody>
          </table>
        </div>
      )}
      {canWork ? (
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-4">
          <Field label="Factory"><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          <Field label="Date"><input type="date" className={inputClass} value={form.disposedOn} onChange={(e) => setForm({ ...form, disposedOn: e.target.value })} /></Field>
          <Field label="Waste"><select className={inputClass} value={form.wasteType} onChange={(e) => setForm({ ...form, wasteType: e.target.value })}>{WASTE_TYPES.map((t) => <option key={t}>{t}</option>)}</select></Field>
          <div className="grid grid-cols-[1fr_6rem] gap-2"><Field label="Quantity"><input type="number" min={0} className={inputClass} value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} /></Field><Field label="Unit"><input className={inputClass} value={form.unit} onChange={(e) => setForm({ ...form, unit: e.target.value })} /></Field></div>
          <Field label="Licensed handler"><select className={inputClass} value={form.contractorId} onChange={(e) => setForm({ ...form, contractorId: e.target.value })}><option value="">Not a registered contractor</option>{(contractors.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
          {!form.contractorId ? <Field label="Handler name"><input className={inputClass} value={form.handlerName} onChange={(e) => setForm({ ...form, handlerName: e.target.value })} /></Field> : null}
          <Field label="Licence (NEMA)"><input className={inputClass} value={form.licenceNumber} onChange={(e) => setForm({ ...form, licenceNumber: e.target.value })} /></Field>
          <Field label="Consignment note"><input className={inputClass} value={form.consignmentNote} onChange={(e) => setForm({ ...form, consignmentNote: e.target.value })} /></Field>
          <div className="flex items-end"><button type="button" className={primaryButton} disabled={!Number(form.quantity) || (!form.contractorId && !form.handlerName)} onClick={() => void save()}>Record disposal</button></div>
        </div>
      ) : null}
    </div>
  )
}

function FoodSafetyRegister() {
  const range = useMemo(() => periodDates('quarter', { from: '', to: '' }), [])
  const [kind, setKind] = useState('')
  const records = useFoodSafety(kind, range.from, range.to)
  const factories = useFactories()
  const action = useFactoryAction()
  const links = useFactoryLinks()
  const canWork = useAnyPermission('work-orders.assign', 'work-orders.complete')
  const empty = { factoryId: '', kind: 'MetalDetector', area: '', method: '', passed: true, fe: true, nfe: true, ss: true, checked: '', damaged: '0', notes: '', verifiedByName: '' }
  const [form, setForm] = useState(empty)
  const [asset, setAsset] = useState<PickedAsset | null>(null)
  const save = async () => {
    const body = { factoryId: form.factoryId || factories[0]?.id, kind: form.kind, assetId: asset?.id ?? null, area: form.area || null, notes: form.notes || null, verifiedByName: form.verifiedByName || null,
      ...(form.kind === 'MetalDetector' ? { ferrousRejected: form.fe, nonFerrousRejected: form.nfe, stainlessRejected: form.ss } : form.kind === 'GlassCheck' ? { itemsChecked: form.checked ? Number(form.checked) : null, itemsDamaged: Number(form.damaged) || 0 } : { method: form.method || null, passed: form.passed }) }
    try {
      const result = (await action.mutateAsync({ path: `${fwPath}/food-safety`, body })) as { passed: boolean; correctiveWorkOrderNumber: string | null }
      if (result.passed) toast.success('Recorded')
      else toast.error(`Failed: corrective job ${result.correctiveWorkOrderNumber} raised`)
      setForm({ ...empty, kind: form.kind, factoryId: form.factoryId })
      setAsset(null)
    } catch {
      // The API client shows the error.
    }
  }
  const yn = (b: boolean | null) => (b == null ? '—' : b ? '✓' : '✗')
  return (
    <div className="space-y-4">
      <Field label="Show" className="w-48"><select className={inputClass} value={kind} onChange={(e) => setKind(e.target.value)}><option value="">All checks</option><option value="MetalDetector">Metal detector</option><option value="Cleaning">Cleaning</option><option value="GlassCheck">Glass checks</option></select></Field>
      {(records.data ?? []).length === 0 ? <p className="text-sm text-slate-500">No checks in the last 3 months.</p> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">When</th><th className="px-4 py-2">Check</th><th className="px-4 py-2">Where</th><th className="px-4 py-2">Detail</th><th className="px-4 py-2">Result</th><th className="px-4 py-2">By</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(records.data ?? []).map((r) => (
                <tr key={r.id}>
                  <td className="px-4 py-2 text-xs">{formatDateTime(r.doneAt)}</td>
                  <td className="px-4 py-2">{r.kind === 'MetalDetector' ? 'Metal detector' : r.kind === 'GlassCheck' ? 'Glass check' : 'Cleaning'}</td>
                  <td className="px-4 py-2">{r.assetCode ? `${r.assetCode} ${r.assetName}` : r.area}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{r.kind === 'MetalDetector' ? `Fe ${yn(r.ferrousRejected)} · Non-Fe ${yn(r.nonFerrousRejected)} · SS ${yn(r.stainlessRejected)}` : r.kind === 'GlassCheck' ? `${r.itemsChecked ?? '—'} checked, ${r.itemsDamaged ?? 0} damaged` : r.method ?? ''}{r.notes ? ` · ${r.notes}` : ''}</td>
                  <td className="px-4 py-2">{r.passed ? <Badge size="sm" tone="success">Pass</Badge> : <><Badge size="sm" tone="danger">Fail</Badge>{r.correctiveWorkOrderId ? <Link className="ml-1 text-xs text-[var(--app-primary)] underline" to={links.job(r.correctiveWorkOrderId)}>{r.correctiveWorkOrderNumber}</Link> : null}</>}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{r.doneByName}{r.verifiedByName ? ` / ${r.verifiedByName}` : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {canWork ? (
        <div className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
          <div className="grid gap-2 sm:grid-cols-3">
            <Field label="Factory"><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
            <Field label="Check"><select className={inputClass} value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}><option value="MetalDetector">Metal detector test</option><option value="Cleaning">Cleaning</option><option value="GlassCheck">Glass and hard plastic check</option></select></Field>
            <Field label="Asset"><AssetPicker value={asset} onChange={setAsset} /></Field>
          </div>
          {!asset ? <Field label="Or area"><input className={inputClass} value={form.area} placeholder="e.g. Sorting room" onChange={(e) => setForm({ ...form, area: e.target.value })} /></Field> : null}
          {form.kind === 'MetalDetector' ? (
            <div className="flex flex-wrap gap-4 text-sm">
              {([['fe', 'Ferrous test piece rejected'], ['nfe', 'Non-ferrous rejected'], ['ss', 'Stainless steel rejected']] as const).map(([k, label]) => (
                <label key={k} className="flex items-center gap-2"><input type="checkbox" checked={form[k]} onChange={(e) => setForm({ ...form, [k]: e.target.checked })} />{label}</label>
              ))}
            </div>
          ) : form.kind === 'GlassCheck' ? (
            <div className="grid grid-cols-2 gap-2 sm:w-80">
              <Field label="Items checked"><input type="number" min={0} className={inputClass} value={form.checked} onChange={(e) => setForm({ ...form, checked: e.target.value })} /></Field>
              <Field label="Damaged"><input type="number" min={0} className={inputClass} value={form.damaged} onChange={(e) => setForm({ ...form, damaged: e.target.value })} /></Field>
            </div>
          ) : (
            <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
              <Field label="Method and chemical"><input className={inputClass} value={form.method} onChange={(e) => setForm({ ...form, method: e.target.value })} /></Field>
              <label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={form.passed} onChange={(e) => setForm({ ...form, passed: e.target.checked })} />Passed verification</label>
            </div>
          )}
          <div className="grid gap-2 sm:grid-cols-[1fr_14rem_auto] sm:items-end">
            <Field label="Notes"><input className={inputClass} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field>
            <Field label="Verified by"><input className={inputClass} value={form.verifiedByName} onChange={(e) => setForm({ ...form, verifiedByName: e.target.value })} /></Field>
            <button type="button" className={primaryButton} disabled={!asset && !form.area} onClick={() => void save()}>Record</button>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function GlassRegister() {
  const factories = useFactories()
  const [factoryId, setFactoryId] = useState('')
  const factory = factoryId || factories[0]?.id || ''
  const items = useGlassRegister(factory)
  const action = useFactoryAction()
  const canWork = useAnyPermission('work-orders.assign', 'work-orders.complete')
  const empty = { location: '', description: '', material: 'Glass', quantity: '1' }
  const [form, setForm] = useState(empty)
  const add = async () => { if (await attempt(() => action.mutateAsync({ path: `${fwPath}/glass-register`, body: { factoryId: factory, ...form, quantity: Number(form.quantity) } }), 'Added')) setForm(empty) }
  const retire = (id: string, item: { location: string; description: string; material: string; quantity: number }) =>
    attempt(() => action.mutateAsync({ method: 'put', path: `${fwPath}/glass-register/${id}`, body: { factoryId: factory, ...item, isActive: false } }), 'Removed from the register')
  const live = (items.data ?? []).filter((x) => x.isActive)
  return (
    <div className="space-y-4">
      <Field label="Factory" className="w-56"><select className={inputClass} value={factory} onChange={(e) => setFactoryId(e.target.value)}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
      <p className="text-sm text-slate-600">{live.reduce((s, x) => s + x.quantity, 0)} items on the register. Record each check under Food safety checks.</p>
      {live.length > 0 ? (
        <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white text-sm shadow-sm">
          {live.map((x) => <li key={x.id} className="flex items-center justify-between gap-2 px-4 py-2"><span>{x.description} × {x.quantity}<span className="block text-xs text-slate-500">{x.location} · {x.material}</span></span>{canWork ? <button type="button" className="text-xs text-slate-500 underline" onClick={() => void retire(x.id, x)}>Remove</button> : null}</li>)}
        </ul>
      ) : null}
      {canWork ? (
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-[1fr_1fr_9rem_5rem_auto] sm:items-end">
          <Field label="Where"><input className={inputClass} value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} /></Field>
          <Field label="Item"><input className={inputClass} value={form.description} placeholder="Light cover, sight glass…" onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
          <Field label="Material"><select className={inputClass} value={form.material} onChange={(e) => setForm({ ...form, material: e.target.value })}><option>Glass</option><option>Hard plastic</option><option>Perspex</option></select></Field>
          <Field label="How many"><input type="number" min={1} className={inputClass} value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} /></Field>
          <button type="button" className={primaryButton} disabled={!form.location || !form.description || !factory} onClick={() => void add()}>Add</button>
        </div>
      ) : null}
    </div>
  )
}

/* ── Data protection ─────────────────────────────────────────────────────── */

function PrivacyTab() {
  const people = useDataSubjects(true)
  const consents = useConsents(true)
  const action = useFactoryAction()
  const [consent, setConsent] = useState({ person: '', purpose: '', method: 'Signed form' })
  const exportPerson = async (userId: string | null, technicianId: string | null, name: string) => {
    try {
      const data = await api.get<unknown>('/api/factory/data-protection/export', { userId: userId ?? undefined, technicianId: technicianId ?? undefined })
      const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }))
      const link = document.createElement('a')
      link.href = url
      link.download = `personal-data-${name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}.json`
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch {
      // The API client shows the error.
    }
  }
  const erase = async (userId: string | null, technicianId: string | null, name: string) => {
    const confirm = window.prompt(`Erase ${name}'s personal details? Their maintenance records are kept under a placeholder name. This can't be undone. Type ERASE to confirm.`)
    if (confirm !== 'ERASE') return
    await attempt(() => action.mutateAsync({ path: '/api/factory/data-protection/erase', body: { userId, technicianId, confirm } }), 'Erased')
  }
  const addConsent = async () => {
    const [kind, id] = consent.person.split(':')
    if (await attempt(() => action.mutateAsync({ path: '/api/factory/data-protection/consents', body: { purpose: consent.purpose, method: consent.method, userId: kind === 'u' ? id : null, technicianId: kind === 't' ? id : null } }), 'Consent recorded')) setConsent({ person: '', purpose: '', method: 'Signed form' })
  }
  return (
    <div className="space-y-6">
      <section className="space-y-2">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-slate-800">People whose data is held</h2>
          <button type="button" className={secondaryButton} onClick={() => void attempt(async () => {
            const r = (await action.mutateAsync({ path: '/api/factory/data-protection/retention' })) as { trainingAnonymised: number; ppeAnonymised: number; consentsRemoved: number }
            toast(`${r.trainingAnonymised} training and ${r.ppeAnonymised} PPE records anonymised; ${r.consentsRemoved} old consents removed.`)
          }, 'Retention applied')}>Apply retention now</button>
        </div>
        {people.isLoading ? <Spinner /> : (
          <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white text-sm shadow-sm">
            {(people.data ?? []).map((p) => (
              <li key={`${p.userId}-${p.technicianId}`} className={cn('flex items-center justify-between gap-2 px-4 py-2', !p.isActive && 'bg-slate-50')}>
                <span>{p.name}<span className="block text-xs text-slate-500">{p.kind} · {p.email || 'no email'}{p.isActive ? '' : ' · left'}</span></span>
                <span className="flex gap-3 text-xs">
                  <button type="button" className="text-[var(--app-primary)] underline" onClick={() => void exportPerson(p.userId, p.technicianId, p.name)}>Export</button>
                  {!p.isActive && !p.name.startsWith('Erased') ? <button type="button" className="text-rose-700 underline" onClick={() => void erase(p.userId, p.technicianId, p.name)}>Erase</button> : null}
                </span>
              </li>
            ))}
          </ul>
        )}
        <p className="text-xs text-slate-500">Only people who have left (deactivated) can be erased. Export answers a subject access request.</p>
      </section>
      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-slate-800">Consents</h2>
        {(consents.data ?? []).length === 0 ? <p className="text-sm text-slate-500">None recorded.</p> : (
          <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white text-sm shadow-sm">
            {(consents.data ?? []).map((c) => (
              <li key={c.id} className="flex items-center justify-between gap-2 px-4 py-2">
                <span>{c.subjectName}: {c.purpose}<span className="block text-xs text-slate-500">{c.method} · {formatDate(c.givenAt)}{c.withdrawnAt ? ` · withdrawn ${formatDate(c.withdrawnAt)}` : ''}</span></span>
                {!c.withdrawnAt ? <button type="button" className="text-xs text-slate-500 underline" onClick={() => void attempt(() => action.mutateAsync({ path: `/api/factory/data-protection/consents/${c.id}/withdraw` }), 'Withdrawn')}>Withdrawn</button> : <Badge size="sm">Withdrawn</Badge>}
              </li>
            ))}
          </ul>
        )}
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-[1fr_1.5fr_10rem_auto] sm:items-end">
          <Field label="Person"><select className={inputClass} value={consent.person} onChange={(e) => setConsent({ ...consent, person: e.target.value })}><option value="">Choose</option>{(people.data ?? []).filter((p) => p.isActive).map((p) => <option key={`${p.userId}-${p.technicianId}`} value={p.technicianId ? `t:${p.technicianId}` : `u:${p.userId}`}>{p.name}</option>)}</select></Field>
          <Field label="For"><input className={inputClass} value={consent.purpose} placeholder="Phone number for SMS job alerts" onChange={(e) => setConsent({ ...consent, purpose: e.target.value })} /></Field>
          <Field label="How"><select className={inputClass} value={consent.method} onChange={(e) => setConsent({ ...consent, method: e.target.value })}><option>Signed form</option><option>In the app</option><option>Verbal, witnessed</option></select></Field>
          <button type="button" className={primaryButton} disabled={!consent.person || !consent.purpose} onClick={() => void addConsent()}>Record</button>
        </div>
      </section>
    </div>
  )
}
