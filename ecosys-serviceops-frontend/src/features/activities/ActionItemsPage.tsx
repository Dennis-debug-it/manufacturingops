import { useState } from 'react'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { primaryButtonClass } from '../projects/components/formStyles'
import { useActionItems } from './api'
import { ActionItemDrawer, ActionItemList } from './ActionItems'
import type { ActionItem } from './types'

type View = 'mine' | 'all'
type StatusFilter = 'Open' | 'Done' | ''

/** "My action items" (design 3.3); managers can also see every item in the workspace. */
export function ActionItemsPage() {
  const canSeeAll = usePermission('activities.manage')
  const [view, setView] = useState<View>('mine')
  const [status, setStatus] = useState<StatusFilter>('Open')
  const [editing, setEditing] = useState<ActionItem | 'new' | null>(null)
  const { data, isLoading } = useActionItems({ mine: view === 'mine', ...(status ? { status } : {}) })

  return (
    <div className="space-y-4">
      <PageHeader
        title="Action items"
        subtitle="Follow-ups from meetings and records, with who owns them and when they're due."
        primaryAction={<button type="button" className={primaryButtonClass} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add action item</button>}
      />
      <div className="flex flex-wrap gap-2">
        {canSeeAll ? (
          <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white p-1">
            {(['mine', 'all'] as const).map((option) => (
              <button key={option} type="button" onClick={() => setView(option)}
                className={cn('rounded-md px-3 py-1 text-xs font-medium', view === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100')}>
                {option === 'mine' ? 'Mine' : 'Everyone'}
              </button>
            ))}
          </div>
        ) : null}
        <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {([['Open', 'Open'], ['Done', 'Done'], ['', 'All']] as const).map(([value, label]) => (
            <button key={label} type="button" onClick={() => setStatus(value)}
              className={cn('rounded-md px-3 py-1 text-xs font-medium', status === value ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100')}>
              {label}
            </button>
          ))}
        </div>
      </div>
      {isLoading ? <Spinner label="Loading action items" /> : (
        <ActionItemList items={data ?? []} onEdit={(item) => setEditing(item)} emptyText={status === 'Open' ? 'Nothing open. Well done.' : 'No action items.'} />
      )}
      {editing ? <ActionItemDrawer {...(editing === 'new' ? {} : { item: editing })} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}
