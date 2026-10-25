import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useChangeTenderStatus, useCreateOpportunityFromTender, useDeleteTender, useReassignTenderOwner, useTender } from './api'
import { OwnerSelect } from './components/OwnerSelect'
import { TenderDocuments } from './components/TenderDocuments'
import { TenderFormDrawer } from './components/TenderFormDrawer'
import { DeadlineBadge } from './TendersPage'
import { TENDER_REASON_STATUSES, stageLabels, tenderStatusLabels, type TenderStatus } from './types'

const outlineButton = 'inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'
const primaryButton = 'inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'
const dangerButton = 'inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50'

const statusPrompts: Partial<Record<TenderStatus, { title: string; notesLabel: string; placeholder: string }>> = {
  Submitted: { title: 'Mark as Submitted', notesLabel: '', placeholder: '' },
  Won: { title: 'Mark as Won', notesLabel: 'Award notes (optional)', placeholder: 'e.g. Award letter ref, start date' },
  Lost: { title: 'Mark as Lost', notesLabel: 'Why was it lost?', placeholder: 'e.g. Price, technical score, awarded to a competitor' },
  NoBid: { title: 'Decide not to bid', notesLabel: 'Why not bid?', placeholder: 'e.g. Outside our scope, deadline too tight' },
  Cancelled: { title: 'Mark as Cancelled', notesLabel: 'What happened?', placeholder: 'e.g. Issuer withdrew the tender' },
}

