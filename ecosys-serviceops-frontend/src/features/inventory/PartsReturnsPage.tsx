import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { usePartsReturns } from '../work-orders/partsReturnsApi'
import { PartsReturnCard } from '../work-orders/components/PartsReturnCard'
import { InventoryTabNav } from './components/InventoryTabNav'

const statuses = ['Pending', 'Received', 'Rejected', 'All'] as const

/** Stores' queue of parts coming back from jobs; Pending first, since those are waiting at the counter. */
export function PartsReturnsPage() {
  const [status, setStatus] = useState<(typeof statuses)[number]>('Pending')
  const { data, isLoading, isError } = usePartsReturns(status)

  return (
    <div className="space-y-4">
      <PageHeader title="Parts Returns" subtitle="Unused and faulty parts coming back from jobs, and old parts removed from client equipment." />
      <InventoryTabNav />

      <div className="flex flex-wrap gap-2">
        {statuses.map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setStatus(value)}
            className={cn(
              'inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition',
              status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {value}
          </button>
        ))}
      </div>

      {isLoading ? <Spinner label="Loading returns" />
        : isError ? <EmptyState title="Returns unavailable" description="Parts returns could not be loaded." />
        : (data ?? []).length === 0 ? <EmptyState title={status === 'Pending' ? 'Nothing waiting to be received' : 'No returns'} description="Returns raised from a work order's Parts tab appear here." />
        : <div className="space-y-3">{(data ?? []).map((item) => <PartsReturnCard key={item.id} partsReturn={item} showWorkOrder />)}</div>}
    </div>
  )
}
