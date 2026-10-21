import { useDeferredValue, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FileText, Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate } from '../../../shared/utils/dates'
import {
  useBillingTerms,
  useCertifyValuation,
  useRaiseAdvanceInvoice,
  useRaiseValuationInvoice,
  useReleaseRetention,
  useRetention,
  useSaveBillingTerms,
  useSaveValuation,
  useSubmitValuation,
  useValuationPreview,
  useValuations,
  useVoidValuation,
} from '../siteApi'
import type { BillingTerms, ProjectDetail, RetentionStage, Valuation } from '../types'
import { fieldClass, labelClass, numberOrNull, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull, todayInput } from './formStyles'

interface ValuationDraft {
  id?: string
  periodEnd: string
  workDoneToDate: string
  materialsOnSite: string
  notes: string
}

interface CertifyDraft {
  valuation: Valuation
  consultantId: string
  certificateReference: string
  certifiedOn: string
  certifiedAmount: string
}

type TermsDraft = Record<'retentionPercent' | 'retentionCapAmount' | 'retentionReleaseAtCompletionPercent' | 'defectsLiabilityMonths' | 'advancePaymentAmount' | 'advanceRecoveryPercent', string>

const termsDraftFrom = (terms: BillingTerms): TermsDraft => ({
  retentionPercent: terms.retentionPercent?.toString() ?? '',
  retentionCapAmount: terms.retentionCapAmount?.toString() ?? '',
  retentionReleaseAtCompletionPercent: terms.retentionReleaseAtCompletionPercent.toString(),
  defectsLiabilityMonths: terms.defectsLiabilityMonths?.toString() ?? '',
  advancePaymentAmount: terms.advancePaymentAmount?.toString() ?? '',
  advanceRecoveryPercent: terms.advanceRecoveryPercent?.toString() ?? '',
})

