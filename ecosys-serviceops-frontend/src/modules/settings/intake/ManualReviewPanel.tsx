import { DataTable } from '../../../components/ui/DataTable'
import type { ManualReviewRecord } from './emailIntakeModels'

export function ManualReviewPanel({ rows }: { rows: ManualReviewRecord[] }) {
  return (
    <section data-testid="email-intake-manual-review-panel" className="surface-card">
      <h3 className="text-lg font-semibold text-app">Manual Review Queue</h3>
      <p className="mt-1 text-sm text-muted">Manage unmatched and low-confidence intake emails.</p>

      <div className="mt-4">
        <DataTable
          rows={rows}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No manual review items"
          emptyDescription="When confidence is low, emails are queued here for review actions."
          minTableWidth="min-w-[1180px] w-full"
          columns={[
            { key: 'date', header: 'Received Date', cell: (row) => new Date(row.receivedDate).toLocaleString() },
            { key: 'sender', header: 'Sender', cell: (row) => row.sender },
            { key: 'subject', header: 'Subject', cell: (row) => row.subject },
            { key: 'client', header: 'Suggested Client', cell: (row) => row.suggestedClient },
            { key: 'asset', header: 'Suggested Asset', cell: (row) => row.suggestedAsset },
            { key: 'confidence', header: 'Confidence', cell: (row) => `${row.confidence}%` },
            { key: 'reason', header: 'Review Reason', cell: (row) => row.reviewReason },
            {
              key: 'actions',
              header: 'Actions',
              cell: () => (
                <div className="flex flex-wrap gap-2">
                  <button type="button" className="button-secondary px-3 py-2">Create work order</button>
                  <button type="button" className="button-secondary px-3 py-2">Link existing</button>
                  <button type="button" className="button-secondary px-3 py-2">Ignore</button>
                  <button type="button" className="button-secondary px-3 py-2">Block sender/domain</button>
                </div>
              ),
            },
          ]}
        />
      </div>
    </section>
  )
}
