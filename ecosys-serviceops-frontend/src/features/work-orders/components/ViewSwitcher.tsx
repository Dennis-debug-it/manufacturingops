import { CalendarDays, Kanban, List, Map as MapIcon } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import type { ViewMode } from '../hooks/useWorkOrderListState'

const OPTIONS: Array<{ key: ViewMode; label: string; icon: typeof List }> = [
  { key: 'table', label: 'Table', icon: List },
  { key: 'kanban', label: 'Kanban', icon: Kanban },
  { key: 'calendar', label: 'Calendar', icon: CalendarDays },
  { key: 'map', label: 'Map', icon: MapIcon },
]

export interface ViewSwitcherProps {
  value: ViewMode
  onChange: (view: ViewMode) => void
}

export function ViewSwitcher({ value, onChange }: ViewSwitcherProps) {
  return (
    <div className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white p-1">
      {OPTIONS.map(({ key, label, icon: Icon }) => (
        <button
          key={key}
          type="button"
          onClick={() => onChange(key)}
          aria-pressed={value === key}
          className={cn(
            'inline-flex h-8 items-center gap-1.5 rounded-md px-2.5 text-xs font-medium transition',
            value === key ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100',
          )}
        >
          <Icon className="h-3.5 w-3.5" />
          {label}
        </button>
      ))}
    </div>
  )
}
