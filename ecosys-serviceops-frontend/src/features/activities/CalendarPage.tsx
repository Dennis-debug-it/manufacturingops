import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { addDays, addMonths, addWeeks, format, isSameMonth, isToday, startOfWeek } from 'date-fns'
import { CalendarPlus, ChevronLeft, ChevronRight, Link2, Users } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { primaryButtonClass, secondaryButtonClass } from '../projects/components/formStyles'
import { useActivityPeople, useCalendar } from './api'
import { ActivityDrawer } from './ActivityDrawer'
import { calendarRange, defaultCalendarMode, groupByDay, itemTimeLabel, type CalendarMode } from './logic'
import { calendarSourceLabels, type CalendarItem, type CalendarSource } from './types'

const sourceTone: Record<CalendarSource, string> = {
  activities: 'bg-sky-100 text-sky-900 border-sky-200',
  'action-items': 'bg-amber-100 text-amber-900 border-amber-200',
  'work-orders': 'bg-emerald-100 text-emerald-900 border-emerald-200',
  maintenance: 'bg-teal-100 text-teal-900 border-teal-200',
  projects: 'bg-violet-100 text-violet-900 border-violet-200',
  contracts: 'bg-rose-100 text-rose-900 border-rose-200',
}

const sourceDot: Record<CalendarSource, string> = {
  activities: 'bg-sky-500',
  'action-items': 'bg-amber-500',
  'work-orders': 'bg-emerald-500',
  maintenance: 'bg-teal-500',
  projects: 'bg-violet-500',
  contracts: 'bg-rose-500',
}

const initialMode = () => (typeof window === 'undefined' ? 'month' : defaultCalendarMode(window.innerWidth))

