import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { HardHat, Package, Undo2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { useAnyPermission, usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { Field, formatNumber, inputClass, primaryButton, secondaryButton } from '../components/ui'
import type { JobDetail } from '../jobs/types'
import { PartPicker, type PickedPart } from './PartPicker'
import { useContractorAction, useContractors, useIssuePart, useJobSpares, useStores, useStoresAction, type JobReservation } from './api'

const finished = (job: JobDetail) => ['Completed', 'Verified', 'Closed', 'Cancelled', 'Rejected'].includes(job.status)

export const RESERVATION_TONES: Record<JobReservation['status'], BadgeTone> = { Reserved: 'info', Kitted: 'success', Issued: 'neutral', Released: 'neutral' }

/**
 * Spares on a job: what is held for it in stores (reserved or kitted), what has been issued and
 * at what cost, issuing a part from the store, and returning what wasn't used.
 */
export function JobSparesCard({ job }: { job: JobDetail }) {
  const spares = useJobSpares(job.id)
  const canPlan = useAnyPermission('work-orders.assign', 'materials.issue')
  const canIssue = usePermission('work-orders.complete') || canPlan
  const canKit = useAnyPermission('materials.issue', 'materials.manage')
  const action = useStoresAction()
  const [drawer, setDrawer] = useState<'issue' | 'reserve' | null>(null)
  const [returning, setReturning] = useState<string | null>(null)
  const open = !finished(job)
  const data = spares.data
  const held = data?.reservations.filter((x) => x.status === 'Reserved' || x.status === 'Kitted') ?? []

  const run = async (path: string, message: string, body?: unknown) => {
    try {
      await action.mutateAsync({ path, body })
      toast.success(message)
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-1.5 text-sm font-semibold text-slate-800"><Package className="h-4 w-4 text-slate-500" />Spares{data?.partsCost ? ` · ${formatNumber(data.partsCost)}` : ''}</h2>
        {data?.defaultStoreName ? <span className="text-xs text-slate-500">From {data.defaultStoreName}</span> : null}
      </div>

      {data && !data.defaultStoreId ? <p className="mb-2 rounded-lg bg-amber-50 p-2 text-xs text-amber-800">This job's factory has no store yet: parts can't be issued until one is set up under <Link className="underline" to="/maintenance/stores">Stores</Link>.</p> : null}

      {held.length > 0 ? (
        <div className="mb-3">
          <p className="mb-1 text-xs font-medium uppercase tracking-wide text-slate-500">Held in stores</p>
          <ul className="space-y-1.5">
            {held.map((r) => (
              <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                <span>{r.itemName} × {formatNumber(r.quantity - r.issuedQuantity)} {r.unitOfMeasure} <Badge size="sm" tone={RESERVATION_TONES[r.status]}>{r.status === 'Kitted' ? 'Kitted' : 'Reserved'}</Badge></span>
                <span className="flex gap-2 text-xs">
                  {r.status === 'Reserved' && canKit ? <button type="button" className="font-medium text-[var(--app-primary)] underline" onClick={() => void run(`/reservations/${r.id}/kit`, 'Kitted')}>Kit</button> : null}
                  {canPlan && open ? <button type="button" className="text-slate-500 underline" onClick={() => void run(`/reservations/${r.id}/release`, 'Released')}>Release</button> : null}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {data && data.used.length > 0 ? (
        <div className="mb-3">
          <p className="mb-1 text-xs font-medium uppercase tracking-wide text-slate-500">Issued</p>
          <ul className="space-y-1.5">
            {data.used.map((u) => (
              <li key={u.id} className="text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span>{u.itemName} × {formatNumber(u.quantity)} {u.unitOfMeasure}<span className="block text-xs text-slate-500">{u.notes ?? ''} · {formatDateTime(u.usedAt)}</span></span>
                  <span className="flex items-center gap-2 text-xs tabular-nums text-slate-600">{formatNumber(u.cost)}
                    {u.quantity > 0 && canIssue ? <button type="button" aria-label={`Return ${u.itemName}`} className="text-slate-500" onClick={() => setReturning(u.id)}><Undo2 className="h-4 w-4" /></button> : null}
                  </span>
                </div>
                {returning === u.id ? <ReturnForm job={job} usage={u} onDone={() => setReturning(null)} /> : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {data && held.length === 0 && data.used.length === 0 ? <p className="mb-3 text-sm text-slate-500">No spares held or issued.</p> : null}

      {open ? (
        <div className="flex flex-wrap gap-2">
          {canIssue ? <button type="button" className={secondaryButton} onClick={() => setDrawer('issue')}>Use a part</button> : null}
          {canPlan && data && data.planned.length > 0 ? <button type="button" className={secondaryButton} onClick={() => void run(`/api/factory/work-orders/${job.id}/reservations/from-plan`, 'Planned parts reserved')}>Reserve planned parts</button> : null}
          {canPlan ? <button type="button" className={secondaryButton} onClick={() => setDrawer('reserve')}>Reserve</button> : null}
        </div>
      ) : null}

      <IssueDrawer job={job} mode={drawer} defaultStoreId={data?.defaultStoreId ?? null} onClose={() => setDrawer(null)} />
    </section>
  )
}

function ReturnForm({ job, usage, onDone }: { job: JobDetail; usage: { id: string; quantity: number; isSerialised: boolean; itemName: string; notes: string | null }; onDone: () => void }) {
  const action = useStoresAction()
  const serial = usage.notes?.match(/serial ([^.,]+)/)?.[1] ?? ''
  const [quantity, setQuantity] = useState(usage.isSerialised ? '1' : String(usage.quantity))
  const [needsRepair, setNeedsRepair] = useState(false)
  const submit = async () => {
    try {
      await action.mutateAsync({ path: `/api/factory/work-orders/${job.id}/parts/${usage.id}/return`, body: { quantity: Number(quantity), serialNumber: serial || null, needsRepair } })
      toast.success(needsRepair ? 'Booked in for repair' : 'Returned to stores')
      onDone()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="mt-2 flex flex-wrap items-end gap-2 rounded-lg bg-slate-50 p-2">
      {!usage.isSerialised ? <Field label="Return" className="w-24"><input type="number" min={0} max={usage.quantity} className={inputClass} value={quantity} onChange={(e) => setQuantity(e.target.value)} /></Field> : <p className="text-xs text-slate-600">Serial {serial}</p>}
      {usage.isSerialised ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={needsRepair} onChange={(e) => setNeedsRepair(e.target.checked)} />Worn: for repair</label> : null}
      <button type="button" className={secondaryButton} onClick={onDone}>Cancel</button>
      <button type="button" className={primaryButton} disabled={action.isPending || Number(quantity) <= 0} onClick={() => void submit()}>Return</button>
    </div>
  )
}

function IssueDrawer({ job, mode, defaultStoreId, onClose }: { job: JobDetail; mode: 'issue' | 'reserve' | null; defaultStoreId: string | null; onClose: () => void }) {
  const stores = useStores()
  const issue = useIssuePart(job)
  const action = useStoresAction()
  const [part, setPart] = useState<PickedPart | null>(null)
  const [storeId, setStoreId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [serial, setSerial] = useState('')
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)

  const close = () => {
    setPart(null)
    setStoreId('')
    setQuantity('1')
    setSerial('')
    setNotes('')
    onClose()
  }

  const submit = async () => {
    if (!part) return
    setBusy(true)
    try {
      if (mode === 'issue') {
        const result = await issue({ materialItemId: part.id, quantity: Number(quantity), storeId: storeId || null, serialNumber: serial || null, notes: notes || null }, part.name)
        toast.success(result.queued ? 'Saved, will sync' : 'Part issued')
      } else {
        await action.mutateAsync({ path: `/api/factory/work-orders/${job.id}/reservations`, body: { materialItemId: part.id, quantity: Number(quantity), storeId: storeId || null } })
        toast.success('Reserved')
      }
      close()
    } catch {
      // The API client shows the error.
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormDrawer open={mode !== null} title={mode === 'issue' ? 'Use a part' : 'Reserve a part'} onClose={close} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={close}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!part || busy || Number(quantity) <= 0 || (part?.isSerialised && mode === 'issue' && !serial)} onClick={() => void submit()}>{mode === 'issue' ? 'Issue' : 'Reserve'}</button></div>
    )}>
      <div className="space-y-3">
        <Field label="Part" required><PartPicker value={part} onChange={setPart} /></Field>
        <Field label="Store" hint={defaultStoreId ? 'Blank: the factory\'s main store' : undefined}>
          <select className={inputClass} value={storeId} onChange={(e) => setStoreId(e.target.value)}>
            <option value="">Main store</option>
            {(stores.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name} · {s.factoryName}</option>)}
          </select>
        </Field>
        {part?.isSerialised && mode === 'issue' ? (
          <Field label="Serial number" required hint="Which unit is being fitted"><input className={inputClass} value={serial} onChange={(e) => setSerial(e.target.value)} /></Field>
        ) : (
          <Field label={`Quantity${part ? ` (${part.unitOfMeasure})` : ''}`} required><input type="number" min={0} step="any" className={inputClass} value={quantity} onChange={(e) => setQuantity(e.target.value)} /></Field>
        )}
        {mode === 'issue' ? <Field label="Notes"><input className={inputClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field> : null}
      </div>
    </FormDrawer>
  )
}

/** On a job: the outside contractor doing it, and the agreement it falls under. */
export function JobContractorCard({ job }: { job: JobDetail }) {
  const canAssign = usePermission('work-orders.assign')
  const contractors = useContractors()
  const action = useContractorAction()
  const [choosing, setChoosing] = useState(false)
  const [contractorId, setContractorId] = useState('')
  const open = !finished(job)
  if (!job.contractorId && (!canAssign || !open)) return null

  const assign = async () => {
    try {
      await action.mutateAsync({ path: `/api/factory/work-orders/${job.id}/contractor`, body: { contractorId } })
      toast.success('Given to contractor')
      setChoosing(false)
    } catch {
      // The API client shows the error.
    }
  }

  const remove = async () => {
    try {
      await action.mutateAsync({ method: 'delete', path: `/api/factory/work-orders/${job.id}/contractor` })
      toast.success('Taken off the contractor')
    } catch {
      // The API client shows the error.
    }
  }

  const chosen = contractors.data?.find((x) => x.id === contractorId)
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-slate-800"><HardHat className="h-4 w-4 text-slate-500" />Contractor</h2>
      {job.contractorId ? (
        <div className="text-sm">
          <Link to="/maintenance/contractors" className="font-medium text-[var(--app-primary)] underline">{job.contractorName}</Link>
          {job.agreementNumber ? <span className="block text-xs text-slate-500">Under {job.agreementNumber}{job.repairDueAt ? ` · fix by ${formatDate(job.repairDueAt)}` : ''}</span> : null}
          {canAssign && open ? <button type="button" className="mt-2 text-xs text-slate-500 underline" onClick={() => void remove()}>Take off contractor</button> : null}
        </div>
      ) : choosing ? (
        <div className="space-y-2">
          <select className={inputClass} value={contractorId} onChange={(e) => setContractorId(e.target.value)}>
            <option value="">Choose a contractor</option>
            {(contractors.data ?? []).map((c) => <option key={c.id} value={c.id} disabled={!c.canTakeWork}>{c.name}{c.canTakeWork ? '' : ' (can\'t take work)'}</option>)}
          </select>
          {chosen && !chosen.canTakeWork ? <p className="text-xs text-rose-700">{chosen.blockers.join(' ')}</p> : null}
          <div className="flex gap-2"><button type="button" className={secondaryButton} onClick={() => setChoosing(false)}>Cancel</button><button type="button" className={primaryButton} disabled={!contractorId || action.isPending} onClick={() => void assign()}>Give to contractor</button></div>
        </div>
      ) : (
        <button type="button" className={secondaryButton} onClick={() => setChoosing(true)}>Give to a contractor</button>
      )}
    </section>
  )
}
