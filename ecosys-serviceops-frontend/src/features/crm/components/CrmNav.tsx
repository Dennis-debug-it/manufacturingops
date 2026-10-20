import { NavLink } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

const items = [
  { to: '/crm', label: 'Pipeline', end: true },
  { to: '/crm/tenders', label: 'RFPs & RFQs', end: false },
]

/** Section tabs for the sales CRM; deal pages sit under Pipeline. */
export function CrmNav() {
  return (
    <nav className="flex flex-wrap items-center gap-1.5 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-1.5 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      {items.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          className={({ isActive }) =>
            cn(
              'inline-flex h-9 items-center rounded-xl px-3.5 text-sm font-semibold transition',
              isActive ? 'bg-[var(--app-primary)] text-white shadow-sm' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
            )
          }
        >
          {item.label}
        </NavLink>
      ))}
    </nav>
  )
}
