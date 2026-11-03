import { useState } from 'react'
import toast from 'react-hot-toast'
import { CheckCircle2, Download, UserPlus, X } from 'lucide-react'
import { downloadCsv } from '../../../shared/utils/csv'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { useBulkAssignWorkOrders, useBulkCloseWorkOrders, type BulkOperationResult } from '../api'
import { AssignTechnicianDialog } from './AssignTechnicianDialog'
import type { WorkOrderRow } from '../listModel'

export interface BulkActionBarProps {
  selectedRows: WorkOrderRow[]
  onClearSelection: () => void
}

function reportResult(result: BulkOperationResult, verb: string) {
  if (result.failed.length === 0) {
    toast.success(`${result.succeeded.length} work order(s) ${verb}`)
  } else if (result.succeeded.length === 0) {
    toast.error(`Could not ${verb} the selected work orders`)
  } else {
    toast(`${result.succeeded.length} ${verb}, ${result.failed.length} failed`)
  }
}

export function BulkActionBar({ selectedRows, onClearSelection }: BulkActionBarProps) {
  const [assignOpen, setAssignOpen] = useState(false)
  const [closeConfirmOpen, setCloseConfirmOpen] = useState(false)
  const bulkAssign = useBulkAssignWorkOrders()
  const bulkClose = useBulkCloseWorkOrders()

  if (selectedRows.length === 0) {
    return null
  }

  const exportCsv = () => {
    downloadCsv(
      `work-orders-${new Date().toISOString().slice(0, 10)}.csv`,
      selectedRows.map((row) => ({
        'WO Number': row.woNo,
        Title: row.title,
        Client: row.client,
        Site: row.site,
        Priority: row.priority,
        Status: row.status,
        'Assigned To': row.assignedTo,
        'Due Date': row.dueDate,
      })),
    )
  }

  return (
    <>
      <div className="sticky bottom-4 z-20 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-[var(--app-primary)] px-4 py-3 text-white shadow-xl">
        <div className="flex items-center gap-3">
          <span className="text-sm font-medium">{selectedRows.length} selected</span>
          <button type="button" onClick={onClearSelection} className="text-xs text-slate-300 underline decoration-slate-500 underline-offset-4 hover:text-white">
            Clear
          </button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" onClick={() => setAssignOpen(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-white/10 px-3 text-xs font-medium transition hover:bg-white/20">
            <UserPlus className="h-3.5 w-3.5" />
            Assign to technician
          </button>
          <button type="button" onClick={exportCsv} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-white/10 px-3 text-xs font-medium transition hover:bg-white/20">
            <Download className="h-3.5 w-3.5" />
            Export to CSV
          </button>
          <button type="button" onClick={() => setCloseConfirmOpen(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-white/10 px-3 text-xs font-medium transition hover:bg-white/20">
            <CheckCircle2 className="h-3.5 w-3.5" />
            Mark as Closed
          </button>
          <button type="button" onClick={onClearSelection} className="inline-flex h-9 w-9 items-center justify-center rounded-lg transition hover:bg-white/10" aria-label="Dismiss selection">
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      <AssignTechnicianDialog
        open={assignOpen}
        title={`Assign ${selectedRows.length} work order(s)`}
        submitting={bulkAssign.isPending}
        onClose={() => setAssignOpen(false)}
        onSubmit={(technicianId, notes) => {
          bulkAssign.mutate(
            { workOrderIds: selectedRows.map((row) => row.id), technicianId, notes },
            {
              onSuccess: (result) => {
                reportResult(result, 'assigned')
                setAssignOpen(false)
                onClearSelection()
              },
            },
          )
        }}
      />

      <ConfirmDialog
        open={closeConfirmOpen}
        title="Close work orders"
        message={`Mark ${selectedRows.length} work order(s) as closed? This requires admin permission and applies immediately.`}
        confirmLabel="Mark as Closed"
        onCancel={() => setCloseConfirmOpen(false)}
        onConfirm={() => {
          bulkClose.mutate(
            { workOrderIds: selectedRows.map((row) => row.id) },
            {
              onSuccess: (result) => {
                reportResult(result, 'closed')
                setCloseConfirmOpen(false)
                onClearSelection()
              },
            },
          )
        }}
      />
    </>
  )
}
