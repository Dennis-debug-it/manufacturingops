import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useLeads, useUpdateLeadStatus } from './api'
import type { LeadListItem, LeadStatus } from './types'
import { CrmSubNav } from './CrmSubNav'

const stages: LeadStatus[] = ['New', 'Contacted', 'Qualified', 'Demo Scheduled', 'Converted to Workspace', 'Not a Fit']

function LeadCard({ lead, onDragStart }: { lead: LeadListItem; onDragStart: (id: string) => void }) {
  return (
    <Link
      to={`/platform/crm/leads/${lead.id}`}
      draggable
      onDragStart={() => onDragStart(lead.id)}
      className="block cursor-grab rounded-xl border border-slate-200 bg-white p-3 text-sm shadow-sm transition hover:border-slate-300 active:cursor-grabbing"
    >
      <div className="font-medium text-slate-900">{lead.companyName}</div>
      <div className="mt-1 text-xs text-slate-500">{lead.contactPersonName}</div>
    </Link>
  )
}

export function PipelinePage() {
  const { data, isLoading, isError } = useLeads()
  const [dragId, setDragId] = useState<string | null>(null)
  const [pendingMove, setPendingMove] = useState<{ id: string; status: LeadStatus; previous: LeadStatus } | null>(null)
  const updateStatus = useUpdateLeadStatus(pendingMove?.id ?? '')

  if (isLoading) {
    return <Spinner label="Loading pipeline" />
  }

  if (isError) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Leads could not be loaded.</div>
  }

  const leads = data ?? []

  const confirmMove = async () => {
    if (!pendingMove) return
    try {
      await updateStatus.mutateAsync(pendingMove.status)
      toast.success(`Moved to ${pendingMove.status}`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPendingMove(null)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="CRM Pipeline" subtitle="Lead flow from demo request to conversion. Drag a card to change its stage." />
      <CrmSubNav />
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
              className="min-w-[220px] space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-3"
            >
              <div className="flex items-center justify-between">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">{stage}</h3>
                <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-600">{stageLeads.length}</span>
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

      <ConfirmDialog
        open={pendingMove !== null}
        title="Move Lead"
        message={pendingMove ? `Move this lead from "${pendingMove.previous}" to "${pendingMove.status}"?` : ''}
        confirmLabel="Confirm"
        onCancel={() => setPendingMove(null)}
        onConfirm={() => void confirmMove()}
      />
    </div>
  )
}
