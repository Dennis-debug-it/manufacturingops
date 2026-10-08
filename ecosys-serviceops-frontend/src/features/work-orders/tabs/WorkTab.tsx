import { useState, type ReactNode } from 'react'
import { CheckCircle2, ChevronDown, Circle } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import { useChecklist, useExecutionBundle } from '../api'
import { usePartsRequestsForWorkOrder } from '../partsRequestsApi'
import { CrewDispatchPanel } from '../components/CrewDispatchPanel'
import { hasRecordedArrival } from '../dispatchGuards'
import type { WorkOrder } from '../types'
import { ExecutionTab } from './ExecutionTab'
import { EvidenceTab } from './EvidenceTab'
import { PartsTab } from './PartsTab'
import { SignaturesTab } from './SignaturesTab'

type StepId = 'onsite' | 'checklist' | 'photos' | 'parts' | 'signatures'

interface Step {
  id: StepId
  label: string
  done: boolean
  /** Short state shown next to the step title, e.g. "3 photos". */
  summary: string
  /** The server refuses completion until this step is done. */
  required?: boolean
  content: ReactNode
}

const OPEN_PARTS_STATUSES = new Set(['Pending', 'Approved', 'Dispatched'])

export function WorkTab({ workOrder }: { workOrder: WorkOrder }) {
  const { data: checklist } = useChecklist(workOrder.id)
  const { data: bundle } = useExecutionBundle(workOrder.id)
  const { data: partsRequests } = usePartsRequestsForWorkOrder(workOrder.id)

  const arrived = Boolean(workOrder.arrivalAt) || hasRecordedArrival(workOrder.technicianAssignments)
  const checklistItems = checklist ?? []
  const requiredItems = checklistItems.filter((item) => item.isRequired)
  const answeredRequired = requiredItems.filter((item) => item.isCompleted).length
  const answered = checklistItems.filter((item) => item.isCompleted).length
  const workDone = bundle?.notes.workDone?.trim() || workOrder.workDoneNotes?.trim()
  const photoCount = bundle?.photos.length ?? 0
  const openParts = (partsRequests ?? []).filter((request) => OPEN_PARTS_STATUSES.has(request.status)).length
  const clientSigned = (bundle?.signatures ?? []).some((signature) => signature.signatureType === 'Client')
  const signatureCount = bundle?.signatures.length ?? 0

  const steps: Step[] = [
    {
      id: 'onsite',
      label: 'Technician on site',
      required: true,
      done: arrived,
      summary: arrived ? 'Arrival recorded' : 'Record arrival',
      content: <CrewDispatchPanel workOrder={workOrder} />,
    },
    {
      id: 'checklist',
      label: checklistItems.length > 0 ? 'Checklist' : 'Work performed',
      done: checklistItems.length > 0 ? requiredItems.length === 0 ? answered === checklistItems.length : answeredRequired === requiredItems.length : Boolean(workDone),
      summary: checklistItems.length > 0 ? `${answered} of ${checklistItems.length} answered` : workDone ? 'Notes saved' : 'Not recorded',
      content: <ExecutionTab workOrderId={workOrder.id} />,
    },
    {
      id: 'photos',
      label: 'Photos',
      done: photoCount > 0,
      summary: photoCount > 0 ? `${photoCount} photo${photoCount === 1 ? '' : 's'}` : 'None yet',
      content: <EvidenceTab workOrderId={workOrder.id} />,
    },
    {
      id: 'parts',
      label: 'Parts',
      done: openParts === 0,
      summary: openParts > 0 ? `${openParts} awaiting delivery` : (partsRequests ?? []).length > 0 ? 'All delivered' : 'None requested',
      content: <PartsTab workOrderId={workOrder.id} />,
    },
    {
      id: 'signatures',
      label: 'Client signature',
      done: clientSigned,
      summary: clientSigned ? 'Signed' : signatureCount > 0 ? `${signatureCount} signature${signatureCount === 1 ? '' : 's'}, no client` : 'Not signed',
      content: <SignaturesTab workOrderId={workOrder.id} />,
    },
  ]

  const firstOpenStep = steps.find((step) => !step.done)?.id ?? null
  const [openSteps, setOpenSteps] = useState<Set<StepId> | null>(null)
  // Until the user toggles anything, only the next unfinished step is expanded.
  const isOpen = (id: StepId) => (openSteps ? openSteps.has(id) : id === firstOpenStep)
  const toggle = (id: StepId) => {
    setOpenSteps((current) => {
      const next = new Set(current ?? (firstOpenStep ? [firstOpenStep] : []))
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }
  const jumpTo = (id: StepId) => {
    setOpenSteps((current) => new Set([...(current ?? (firstOpenStep ? [firstOpenStep] : [])), id]))
    document.getElementById(`work-step-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <div className="space-y-3">
      <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-3">
        <ul className="flex flex-wrap gap-2" aria-label="Completion readiness">
          {steps.map((step) => (
            <li key={step.id}>
              <button
                type="button"
                onClick={() => jumpTo(step.id)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition hover:bg-slate-50',
                  step.done ? 'border-emerald-200 text-emerald-700' : step.required ? 'border-amber-300 text-amber-800' : 'border-slate-200 text-slate-600',
                )}
              >
                {step.done ? <CheckCircle2 className="h-3.5 w-3.5" /> : <Circle className="h-3.5 w-3.5" />}
                {step.label}
              </button>
            </li>
          ))}
        </ul>
        {['Completed', 'Closed', 'Cancelled'].includes(workOrder.status) ? null : <p className="mt-2 text-xs text-slate-500">
          {arrived
            ? 'Ready to complete. You will be asked for work-done notes; the other steps are recommended.'
            : 'Completing needs a recorded arrival (or an admin override) and work-done notes. The other steps are recommended.'}
        </p>}
      </div>

      {steps.map((step, index) => (
        <section key={step.id} id={`work-step-${step.id}`} className="scroll-mt-4 rounded-[var(--radius-card)] border border-slate-200 bg-white">
          <button
            type="button"
            onClick={() => toggle(step.id)}
            aria-expanded={isOpen(step.id)}
            className="flex w-full items-center gap-3 px-4 py-3 text-left"
          >
            <span className={cn('flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold', step.done ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-600')}>
              {step.done ? <CheckCircle2 className="h-4 w-4" /> : index + 1}
            </span>
            <span className="flex-1 text-sm font-semibold text-slate-900">
              {step.label}
              {step.required && !step.done ? <span className="ml-2 text-xs font-medium text-amber-700">Required</span> : null}
            </span>
            <span className="text-xs text-slate-500">{step.summary}</span>
            <ChevronDown className={cn('h-4 w-4 text-slate-400 transition-transform', isOpen(step.id) && 'rotate-180')} />
          </button>
          {isOpen(step.id) ? <div className="border-t border-slate-100 p-4">{step.content}</div> : null}
        </section>
      ))}
    </div>
  )
}