const stageLabels: Record<RetentionStage, string> = {
  PracticalCompletion: 'At practical completion',
  EndOfDefectsLiability: 'After the defects liability period',
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex justify-between gap-3 py-1 text-sm ${strong ? 'border-t border-slate-200 pt-2 font-semibold text-slate-900' : 'text-slate-700'}`}>
      <span>{label}</span><span>{value}</span>
    </div>
  )
}

/**
 * Interim valuations (D9), recorded as totals: the QS's BQ workings stay outside Ecosys and are
 * attached as documents. Ecosys works out retention, advance recovery and the amount due; the
 * consultant's certified figure is what gets invoiced.
 */
export function ValuationsTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const { abilities } = project
  const isCancelled = project.summary.stageCategory === 'Cancelled'
  const isClosed = project.summary.stageCategory === 'Closed'
  const canManage = abilities.canManageFinancials && !isCancelled
  const { data: terms } = useBillingTerms(projectId)
  const { data: valuations, isLoading } = useValuations(projectId)
  const { data: retention } = useRetention(projectId)
  const saveTerms = useSaveBillingTerms(projectId)
  const raiseAdvance = useRaiseAdvanceInvoice(projectId)
  const saveValuation = useSaveValuation(projectId)
  const submitValuation = useSubmitValuation(projectId)
  const certifyValuation = useCertifyValuation(projectId)
  const raiseInvoice = useRaiseValuationInvoice(projectId)
  const voidValuation = useVoidValuation(projectId)
  const releaseRetention = useReleaseRetention(projectId)
  const [termsDraft, setTermsDraft] = useState<TermsDraft | null>(null)
  const [draft, setDraft] = useState<ValuationDraft | null>(null)
  const [certify, setCertify] = useState<CertifyDraft | null>(null)
  const [voiding, setVoiding] = useState<Valuation | null>(null)
  const [voidReason, setVoidReason] = useState('')
  const [releasing, setReleasing] = useState<RetentionStage | null>(null)
  const money = (value: number) => formatCurrency(value, project.summary.currency)

  // The calculation preview follows the form without a request on every keystroke.
  const deferredDraft = useDeferredValue(draft)
  const { data: preview } = useValuationPreview(projectId, {
    workDoneToDate: deferredDraft && deferredDraft.workDoneToDate !== '' ? Number(deferredDraft.workDoneToDate) : null,
    materialsOnSite: deferredDraft && deferredDraft.materialsOnSite !== '' ? Number(deferredDraft.materialsOnSite) : null,
    valuationId: deferredDraft?.id ?? null,
  }, draft !== null)

  const inProgress = (valuations ?? []).some((x) => x.status === 'Draft' || x.status === 'Submitted')
  const latestLive = (valuations ?? []).find((x) => x.status !== 'Void')

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action()
      toast.success(success)
      return true
    } catch {
      return false // toast from the API client
    }
  }

  const saveTermsDraft = async () => {
    if (!termsDraft) return
    const ok = await run(() => saveTerms.mutateAsync({
      retentionPercent: numberOrNull(termsDraft.retentionPercent),
      retentionCapAmount: numberOrNull(termsDraft.retentionCapAmount),
      retentionReleaseAtCompletionPercent: numberOrNull(termsDraft.retentionReleaseAtCompletionPercent),
      defectsLiabilityMonths: numberOrNull(termsDraft.defectsLiabilityMonths),
      advancePaymentAmount: numberOrNull(termsDraft.advancePaymentAmount),
      advanceRecoveryPercent: numberOrNull(termsDraft.advanceRecoveryPercent),
    }), 'Billing terms saved')
    if (ok) setTermsDraft(null)
  }

  const saveDraft = async (andSubmit: boolean) => {
    if (!draft) return
    const ok = await run(async () => {
      const saved = await saveValuation.mutateAsync({
        ...(draft.id ? { valuationId: draft.id } : {}),
        periodEnd: draft.periodEnd,
        workDoneToDate: Number(draft.workDoneToDate),
        materialsOnSite: Number(draft.materialsOnSite || 0),
        notes: textOrNull(draft.notes),
      })
      if (andSubmit) await submitValuation.mutateAsync(saved.id)
    }, andSubmit ? 'Valuation submitted for certification' : 'Valuation saved as draft')
    if (ok) setDraft(null)
  }

  const saveCertificate = async () => {
    if (!certify) return
    const ok = await run(() => certifyValuation.mutateAsync({
      valuationId: certify.valuation.id,
      consultantId: certify.consultantId || null,
      certificateReference: certify.certificateReference.trim(),
      certifiedOn: certify.certifiedOn,
      certifiedAmount: Number(certify.certifiedAmount),
    }), `${certify.valuation.valuationNumber} certified`)
    if (ok) setCertify(null)
  }

  const columns: DataTableColumn<Valuation>[] = [
    { key: 'number', header: 'Valuation', cell: (row) => <div><div className="font-medium text-slate-900">{row.valuationNumber}</div><div className="text-xs text-slate-500">Work to {formatDate(row.periodEnd)}</div></div> },
    { key: 'gross', header: 'Gross to date', cell: (row) => money(row.grossValueToDate) },
    { key: 'due', header: 'Due', cell: (row) => <div><div>{money(row.certifiedAmount ?? row.amountDue)}</div>{row.certifiedAmount !== null && row.certifiedAmount !== row.amountDue ? <div className="text-xs text-slate-500">calculated {money(row.amountDue)}</div> : null}</div> },
    { key: 'certificate', header: 'Certificate', cell: (row) => (row.certificateReference ? <div><div>{row.certificateReference}</div><div className="text-xs text-slate-500">{[row.certifiedByConsultantName, row.certifiedOn ? formatDate(row.certifiedOn) : null].filter(Boolean).join(' · ')}</div></div> : '—') },
    { key: 'status', header: 'Status', cell: (row) => <div className="space-y-1"><StatusBadge status={row.status} />{row.invoiceId ? <Link to={`/finance/invoices/${row.invoiceId}`} className="block text-xs text-[var(--app-primary)] hover:underline">{row.invoiceNumber}{row.invoiceStatus === 'Void' ? ' (void)' : ''}</Link> : null}</div> },
    {
      key: 'actions',
      header: '',
      cell: (row) => {
        if (!canManage) return null
        const invoiceVoid = row.status === 'Invoiced' && row.invoiceStatus === 'Void'
        return (
          <div className="flex flex-wrap justify-end gap-2">
            {row.status === 'Draft' && !isClosed ? (
              <>
                <button type="button" className={smallButtonClass} onClick={() => setDraft({ id: row.id, periodEnd: row.periodEnd, workDoneToDate: String(row.workDoneToDate), materialsOnSite: String(row.materialsOnSite), notes: row.notes ?? '' })}>Edit</button>
                <button type="button" className={smallButtonClass} onClick={() => void run(() => submitValuation.mutateAsync(row.id), `${row.valuationNumber} submitted`)}>Submit</button>
              </>
            ) : null}
            {row.status === 'Submitted' && !isClosed ? (
              <button type="button" className={smallButtonClass} onClick={() => setCertify({ valuation: row, consultantId: project.consultants[0]?.consultantId ?? '', certificateReference: '', certifiedOn: todayInput(), certifiedAmount: String(row.amountDue) })}>Record certificate</button>
            ) : null}
            {row.status === 'Certified' || invoiceVoid ? (
              <button type="button" className={smallButtonClass} onClick={() => void run(() => raiseInvoice.mutateAsync(row.id), 'Draft invoice raised')}><FileText className="h-3.5 w-3.5" />Raise invoice</button>
            ) : null}
            {row.id === latestLive?.id && row.status !== 'Void' && (row.status !== 'Invoiced' || invoiceVoid) && !isClosed ? (
              <button type="button" className={smallButtonClass} onClick={() => { setVoiding(row); setVoidReason('') }}>Void</button>
            ) : null}
          </div>
        )
      },
    },
  ]

  return (
    <div className="space-y-5">
      <section className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-4">
          <div className="flex items-start justify-between gap-2">
            <h3 className="text-sm font-semibold text-slate-900">Billing terms</h3>
            {canManage && terms && !isClosed ? <button type="button" className={smallButtonClass} onClick={() => setTermsDraft(termsDraftFrom(terms))}>Edit</button> : null}
          </div>
          {terms ? (
            <div className="mt-2">
              <Row label="Retention" value={terms.retentionPercent ? `${terms.retentionPercent}%${terms.retentionCapAmount ? `, capped at ${money(terms.retentionCapAmount)}` : ''}` : 'None'} />
              <Row label="Released at completion" value={`${terms.retentionReleaseAtCompletionPercent}%, balance after ${terms.defectsLiabilityMonths ?? 0} months`} />
              <Row label="Advance" value={terms.advancePaymentAmount ? `${money(terms.advancePaymentAmount)}, recovered at ${terms.advanceRecoveryPercent}% of work` : 'None'} />
              {terms.advancePaymentAmount ? (
                <div className="mt-2 flex items-center justify-between gap-2 text-sm">
                  {terms.advanceInvoiceId && terms.advanceInvoiceStatus !== 'Void'
                    ? <Link to={`/finance/invoices/${terms.advanceInvoiceId}`} className="text-[var(--app-primary)] hover:underline">Advance invoice {terms.advanceInvoiceNumber}</Link>
                    : <span className="text-slate-500">Advance not invoiced yet</span>}
                  {canManage && !isClosed && (!terms.advanceInvoiceId || terms.advanceInvoiceStatus === 'Void')
                    ? <button type="button" className={smallButtonClass} disabled={raiseAdvance.isPending} onClick={() => void run(() => raiseAdvance.mutateAsync(undefined), 'Draft advance invoice raised')}>Raise advance invoice</button>
                    : null}
                </div>
              ) : null}
              {terms.calculationTermsLocked ? <p className="mt-2 text-xs text-slate-500">Retention and advance terms are fixed now that a valuation is certified.</p> : null}
            </div>
          ) : <p className="mt-2 text-sm text-slate-500">Loading…</p>}
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4">
          <h3 className="text-sm font-semibold text-slate-900">Retention</h3>
          {retention ? (
            <div className="mt-2">
              <Row label="Held" value={money(retention.held)} />
              <Row label="Handover" value={retention.handedOverAt ? formatDate(retention.handedOverAt) : 'Not yet'} />
              <Row label="Defects liability ends" value={retention.defectsLiabilityEnds ? formatDate(retention.defectsLiabilityEnds) : '—'} />
              {retention.releases.map((release) => (
                <Row key={release.id} label={`Released ${stageLabels[release.stage].toLowerCase()}`} value={`${money(release.amount)}${release.invoiceNumber ? ` · ${release.invoiceNumber}${release.invoiceStatus === 'Void' ? ' (void)' : ''}` : ''}`} />
              ))}
              {canManage ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {retention.completionDue > 0 ? <button type="button" className={smallButtonClass} onClick={() => setReleasing('PracticalCompletion')}>Release {money(retention.completionDue)} at completion</button> : null}
                  {retention.finalDue > 0 ? <button type="button" className={smallButtonClass} onClick={() => setReleasing('EndOfDefectsLiability')}>Release the {money(retention.finalDue)} balance</button> : null}
                </div>
              ) : null}
              {retention.held > 0 && retention.completionDue === 0 && retention.finalDue === 0 ? (
                <p className="mt-2 text-xs text-slate-500">{retention.handedOverAt ? 'The balance is released once the defects liability period ends.' : 'The first part is released at handover.'}</p>
              ) : null}
            </div>
          ) : <p className="mt-2 text-sm text-slate-500">Loading…</p>}
        </div>
      </section>

      <section className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-slate-600">Enter the totals from the QS's valuation and attach the BQ workings and signed certificate under Documents.</p>
          {canManage && !isClosed && project.features.valuations ? (
            <button type="button" className={primaryButtonClass} disabled={inProgress} title={inProgress ? 'Finish or void the valuation in progress first' : undefined}
              onClick={() => setDraft({ periodEnd: todayInput(), workDoneToDate: '', materialsOnSite: '0', notes: '' })}>
              <Plus className="h-4 w-4" />New valuation
            </button>
          ) : null}
        </div>
        <DataTable columns={columns} data={valuations ?? []} loading={isLoading} emptyTitle="No valuations" emptyDescription="Projects without valuations can be invoiced directly from Finance." />
      </section>

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit valuation' : 'New valuation'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={secondaryButtonClass} disabled={!draft?.periodEnd || draft.workDoneToDate === '' || saveValuation.isPending} onClick={() => void saveDraft(false)}>Save draft</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.periodEnd || draft.workDoneToDate === '' || saveValuation.isPending} onClick={() => void saveDraft(true)}>Save and submit</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div>
              <label className={labelClass} htmlFor="valuation-period">Work valued up to</label>
              <input id="valuation-period" type="date" value={draft.periodEnd} onChange={(event) => setDraft({ ...draft, periodEnd: event.target.value })} className={fieldClass} />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="valuation-work">Work done to date</label>
                <input id="valuation-work" type="number" min="0" step="0.01" inputMode="decimal" value={draft.workDoneToDate} placeholder={preview ? String(preview.suggestedWorkDoneToDate) : ''} onChange={(event) => setDraft({ ...draft, workDoneToDate: event.target.value })} className={fieldClass} />
                {preview && draft.workDoneToDate === '' ? (
                  <button type="button" className="mt-1 text-xs text-[var(--app-primary)] hover:underline" onClick={() => setDraft({ ...draft, workDoneToDate: String(preview.suggestedWorkDoneToDate) })}>
                    Start from progress × revised value ({money(preview.suggestedWorkDoneToDate)})
                  </button>
                ) : null}
              </div>
              <div>
                <label className={labelClass} htmlFor="valuation-mos">Materials on site</label>
                <input id="valuation-mos" type="number" min="0" step="0.01" inputMode="decimal" value={draft.materialsOnSite} onChange={(event) => setDraft({ ...draft, materialsOnSite: event.target.value })} className={fieldClass} />
              </div>
            </div>
            {preview && draft.workDoneToDate !== '' ? (
              <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
                <Row label="Gross value to date" value={money(preview.grossValueToDate)} />
                <Row label="Less retention" value={`− ${money(preview.retentionToDate)}`} />
                <Row label="Less advance recovered to date" value={`− ${money(preview.advanceRecoveredToDate)}`} />
                <Row label="Less previously certified" value={`− ${money(preview.previouslyCertified)}`} />
                <Row label="Amount due (before VAT)" value={money(preview.amountDue)} strong />
              </div>
            ) : null}
            <div>
              <label className={labelClass} htmlFor="valuation-notes">Notes</label>
              <textarea id="valuation-notes" rows={2} value={draft.notes} onChange={(event) => setDraft({ ...draft, notes: event.target.value })} className={textAreaClass} />
            </div>
          </div>
        ) : null}
      </FormDrawer>

      <FormDrawer
        open={certify !== null}
        title={certify ? `Certificate for ${certify.valuation.valuationNumber}` : ''}
        onClose={() => setCertify(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setCertify(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!certify?.certificateReference.trim() || certify.certifiedAmount === '' || certifyValuation.isPending} onClick={() => void saveCertificate()}>Record certificate</button>
          </div>
        }
      >
        {certify ? (
          <div className="space-y-4">
            <p className="text-sm text-slate-600">Calculated amount due: <strong>{money(certify.valuation.amountDue)}</strong>. Enter the amount on the signed certificate; that's what gets invoiced.</p>
            <div>
              <label className={labelClass} htmlFor="certify-consultant">Certified by</label>
              <select id="certify-consultant" value={certify.consultantId} onChange={(event) => setCertify({ ...certify, consultantId: event.target.value })} className={fieldClass}>
                <option value="">Not recorded</option>
                {project.consultants.map((link) => <option key={link.id} value={link.consultantId}>{link.firmName}{link.roleOnProject ? ` (${link.roleOnProject})` : ''}</option>)}
              </select>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="certify-ref">Certificate number</label>
                <input id="certify-ref" value={certify.certificateReference} onChange={(event) => setCertify({ ...certify, certificateReference: event.target.value })} placeholder="e.g. IPC 3" className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="certify-date">Certificate date</label>
                <input id="certify-date" type="date" max={todayInput()} value={certify.certifiedOn} onChange={(event) => setCertify({ ...certify, certifiedOn: event.target.value })} className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="certify-amount">Certified amount, before VAT ({project.summary.currency})</label>
              <input id="certify-amount" type="number" min="0" step="0.01" inputMode="decimal" value={certify.certifiedAmount} onChange={(event) => setCertify({ ...certify, certifiedAmount: event.target.value })} className={fieldClass} />
            </div>
          </div>
        ) : null}
      </FormDrawer>

      <FormDrawer
        open={termsDraft !== null}
        title="Billing terms"
        onClose={() => setTermsDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setTermsDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={saveTerms.isPending} onClick={() => void saveTermsDraft()}>Save</button>
          </div>
        }
      >
        {termsDraft ? (
          <div className="space-y-4">
            {terms?.calculationTermsLocked ? <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800">A valuation is certified, so retention %, cap and the advance can no longer change. The release split and defects period still can.</p> : null}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {([
                ['retentionPercent', 'Retention %', '0.1'],
                ['retentionCapAmount', `Retention cap (${project.summary.currency})`, '1'],
                ['retentionReleaseAtCompletionPercent', 'Released at completion %', '1'],
                ['defectsLiabilityMonths', 'Defects liability (months)', '1'],
                ['advancePaymentAmount', `Advance payment (${project.summary.currency})`, '1'],
                ['advanceRecoveryPercent', 'Advance recovery % of work', '0.1'],
              ] as const).map(([key, label, step]) => (
                <div key={key}>
                  <label className={labelClass} htmlFor={`terms-${key}`}>{label}</label>
                  <input id={`terms-${key}`} type="number" min="0" step={step} inputMode="decimal" value={termsDraft[key]} onChange={(event) => setTermsDraft({ ...termsDraft, [key]: event.target.value })} className={fieldClass} />
                </div>
              ))}
            </div>
            <p className="text-xs text-slate-500">Leave retention or the advance empty if the contract has none. Advance recovery applies to work done, not to materials on site, and stops once the advance is recovered.</p>
          </div>
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={voiding !== null}
        title={voiding ? `Void ${voiding.valuationNumber}` : ''}
        message="A voided valuation stays on record. The next valuation is worked out from the last certified one."
        confirmLabel="Void"
        danger
        confirmDisabled={!voidReason.trim() || voidValuation.isPending}
        onCancel={() => setVoiding(null)}
        onConfirm={() => void (voiding && run(() => voidValuation.mutateAsync({ valuationId: voiding.id, reason: voidReason.trim() }), `${voiding.valuationNumber} voided`).then(() => setVoiding(null)))}
      >
        <div className="text-left">
          <label className={labelClass} htmlFor="void-reason">Reason (required)</label>
          <textarea id="void-reason" rows={2} value={voidReason} onChange={(event) => setVoidReason(event.target.value)} className={textAreaClass} />
        </div>
      </ConfirmDialog>

      <ConfirmDialog
        open={releasing !== null}
        title="Release retention"
        message={releasing && retention ? `${stageLabels[releasing]}: a draft invoice for ${money(releasing === 'PracticalCompletion' ? retention.completionDue : retention.finalDue)} plus VAT will be raised for finance to send.` : ''}
        confirmLabel="Release and raise invoice"
        confirmDisabled={releaseRetention.isPending}
        onCancel={() => setReleasing(null)}
        onConfirm={() => void (releasing && run(() => releaseRetention.mutateAsync(releasing), 'Retention released: draft invoice raised').then(() => setReleasing(null)))}
      />
    </div>
  )
}
