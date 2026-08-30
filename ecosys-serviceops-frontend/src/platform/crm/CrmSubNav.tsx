import { NavLink } from 'react-router-dom'
import { cn } from '../../shared/utils/cn'

const items = [
  { to: '/platform/leads', label: 'Leads' },
  { to: '/platform/crm/pipeline', label: 'Pipeline' },
  { to: '/platform/crm/contacts', label: 'Contacts' },
  { to: '/platform/crm/activities', label: 'Activities' },
  { to: '/platform/crm/tasks', label: 'Tasks' },
]

export function CrmSubNav() {
  return (
    <div className="flex flex-wrap gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-2 shadow-sm">
      {items.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          className={({ isActive }) =>
            cn('inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition', isActive ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100')
          }
        >
          {item.label}
        </NavLink>
      ))}
    </div>
  )
}