export function TenderDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const canManage = usePermission('crm.manage')
  const { data: tender, isLoading, isError } = useTender(id || undefined)
  const changeStatus = useChangeTenderStatus(id)
  const createOpportunity = useCreateOpportunityFromTender(id)
  const reassignOwner = useReassignTenderOwner(id)
  const deleteTender = useDeleteTender()
  const [showEdit, setShowEdit] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [pending, setPending] = useState<TenderStatus | null>(null)
  const [notes, setNotes] = useState('')
  const [submittedValue, setSubmittedValue] = useState('')

  if (isLoading) return <Spinner label="Loading request" />
  if (isError || !tender) return <EmptyState title="Request unavailable" description="This RFP/RFQ could not be loaded." />

  const closed = ['Won', 'Lost', 'NoBid', 'Cancelled'].includes(tender.status)
  const beforeSubmission = ['Received', 'Reviewing', 'Preparing'].includes(tender.status)

  const moveTo = async (status: TenderStatus, extra?: { notes?: string | null; submittedValue?: number | null }) => {
    try {
      await changeStatus.mutateAsync({ status, ...extra })
      toast.success(`Marked ${tenderStatusLabels[status].toLowerCase()}`)
      return true
    } catch {
      return false
    }
  }

  const openPrompt = (status: TenderStatus) => {
    setNotes('')
    setSubmittedValue(String(tender.submittedValue ?? tender.estimatedValue ?? ''))
    setPending(status)
  }

  const prompt = pending ? statusPrompts[pending] : undefined
  const needsReason = pending ? TENDER_REASON_STATUSES.includes(pending) : false

  const startBid = async () => {
    try {
      const updated = await createOpportunity.mutateAsync()
      toast.success('Opportunity added to the pipeline')
      if (updated.opportunityId) navigate(`/crm/opportunities/${updated.opportunityId}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={tender.title}
        subtitle={`${tender.type}${tender.reference ? ` · ${tender.reference}` : ''} · ${tender.clientName ?? tender.issuerName ?? ''}`}
        breadcrumbs={[{ label: 'Sales Pipeline', to: '/crm' }, { label: 'RFPs & RFQs', to: '/crm/tenders' }, { label: tender.reference ?? tender.title }]}
        secondaryActions={<StatusBadge status={tenderStatusLabels[tender.status]} />}
        primaryAction={
          canManage ? (
            <div className="flex flex-wrap gap-2">
              {!closed && !tender.opportunityId ? (
                <button type="button" disabled={createOpportunity.isPending} onClick={() => void startBid()} className={primaryButton}>Bid — Create Opportunity</button>
              ) : null}
              {beforeSubmission ? <button type="button" onClick={() => openPrompt('Submitted')} className={tender.opportunityId ? primaryButton : outlineButton}>Mark Submitted</button> : null}
              {tender.status === 'Submitted' ? (
                <>
                  <button type="button" onClick={() => openPrompt('Won')} className={primaryButton}>Won</button>
                  <button type="button" onClick={() => openPrompt('Lost')} className={dangerButton}>Lost</button>
                </>
              ) : null}
              {beforeSubmission ? <button type="button" onClick={() => openPrompt('NoBid')} className={dangerButton}>No Bid</button> : null}
              {!closed ? <button type="button" onClick={() => openPrompt('Cancelled')} className={outlineButton}>Cancelled</button> : null}
              {closed ? (
                <button type="button" disabled={changeStatus.isPending} onClick={() => void moveTo(tender.submittedAt ? 'Submitted' : 'Reviewing')} className={outlineButton}>Reopen</button>
              ) : null}
              <button type="button" onClick={() => setShowEdit(true)} className={outlineButton}>Edit</button>
              {!tender.opportunityId ? <button type="button" onClick={() => setShowDelete(true)} className={outlineButton}>Delete</button> : null}
            </div>
          ) : undefined
        }
      />

      {canManage && beforeSubmission ? (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Progress">
          {(['Received', 'Reviewing', 'Preparing'] as TenderStatus[]).map((status) => (
            <button
              key={status}
              type="button"
              disabled={changeStatus.isPending}
              onClick={() => void moveTo(status)}
              className={cn('h-9 rounded-full border px-4 text-sm font-medium transition', status === tender.status ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50')}
            >
              {tenderStatusLabels[status]}
            </button>
          ))}
        </div>
      ) : null}

      {tender.status === 'Won' && tender.opportunityId ? (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
          Awarded. The linked deal is marked won —{' '}
          <Link to={`/crm/opportunities/${tender.opportunityId}`} className="font-medium underline underline-offset-4">open it to create the work order</Link>.
        </div>
      ) : null}
      {tender.status === 'Won' && !tender.opportunityId ? (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">Awarded. Reopen it and create an opportunity to hand the job over to operations.</div>
      ) : null}
      {tender.outcomeNotes && closed ? (
        <div className={cn('rounded-xl border px-4 py-3 text-sm', tender.status === 'Won' ? 'border-slate-200 bg-white text-slate-700' : 'border-rose-200 bg-rose-50 text-rose-900')}>
          {tenderStatusLabels[tender.status]}: {tender.outcomeNotes}
        </div>
      ) : null}

      <SummarySection title="Request Details" description="Issuer, deadlines and value.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            ['Issued by', tender.clientId ? <Link key="c" to={`/clients/${tender.clientId}`} className="underline decoration-slate-300 underline-offset-4">{tender.clientName}</Link> : (tender.issuerName ?? '—')],
            ['Contact', [tender.contactName, tender.contactPhone, tender.contactEmail].filter(Boolean).join(' · ') || '—'],
            ['Received', formatDate(tender.receivedAt)],
            ['Clarifications by', tender.clarificationDeadline ? formatDate(tender.clarificationDeadline) : '—'],
            ['Submission deadline', <span key="d"><DeadlineBadge deadline={tender.submissionDeadline} status={tender.status} /><span className="block text-xs text-slate-500">{formatDateTime(tender.submissionDeadline)}</span></span>],
            ['How to submit', tender.submissionMethod ?? '—'],
            ['Bid bond', tender.bidBondRequired ? (tender.bidBondAmount != null ? formatCurrency(tender.bidBondAmount, tender.currency) : 'Required') : 'Not required'],
            ['Estimated value', (
              <span key="v">
                {formatCurrency(tender.estimatedValue, tender.currency)}
                {tender.currency !== tender.reportingCurrency ? (
                  <span className={cn('block text-xs', tender.convertedValue === null ? 'text-amber-700' : 'text-slate-500')}>
                    {tender.convertedValue === null ? `No ${tender.currency} rate yet` : `≈ ${formatCurrency(tender.convertedValue, tender.reportingCurrency)}`}
                  </span>
                ) : null}
              </span>
            )],
            ['Submitted', tender.submittedAt ? `${formatDate(tender.submittedAt)} · ${formatCurrency(tender.submittedValue ?? 0, tender.currency)}` : '—'],
            ['Decided', tender.decidedAt ? formatDate(tender.decidedAt) : '—'],
            ['Opportunity', tender.opportunityId ? (
              <Link key="o" to={`/crm/opportunities/${tender.opportunityId}`} className="underline decoration-slate-300 underline-offset-4">
                {tender.opportunityTitle}{tender.opportunityStage ? ` (${stageLabels[tender.opportunityStage]})` : ''}
              </Link>
            ) : 'None yet'],
            ['Owner', canManage ? (
              <OwnerSelect
                key="ow"
                aria-label="Owner"
                value={tender.ownerUserId ?? ''}
                emptyLabel={tender.ownerUserId ? undefined : 'Unassigned'}
                disabled={reassignOwner.isPending}
                onChange={(ownerUserId) => {
                  if (!ownerUserId) return
                  reassignOwner.mutateAsync(ownerUserId).then((updated) => toast.success(`Reassigned to ${updated.ownerName}`)).catch(() => undefined)
                }}
                className="-my-1 h-9 w-full rounded-lg border border-slate-200 bg-white px-2 text-sm outline-none focus:border-[var(--app-primary)]"
              />
            ) : (tender.ownerName ?? '—')],
          ].map(([label, value]) => (
            <div key={label as string} className="rounded-xl border border-slate-200 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 break-words text-sm text-slate-900">{value}</div>
            </div>
          ))}
        </div>
        {tender.description ? <p className="whitespace-pre-wrap text-sm text-slate-700">{tender.description}</p> : null}
      </SummarySection>

      <SummarySection title="Documents" description="Tender pack, clarifications and your submission.">
        <TenderDocuments tenderId={tender.id} canManage={canManage} />
      </SummarySection>

      <TenderFormDrawer open={showEdit} onClose={() => setShowEdit(false)} tender={tender} />

      <FormDrawer
        open={Boolean(pending && prompt)}
        title={prompt?.title ?? ''}
        onClose={() => setPending(null)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setPending(null)} className={outlineButton}>Cancel</button>
            <button
              type="button"
              disabled={changeStatus.isPending || (needsReason && !notes.trim())}
              onClick={async () => {
                if (!pending) return
                const ok = await moveTo(pending, {
                  notes: notes.trim() || null,
                  submittedValue: pending === 'Submitted' && submittedValue ? Number(submittedValue) : null,
                })
                if (ok) setPending(null)
              }}
              className={pending && ['Lost', 'NoBid', 'Cancelled'].includes(pending) ? 'inline-flex h-11 items-center rounded-lg bg-rose-600 px-4 text-sm font-medium text-white transition hover:bg-rose-700 disabled:opacity-60' : primaryButton}
            >
              Confirm
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          {pending === 'Submitted' ? (
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="submitted-value">Amount bid ({tender.currency})</label>
              <input id="submitted-value" type="number" min="0" value={submittedValue} onChange={(event) => setSubmittedValue(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              <p className="mt-1.5 text-xs text-slate-500">Updates the linked opportunity&apos;s value too.</p>
            </div>
          ) : null}
          {prompt?.notesLabel ? (
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="status-notes">{prompt.notesLabel}</label>
              <textarea id="status-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} maxLength={1000} placeholder={prompt.placeholder} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          ) : null}
          {tender.opportunityId && pending && pending !== 'Submitted' ? (
            <p className="text-sm text-slate-500">The linked opportunity will be marked {pending === 'Won' ? 'won' : 'lost'} as well.</p>
          ) : null}
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={showDelete}
        title="Delete this request?"
        message="This removes the RFP/RFQ record. Requests with documents or a linked opportunity can't be deleted — mark them cancelled instead."
        confirmLabel="Delete"
        danger
        onCancel={() => setShowDelete(false)}
        onConfirm={async () => {
          try {
            await deleteTender.mutateAsync(id)
            toast.success('Request deleted')
            navigate('/crm/tenders')
          } catch {
            setShowDelete(false)
          }
        }}
      />
    </div>
  )
}
