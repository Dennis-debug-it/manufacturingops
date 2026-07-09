import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { useLeads, useUpdateLeadStatus } from './api'
import type { LeadListItem, LeadStatus } from './types'

const stages: LeadStatus[] = ['New', 'Contacted', 'Qualified', 'Demo Scheduled', 'Converted to Workspace', 'Not a Fit']

function LeadCard({ lead, onDragStart }: { lead: LeadListItem; onDragStart: (id: string) => void }) {
  return (
    <Link
      to={`/platform/crm/leads/${lead.id}`}
      draggable
      onDragStart={() => onDragStart(lead.id)}
      className="block cursor-grab rounded-xl border border-white/10 bg-white/5 p-3 text-sm text-white transition hover:border-white/30 active:cursor-grabbing"
    >
      <div className="font-medium">{lead.companyName}</div>
      <div className="mt-1 text-xs text-slate-400">{lead.contactPersonName}</div>
    </Link>
  )
}

export function PipelinePage() {
  const { data, isLoading, isError } = useLeads()
  const [dragId, setDragId] = useState<string | null>(null)
  const [pendingMove, setPendingMove] = useState<{ id: string; status: LeadStatus; previous: LeadStatus } | null>(null)

  if (isLoading) {
    return <Spinner label="Loading pipeline" />
  }

  if (isError) {
    return <div className="rounded-2xl border border-dashed border-white/20 bg-white/5 p-8 text-center text-sm text-slate-300">Leads could not be loaded.</div>
  }

  const leads = data ?? []

  return (
    <div className="space-y-6 text-white">
      <PageHeader title="CRM Pipeline" subtitle="Lead flow from demo request to conversion. Drag a card to change its stage." />
      <div className="grid grid-cols-1 gap-4 overflow-x-auto pb-2 lg:grid-cols-6">
        {stages.map((stage) => {
          const stageLeads = leads.filter((lead) => lead.status === stage)
          return (
            <div
              key={stage}
              onDragOver={(event) => event.preventDefault()}
              onDrop={() => {
                if (dragId) {
                  const lead = leads.find((item) => item.id === dragId)
                  if (lead && lead.status !== stage) {
                    setPendingMove({ id: dragId, status: stage, previous: lead.status })
                  }
                }
                setDragId(null)
              }}
              className="min-w-[220px] space-y-3 rounded-2xl border border-white/10 bg-white/5 p-3"
            >
              <div className="flex items-center justify-between">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-300">{stage}</h3>
                <span className="rounded-full bg-white/10 px-2 py-0.5 text-xs text-slate-300">{stageLeads.length}</span>
              </div>
              <div className="space-y-2">
                {stageLeads.map((lead) => (
                  <LeadCard key={lead.id} lead={lead} onDragStart={setDragId} />
                ))}
              </div>
            </div>
          )
        })}
      </div>

      {pendingMove ? <StageChangeHandler move={pendingMove} onDone={() => setPendingMove(null)} /> : null}
    </div>
  )
}

function StageChangeHandler({ move, onDone }: { move: { id: string; status: LeadStatus; previous: LeadStatus }; onDone: () => void }) {
  const updateStatus = useUpdateLeadStatus(move.id)
  const [confirmed, setConfirmed] = useState(false)

  if (!confirmed) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4" onClick={onDone}>
        <div className="w-full max-w-sm rounded-2xl border border-white/10 bg-slate-900 p-6 text-white shadow-2xl" onClick={(event) => event.stopPropagation()}>
          <h3 className="text-lg font-semibold">Move Lead</h3>
          <p className="mt-2 text-sm text-slate-300">Move this lead from <strong>{move.previous}</strong> to <strong>{move.status}</strong>?</p>
          <div className="mt-6 flex justify-end gap-2">
            <button type="button" onClick={onDone} className="inline-flex h-10 items-center rounded-lg border border-white/20 px-4 text-sm font-medium text-white hover:bg-white/10">Cancel</button>
            <button
              type="button"
              onClick={async () => {
                setConfirmed(true)
                try {
                  await updateStatus.mutateAsync(move.status)
                  toast.success(`Moved to ${move.status}`)
                } catch {
                  // interceptor surfaces the error toast
                } finally {
                  onDone()
                }
              }}
              className="inline-flex h-10 items-center rounded-lg bg-white px-4 text-sm font-medium text-slate-900 hover:bg-slate-100"
            >
              Confirm
            </button>
          </div>
        </div>
      </div>
    )
  }
  return null
}