/** My calendar (design 3.4): meetings plus the dates Ecosys already knows, each source only when the user may open it. */
export function CalendarPage() {
  const navigate = useNavigate()
  const [mode, setMode] = useState<CalendarMode>(initialMode)
  const [cursor, setCursor] = useState(() => new Date())
  const [selectedDay, setSelectedDay] = useState<Date | null>(null)
  const [hidden, setHidden] = useState<CalendarSource[]>([])
  const [team, setTeam] = useState<string[] | null>(null)
  const [scheduleOn, setScheduleOn] = useState<string | null>(null)

  // The team view is a week, one row per person, like the scheduling board.
  const effectiveMode: CalendarMode = team ? 'week' : mode
  const range = useMemo(() => calendarRange(effectiveMode, cursor), [effectiveMode, cursor])
  const { data, isLoading } = useCalendar(range.from, range.to, team ?? [])
  const { data: people } = useActivityPeople(null, Boolean(data?.canViewTeam))
  const items = useMemo(() => (data?.items ?? []).filter((x) => !hidden.includes(x.source)), [data, hidden])
  const byDay = useMemo(() => groupByDay(items), [items])

  const step = (direction: -1 | 1) => setCursor((current) =>
    effectiveMode === 'month' ? addMonths(current, direction) : effectiveMode === 'week' ? addWeeks(current, direction) : addDays(current, direction * 7))
  const headerLabel = effectiveMode === 'month' ? format(cursor, 'MMMM yyyy')
    : effectiveMode === 'week' ? `Week of ${format(startOfWeek(cursor), 'd MMM yyyy')}` : `From ${format(cursor, 'd MMM yyyy')}`
  const open = (item: CalendarItem) => { if (item.url) navigate(item.url) }

  return (
    <div className="space-y-4">
      <PageHeader
        title={team ? 'Team calendar' : 'My calendar'}
        subtitle="Meetings and visits, with the work order, project and contract dates you can open."
        secondaryActions={<Link to="/calendar/feed" className={secondaryButtonClass}><Link2 className="h-4 w-4" />Subscribe</Link>}
        primaryAction={<button type="button" className={primaryButtonClass} onClick={() => setScheduleOn(format(selectedDay ?? new Date(), 'yyyy-MM-dd'))}><CalendarPlus className="h-4 w-4" />Schedule</button>}
      />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-1 py-1">
          <button type="button" onClick={() => step(-1)} className="rounded-md p-1.5 hover:bg-slate-100" aria-label="Previous"><ChevronLeft className="h-4 w-4" /></button>
          <span className="min-w-[9rem] px-1 text-center text-sm font-medium text-slate-900">{headerLabel}</span>
          <button type="button" onClick={() => step(1)} className="rounded-md p-1.5 hover:bg-slate-100" aria-label="Next"><ChevronRight className="h-4 w-4" /></button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" onClick={() => setCursor(new Date())} className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">Today</button>
          {!team ? (
            <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-1 py-1">
              {(['month', 'week', 'agenda'] as const).map((option) => (
                <button key={option} type="button" onClick={() => setMode(option)}
                  className={cn('rounded-md px-2.5 py-1 text-xs font-medium capitalize transition', mode === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100')}>
                  {option}
                </button>
              ))}
            </div>
          ) : null}
          {data?.canViewTeam ? (
            <button type="button" onClick={() => setTeam(team ? null : [])}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium', team ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
              <Users className="h-4 w-4" />Team
            </button>
          ) : null}
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {(data?.sources ?? []).map((source) => (
          <button key={source} type="button" aria-pressed={!hidden.includes(source)}
            onClick={() => setHidden((list) => (list.includes(source) ? list.filter((x) => x !== source) : [...list, source]))}
            className={cn('inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium', hidden.includes(source) ? 'border-slate-200 bg-white text-slate-400' : sourceTone[source])}>
            <span className={cn('h-2 w-2 rounded-full', sourceDot[source])} />{calendarSourceLabels[source]}
          </button>
        ))}
      </div>

      {team ? (
        <TeamPicker people={people?.users ?? []} selected={team} onChange={setTeam} />
      ) : null}

      {isLoading ? <Spinner label="Loading calendar" /> : team ? (
        <TeamWeek days={range.days} items={items} people={(people?.users ?? []).filter((x) => team.includes(x.id))} onOpen={open} />
      ) : effectiveMode === 'agenda' ? (
        <Agenda days={range.days} byDay={byDay} onOpen={open} />
      ) : (
        <>
          <div className="grid grid-cols-7 gap-px overflow-hidden rounded-2xl border border-slate-200 bg-slate-200 shadow-sm">
            {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((label) => (
              <div key={label} className="bg-slate-50 px-1 py-1.5 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>
            ))}
            {range.days.map((day) => {
              const key = format(day, 'yyyy-MM-dd')
              const dayItems = byDay.get(key) ?? []
              const inMonth = effectiveMode !== 'month' || isSameMonth(day, cursor)
              return (
                <button key={key} type="button" onClick={() => setSelectedDay(day)}
                  className={cn('flex min-w-0 flex-col gap-1 bg-white p-1 text-left align-top hover:bg-slate-50 sm:p-1.5',
                    effectiveMode === 'week' ? 'min-h-40' : 'min-h-16 sm:min-h-24', !inMonth && 'bg-slate-50 text-slate-400',
                    selectedDay && format(selectedDay, 'yyyy-MM-dd') === key && 'ring-2 ring-inset ring-[var(--app-primary)]')}>
                  <span className={cn('inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium', isToday(day) ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700')}>{format(day, 'd')}</span>
                  {/* Phones show dots; wider screens show the first few titles. */}
                  <span className="flex flex-wrap gap-0.5 sm:hidden">
                    {dayItems.slice(0, 6).map((item) => <span key={`${item.uid}-${item.userId}`} className={cn('h-1.5 w-1.5 rounded-full', sourceDot[item.source])} />)}
                  </span>
                  <span className="hidden min-w-0 flex-col gap-0.5 sm:flex">
                    {dayItems.slice(0, effectiveMode === 'week' ? 8 : 3).map((item) => (
                      <span key={`${item.uid}-${item.userId}`} className={cn('truncate rounded border px-1 py-0.5 text-[11px]', sourceTone[item.source])}>
                        {item.allDay ? '' : `${format(new Date(item.startsAt), 'HH:mm')} `}{item.title}
                      </span>
                    ))}
                    {dayItems.length > (effectiveMode === 'week' ? 8 : 3) ? <span className="text-[11px] text-slate-500">+{dayItems.length - (effectiveMode === 'week' ? 8 : 3)} more</span> : null}
                  </span>
                </button>
              )
            })}
          </div>
          {selectedDay ? (
            <section className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="text-sm font-semibold text-slate-900">{format(selectedDay, 'EEEE d MMMM')}</h2>
                <button type="button" className={secondaryButtonClass} onClick={() => setScheduleOn(format(selectedDay, 'yyyy-MM-dd'))}><CalendarPlus className="h-4 w-4" />Schedule on this day</button>
              </div>
              <ItemList items={byDay.get(format(selectedDay, 'yyyy-MM-dd')) ?? []} onOpen={open} />
            </section>
          ) : null}
        </>
      )}

      {scheduleOn ? (
        <ActivityDrawer open onClose={() => setScheduleOn(null)} defaults={{ date: scheduleOn }} onSaved={(saved) => navigate(`/activities/${saved.summary.id}`)} />
      ) : null}
    </div>
  )
}

function ItemList({ items, onOpen }: { items: CalendarItem[]; onOpen: (item: CalendarItem) => void }) {
  if (items.length === 0) return <p className="text-sm text-slate-500">Nothing on this day.</p>
  return (
    <ul className="space-y-1.5">
      {items.map((item) => (
        <li key={`${item.uid}-${item.userId}`}>
          <button type="button" onClick={() => onOpen(item)} className={cn('flex w-full min-w-0 items-start gap-3 rounded-lg border px-3 py-2 text-left text-sm', sourceTone[item.source])}>
            <span className="w-24 shrink-0 text-xs font-medium">{itemTimeLabel(item)}</span>
            <span className="min-w-0 flex-1">
              <span className={cn('block break-words font-medium', item.status === 'Cancelled' && 'line-through')}>{item.title}</span>
              {item.location ? <span className="block truncate text-xs opacity-80">{item.location}</span> : null}
            </span>
          </button>
        </li>
      ))}
    </ul>
  )
}

function Agenda({ days, byDay, onOpen }: { days: Date[]; byDay: Map<string, CalendarItem[]>; onOpen: (item: CalendarItem) => void }) {
  const withItems = days.filter((day) => (byDay.get(format(day, 'yyyy-MM-dd')) ?? []).length > 0)
  if (withItems.length === 0) return <p className="rounded-2xl border border-dashed border-slate-200 bg-white p-6 text-center text-sm text-slate-500">Nothing in the next three weeks.</p>
  return (
    <div className="space-y-4">
      {withItems.map((day) => (
        <section key={day.toISOString()} className="space-y-1.5">
          <h2 className={cn('text-xs font-semibold uppercase tracking-wide', isToday(day) ? 'text-[var(--app-primary)]' : 'text-slate-500')}>
            {isToday(day) ? 'Today · ' : ''}{format(day, 'EEE d MMM')}
          </h2>
          <ItemList items={byDay.get(format(day, 'yyyy-MM-dd')) ?? []} onOpen={onOpen} />
        </section>
      ))}
    </div>
  )
}

function TeamPicker({ people, selected, onChange }: { people: Array<{ id: string; name: string }>; selected: string[]; onChange: (ids: string[]) => void }) {
  return (
    <div className="flex flex-wrap gap-1.5 rounded-2xl border border-slate-200 bg-white p-3">
      <span className="w-full text-xs text-slate-500">Choose up to 50 people.</span>
      {people.map((person) => (
        <button key={person.id} type="button" aria-pressed={selected.includes(person.id)}
          onClick={() => onChange(selected.includes(person.id) ? selected.filter((x) => x !== person.id) : selected.length < 50 ? [...selected, person.id] : selected)}
          className={cn('rounded-full border px-2.5 py-1 text-xs', selected.includes(person.id) ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 text-slate-700 hover:bg-slate-50')}>
          {person.name}
        </button>
      ))}
    </div>
  )
}

/** One row per person, one column per day. Scrolls inside its own box on narrow screens. */
function TeamWeek({ days, items, people, onOpen }: { days: Date[]; items: CalendarItem[]; people: Array<{ id: string; name: string }>; onOpen: (item: CalendarItem) => void }) {
  if (people.length === 0) return <p className="rounded-2xl border border-dashed border-slate-200 bg-white p-6 text-center text-sm text-slate-500">Choose people to compare their week.</p>
  const byPerson = new Map(people.map((person) => [person.id, groupByDay(items.filter((x) => x.userId === person.id))]))
  return (
    <div className="max-w-full overflow-x-auto rounded-2xl border border-slate-200 bg-white">
      <table className="w-full min-w-[720px] table-fixed text-xs">
        <thead>
          <tr className="bg-slate-50 text-slate-500">
            <th className="w-32 px-2 py-2 text-left font-semibold">Person</th>
            {days.map((day) => <th key={day.toISOString()} className={cn('px-1 py-2 text-left font-semibold', isToday(day) && 'text-[var(--app-primary)]')}>{format(day, 'EEE d')}</th>)}
          </tr>
        </thead>
        <tbody>
          {people.map((person) => (
            <tr key={person.id} className="border-t border-slate-100 align-top">
              <td className="truncate px-2 py-2 font-medium text-slate-900">{person.name}</td>
              {days.map((day) => (
                <td key={day.toISOString()} className="space-y-0.5 px-1 py-1">
                  {(byPerson.get(person.id)?.get(format(day, 'yyyy-MM-dd')) ?? []).map((item) => (
                    <button key={item.uid} type="button" onClick={() => onOpen(item)} className={cn('block w-full truncate rounded border px-1 py-0.5 text-left', sourceTone[item.source])}>
                      {item.allDay ? '' : `${format(new Date(item.startsAt), 'HH:mm')} `}{item.title}
                    </button>
                  ))}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
