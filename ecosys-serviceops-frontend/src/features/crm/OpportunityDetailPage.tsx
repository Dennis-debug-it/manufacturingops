import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { CheckCircle2, Lock } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { formatDate, formatDateTime, isPast } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { workTypeOptions } from '../work-orders/schemas'
import {
  useAddActivity,
  useChangeStage,
  useCompleteActivity,
  useConvertToClient,
  useConvertToQuotation,
  useDeleteOpportunity,
  useOpportunity,
  usePipelines,
  useReassignOpportunityOwner,
} from './api'
import { DealPeople } from './components/DealPeople'
import { ConvertToWorkOrderDrawer } from './components/ConvertToWorkOrderDrawer'
import { OpportunityFormDrawer } from './components/OpportunityFormDrawer'
import { OwnerSelect } from './components/OwnerSelect'
import { formatDealFieldValue } from './components/DealFieldInput'
import { activityTypeOptions, openStagesOf, opportunityParty, pipelineFor, stageName, tenderStatusLabels, type SalesStage } from './types'

const outlineButton = 'inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'
const primaryButton = 'inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'
const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'

export function OpportunityDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const canManage = usePermission('crm.manage')
  const canCreateWorkOrders = usePermission('work-orders.create')
  const canQuote = usePermission('finance.billable-work.manage')
  const { hasModule } = useEntitlements()
  const { data: opportunity, isLoading, isError } = useOpportunity(id || undefined)
  const changeStage = useChangeStage()
  const convertToClient = useConvertToClient(id)
  const convertToQuotation = useConvertToQuotation(id)
  const deleteOpportunity = useDeleteOpportunity()
  const addActivity = useAddActivity(id)
  const completeActivity = useCompleteActivity()
  const reassignOwner = useReassignOpportunityOwner(id)
  const { data: pipelines } = usePipelines()

  const [showEdit, setShowEdit] = useState(false)
  const [showWorkOrder, setShowWorkOrder] = useState(false)
  const [showLost, setShowLost] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [lostReason, setLostReason] = useState('')
  const [activityType, setActivityType] = useState('Call')
  const [activitySummary, setActivitySummary] = useState('')
  const [activityDue, setActivityDue] = useState('')
  const [activityDueTime, setActivityDueTime] = useState('09:00')

  if (isLoading) return <Spinner label="Loading opportunity" />
  if (isError || !opportunity) return <EmptyState title="Opportunity unavailable" description="This opportunity could not be loaded." />

  const isWon = opportunity.stage === 'Won'
  const isClosed = isWon || opportunity.stage === 'Lost'
  const pipeline = pipelineFor(pipelines, opportunity.pipelineId)
  const openStages = openStagesOf(pipeline)
  const nameOf = (stage: SalesStage) => stageName(stage, pipelines, opportunity.pipelineId)

  const moveTo = async (stage: SalesStage, reason?: string) => {
    // Stage rules (Settings › Sales): say what is missing and open the form to fill it in.
    const missing = opportunity.stageBlockers?.[stage]
    if (missing?.length) {
      toast.error(`Before moving to ${nameOf(stage)}, fill in: ${missing.join(', ')}`)
      if (!missing.every((item) => item === 'A quotation')) setShowEdit(true)
      return false
    }
    try {
      await changeStage.mutateAsync({ id, stage, lostReason: reason ?? null })
      toast.success(stage === 'Won' ? 'Deal won 🎉 — create the work order when you are ready' : `Moved to ${nameOf(stage)}`)
      return true
    } catch {
      return false
    }
  }

  const createQuote = async () => {
    try {
      if (!opportunity.clientId) await convertToClient.mutateAsync()
      const quote = await convertToQuotation.mutateAsync()
      toast.success(`Draft quote ${quote.quoteNumber} created`)
      navigate(`/finance/quotes/${quote.quotationId}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const logActivity = async () => {
    if (!activitySummary.trim()) {
      toast.error('Write what happened or what needs doing')
      return
    }
    try {
      await addActivity.mutateAsync({
        type: activityType,
        summary: activitySummary.trim(),
        dueAt: activityDue ? new Date(`${activityDue}T${activityDueTime || '09:00'}`).toISOString() : null,
      })
      setActivitySummary('')
      setActivityDue('')
      setActivityDueTime('09:00')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const workTypeLabel = workTypeOptions.find((option) => option.value === opportunity.serviceType)?.label

  return (
    <div className="space-y-4">
      <PageHeader
        title={opportunity.title}
        subtitle={pipelines && pipelines.length > 1 && opportunity.pipelineName ? `${opportunityParty(opportunity)} · ${opportunity.pipelineName} pipeline` : opportunityParty(opportunity)}
        breadcrumbs={[{ label: 'Sales Pipeline', to: '/crm' }, { label: opportunity.title }]}
        secondaryActions={<StatusBadge status={nameOf(opportunity.stage)} />}
        primaryAction={
          canManage ? (
            <div className="flex flex-wrap gap-2">
              {isWon && hasModule('operations') && canCreateWorkOrders ? (
                <button type="button" onClick={() => setShowWorkOrder(true)} className={primaryButton}>Create Work Order</button>
              ) : null}
              {!isClosed ? (
                <>
                  <button type="button" disabled={changeStage.isPending} onClick={() => void moveTo('Won')} className={primaryButton}>Mark Won</button>
                  <button type="button" onClick={() => setShowLost(true)} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">Mark Lost</button>
                </>
              ) : (
                <button type="button" disabled={changeStage.isPending} onClick={() => void moveTo(openStages[openStages.length - 1]?.key ?? 'Negotiation')} className={outlineButton}>Reopen</button>
              )}
              {opportunity.stage !== 'Lost' && hasModule('finance') && canQuote ? (
                <button type="button" disabled={convertToQuotation.isPending} onClick={() => void createQuote()} className={outlineButton}>Create Quote</button>
              ) : null}
              {!opportunity.clientId || opportunity.clientIsProspect ? (
                <button type="button" disabled={convertToClient.isPending} onClick={() => convertToClient.mutateAsync().then(() => toast.success(`${opportunityParty(opportunity)} is now a client`)).catch(() => undefined)} className={outlineButton}>Make Client</button>
              ) : null}
              <button type="button" onClick={() => setShowEdit(true)} className={outlineButton}>Edit</button>
              <button type="button" onClick={() => setShowDelete(true)} className={outlineButton}>Delete</button>
            </div>
          ) : undefined
        }
      />

      {!isClosed && canManage ? (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Pipeline stage">
          {openStages.map((stage) => (
            <button
              key={stage.key}
              type="button"
              disabled={changeStage.isPending}
              onClick={() => void moveTo(stage.key)}
              title={opportunity.stageBlockers?.[stage.key] ? `Needs: ${opportunity.stageBlockers[stage.key]?.join(', ')}` : undefined}
              className={cn(
                'inline-flex h-9 items-center gap-1.5 rounded-full border px-4 text-sm font-medium transition',
                stage.key === opportunity.stage ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
              )}
            >
              {stage.key !== opportunity.stage && opportunity.stageBlockers?.[stage.key] ? <Lock className="h-3 w-3" aria-label="Has requirements" /> : null}
              {stage.name}
            </button>
          ))}
        </div>
      ) : null}

      {isWon && opportunity.workOrders.length === 0 ? (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
          This deal is won. Create a work order to hand it over to operations{opportunity.clientId ? '' : ' — the client record is created for you'}.
        </div>
      ) : null}
      {opportunity.renewalOfContractId ? (
        <div className="rounded-xl border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
          Renewal of contract{' '}
          <Link to={`/contracts/${opportunity.renewalOfContractId}`} className="font-medium underline underline-offset-4">{opportunity.renewalContractNumber ?? 'contract'}</Link>.
          {' '}Renewing the contract wins this deal; letting it lapse marks it lost.
        </div>
      ) : null}
      {opportunity.sourceWorkOrderId ? (
        <div className="rounded-xl border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
          Flagged by a technician on work order{' '}
          <Link to={`/work-orders/${opportunity.sourceWorkOrderId}`} className="font-medium underline underline-offset-4">{opportunity.sourceWorkOrderNumber ?? 'work order'}</Link>.
        </div>
      ) : null}
      {opportunity.stage === 'Lost' && opportunity.lostReason ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">Lost: {opportunity.lostReason}</div>
      ) : null}

      <SummarySection title="Deal Overview" description="Customer, value and expected close.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            ['Customer', opportunity.clientId ? <span key="c"><Link to={`/clients/${opportunity.clientId}`} className="underline decoration-slate-300 underline-offset-4">{opportunity.clientName}</Link>{opportunity.clientIsProspect ? ' (prospect)' : ''}</span> : `${opportunityParty(opportunity)} (prospect)`],
            ['Site', opportunity.siteName ?? '—'],
            ['Contact', [opportunity.contactName, opportunity.contactPhone, opportunity.contactEmail].filter(Boolean).join(' · ') || '—'],
            ['Location', opportunity.location ?? '—'],
            ['Estimated value', opportunity.currency === opportunity.reportingCurrency ? formatCurrency(opportunity.estimatedValue, opportunity.currency) : (
              <span key="v">
                {formatCurrency(opportunity.estimatedValue, opportunity.currency)}
                <span className={cn('block text-xs', opportunity.convertedValue === null ? 'text-amber-700' : 'text-slate-500')}>
                  {opportunity.convertedValue === null ? `No ${opportunity.currency} rate yet — not in pipeline totals` : `≈ ${formatCurrency(opportunity.convertedValue, opportunity.reportingCurrency)}`}
                </span>
              </span>
            )],
            ['Probability', `${opportunity.probability}%`],
            ['Expected close', opportunity.expectedCloseDate ? formatDate(opportunity.expectedCloseDate) : '—'],
            ['Type of work', workTypeLabel ?? '—'],
            ['Source', opportunity.source ?? '—'],
            ['Owner', canManage ? (
              <OwnerSelect
                key="o"
                aria-label="Owner"
                value={opportunity.ownerUserId ?? ''}
                emptyLabel={opportunity.ownerUserId ? undefined : 'Unassigned'}
                disabled={reassignOwner.isPending}
                onChange={(ownerUserId) => {
                  if (!ownerUserId) return
                  reassignOwner.mutateAsync(ownerUserId).then((updated) => toast.success(`Reassigned to ${updated.ownerName}`)).catch(() => undefined)
                }}
                className="-my-1 h-9 w-full rounded-lg border border-slate-200 bg-white px-2 text-sm outline-none focus:border-[var(--app-primary)]"
              />
            ) : (opportunity.ownerName ?? '—')],
            ['Created', formatDate(opportunity.createdAt)],
            ['Won', opportunity.wonAt ? formatDate(opportunity.wonAt) : '—'],
            ...(opportunity.customFields ?? []).map((field) => [field.label, formatDealFieldValue(field.fieldType, field.value)]),
          ].map(([label, value]) => (
            <div key={label as string} className="rounded-xl border border-slate-200 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 break-words text-sm text-slate-900">{value}</div>
            </div>
          ))}
        </div>
        {opportunity.description ? <p className="whitespace-pre-wrap text-sm text-slate-700">{opportunity.description}</p> : null}
      </SummarySection>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <div className="xl:col-span-2">
          <SummarySection title="Activity" description="Calls, meetings, site visits and follow-ups.">
            {canManage ? (
              <div className="space-y-2 rounded-xl border border-slate-200 p-3">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-[10rem_1fr]">
                  <select aria-label="Activity type" value={activityType} onChange={(event) => setActivityType(event.target.value)} className={fieldClass}>
                    {activityTypeOptions.map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                  <input aria-label="Activity summary" value={activitySummary} onChange={(event) => setActivitySummary(event.target.value)} className={fieldClass} placeholder="What happened, or what needs doing next" />
                </div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <label className="flex items-center gap-2 text-sm text-slate-600">
                    Follow up on
                    <input type="date" value={activityDue} onChange={(event) => setActivityDue(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
                    {activityDue ? (
                      <input type="time" aria-label="Follow-up time" value={activityDueTime} onChange={(event) => setActivityDueTime(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
                    ) : null}
                  </label>
                  <button type="button" disabled={addActivity.isPending} onClick={() => void logActivity()} className={primaryButton}>Log Activity</button>
                </div>
                {activityDue ? (
                  <p className="text-xs text-slate-500">
                    {opportunity.ownerName ?? 'The owner'} gets a reminder by email and push notification at that time, then up to twice more a day apart until it is marked done.
                  </p>
                ) : null}
              </div>
            ) : null}
            <ol className="divide-y divide-slate-100">
              {opportunity.activities.length === 0 ? <li className="py-4 text-sm text-slate-500">No activity yet.</li> : null}
              {opportunity.activities.map((activity) => {
                const isOpenFollowUp = Boolean(activity.dueAt && !activity.completedAt)
                const overdue = isOpenFollowUp && isPast(activity.dueAt!)
                return (
                  <li key={activity.id} className="flex items-start justify-between gap-3 py-3">
                    <div className="min-w-0">
                      <div className="text-sm text-slate-900">
                        <span className={cn('mr-2 text-xs font-semibold uppercase tracking-wide', activity.type === 'System' ? 'text-slate-400' : 'text-[var(--app-primary)]')}>
                          {activityTypeOptions.find((option) => option.value === activity.type)?.label ?? activity.type}
                        </span>
                        {activity.summary}
                      </div>
                      <div className="mt-0.5 text-xs text-slate-500">
                        {formatDateTime(activity.occurredAt)}{activity.loggedByName ? ` · ${activity.loggedByName}` : ''}
                        {activity.dueAt ? (
                          <span className={cn('ml-2', overdue ? 'font-medium text-amber-700' : activity.completedAt ? 'text-emerald-700' : '')}>
                            {activity.completedAt ? `Done ${formatDate(activity.completedAt)}` : `Due ${formatDate(activity.dueAt)}`}
                          </span>
                        ) : null}
                      </div>
                    </div>
                    {canManage && isOpenFollowUp ? (
                      <button type="button" onClick={() => completeActivity.mutate(activity.id)} className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50">
                        <CheckCircle2 className="h-3.5 w-3.5" />Done
                      </button>
                    ) : null}
                  </li>
                )
              })}
            </ol>
          </SummarySection>
        </div>

        <div className="space-y-4">
          <DealPeople opportunity={opportunity} canManage={canManage} />
          <SummarySection title="Work Orders" description="Jobs raised from this deal.">
            {opportunity.workOrders.length === 0 ? (
              <p className="text-sm text-slate-500">{isWon ? 'None yet.' : 'Win the deal to create work orders from it.'}</p>
            ) : (
              <ul className="divide-y divide-slate-100">
                {opportunity.workOrders.map((workOrder) => (
                  <li key={workOrder.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                    <Link to={`/work-orders/${workOrder.id}`} className="min-w-0">
                      <div className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4">{workOrder.workOrderNumber}</div>
                      <div className="truncate text-slate-500">{workOrder.title}</div>
                    </Link>
                    <StatusBadge status={workOrder.status} />
                  </li>
                ))}
              </ul>
            )}
          </SummarySection>

          {opportunity.tenders.length > 0 ? (
            <SummarySection title="RFPs & RFQs" description="The tender this deal is bidding on.">
              <ul className="divide-y divide-slate-100">
                {opportunity.tenders.map((tender) => (
                  <li key={tender.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                    <Link to={`/crm/tenders/${tender.id}`} className="min-w-0">
                      <div className="text-slate-900 underline decoration-slate-300 underline-offset-4">{tender.type} {tender.reference ?? ''}</div>
                      <div className="truncate text-slate-500">Due {formatDate(tender.submissionDeadline)}</div>
                    </Link>
                    <StatusBadge status={tenderStatusLabels[tender.status]} />
                  </li>
                ))}
              </ul>
            </SummarySection>
          ) : null}

          {hasModule('finance') ? (
            <SummarySection title="Quotes" description="Quotations prepared for this deal.">
              {opportunity.quotations.length === 0 ? (
                <p className="text-sm text-slate-500">No quotes yet.</p>
              ) : (
                <ul className="divide-y divide-slate-100">
                  {opportunity.quotations.map((quote) => (
                    <li key={quote.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                      <Link to={`/finance/quotes/${quote.id}`} className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4">{quote.quoteNumber}</Link>
                      <span className="text-slate-600">{formatCurrency(quote.totalAmount, quote.currency)}</span>
                      <StatusBadge status={quote.status} />
                    </li>
                  ))}
                </ul>
              )}
            </SummarySection>
          ) : null}
        </div>
      </div>

      <OpportunityFormDrawer open={showEdit} onClose={() => setShowEdit(false)} opportunity={opportunity} />
      <ConvertToWorkOrderDrawer open={showWorkOrder} onClose={() => setShowWorkOrder(false)} opportunity={opportunity} />

      <FormDrawer
        open={showLost}
        title="Mark Deal as Lost"
        onClose={() => setShowLost(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowLost(false)} className={outlineButton}>Cancel</button>
            <button
              type="button"
              disabled={!lostReason.trim() || changeStage.isPending}
              onClick={async () => {
                if (await moveTo('Lost', lostReason.trim())) {
                  setShowLost(false)
                  setLostReason('')
                }
              }}
              className="inline-flex h-11 items-center rounded-lg bg-rose-600 px-4 text-sm font-medium text-white transition hover:bg-rose-700 disabled:opacity-60"
            >
              Mark Lost
            </button>
          </div>
        }
      >
        <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="lost-reason">Why was it lost?</label>
        <textarea id="lost-reason" value={lostReason} onChange={(event) => setLostReason(event.target.value)} rows={4} maxLength={500} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="e.g. Price, timing, went with a competitor" />
      </FormDrawer>

      <ConfirmDialog
        open={showDelete}
        title="Delete opportunity?"
        message="This removes the deal and its activity log. Deals with quotes, work orders or an RFP/RFQ can't be deleted — mark them lost instead."
        confirmLabel="Delete"
        danger
        onCancel={() => setShowDelete(false)}
        onConfirm={async () => {
          try {
            await deleteOpportunity.mutateAsync(id)
            toast.success('Opportunity deleted')
            navigate('/crm')
          } catch {
            setShowDelete(false)
          }
        }}
      />
    </div>
  )
}
