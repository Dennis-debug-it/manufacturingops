import { DataTable } from '../../../components/ui/DataTable'
import type { IntakeActivityRecord } from './emailIntakeModels'

export type ActivityLogFilters = {
  status: string
  sender: string
  actionType: string
  fromDate: string
  toDate: string
}

export function ActivityLogPanel({
  rows,
  filters,
  onFilterChange,
}: {
  rows: IntakeActivityRecord[]
  filters: ActivityLogFilters
  onFilterChange: (patch: Partial<ActivityLogFilters>) => void
}) {
  return (
    <section data-testid="email-intake-activity-log-panel" className="space-y-5">
      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Activity Filters</h3>
        <div className="mt-4 grid gap-4 lg:grid-cols-5">
          <select className="field-input" value={filters.status} onChange={(event) => onFilterChange({ status: event.target.value })}>
            <option value="">Status</option>
            <option value="Success">Success</option>
            <option value="Warning">Warning</option>
            <option value="Failed">Failed</option>
            <option value="Info">Info</option>
          </select>
          <input className="field-input" value={filters.sender} onChange={(event) => onFilterChange({ sender: event.target.value })} placeholder="Sender" />
          <input className="field-input" value={filters.actionType} onChange={(event) => onFilterChange({ actionType: event.target.value })} placeholder="Action type" />
          <input className="field-input" type="date" value={filters.fromDate} onChange={(event) => onFilterChange({ fromDate: event.target.value })} />
          <input className="field-input" type="date" value={filters.toDate} onChange={(event) => onFilterChange({ toDate: event.target.value })} />
        </div>
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Intake Activity Log</h3>
        <p className="mt-1 text-sm text-muted">Track each stage from email receipt to routing and notification.</p>
        <div className="mt-4">
          <DataTable
            rows={rows}
            rowKey={(row) => row.id}
            pageSize={10}
            emptyTitle="No activity records"
            emptyDescription="Intake processing history will appear here."
            minTableWidth="min-w-[1180px] w-full"
            columns={[
              { key: 'time', header: 'Time', cell: (row) => new Date(row.occurredAt).toLocaleString() },
              { key: 'sender', header: 'Sender', cell: (row) => row.sender },
              { key: 'subject', header: 'Subject', cell: (row) => row.subject },
              { key: 'action', header: 'Action Type', cell: (row) => row.actionType },
              { key: 'status', header: 'Status', cell: (row) => row.status },
              { key: 'wo', header: 'Work Order', cell: (row) => row.workOrderRef || '-' },
              { key: 'detail', header: 'Details', cell: (row) => row.detail || '-' },
            ]}
          />
        </div>
      </article>
    </section>
  )
}
