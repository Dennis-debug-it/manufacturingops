import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { useAuth } from '../../shared/hooks/useAuth'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { cn } from '../../shared/utils/cn'
import { canOpenPath } from '../../shared/utils/routeAccess'
import type { AuthUser } from '../../shared/types/auth'
import { guides, type Guide } from './guides'

/**
 * Guides for the areas this person can actually open; admins see everything their edition includes.
 * <paramref name="canAccessPath"/> is the edition check (useEntitlements); omitted, every edition area counts.
 */
export function visibleGuides(user: AuthUser | null | undefined, canAccessPath: (path: string) => boolean = () => true): Guide[] {
  if (!user) return []
  return guides.filter((guide) => {
    if (guide.technicianOnly) return false
    if (guide.path && !canAccessPath(guide.path)) return false
    if (user.role === 'admin') return true
    if (guide.adminOnly) return false
    if (guide.roles && !guide.roles.includes(user.role)) return false
    return guide.path ? canOpenPath(user, guide.path) : true
  })
}

const matches = (guide: Guide, query: string) => {
  const text = [guide.title, guide.summary, ...guide.sections.flatMap((section) => [section.title, section.text ?? '', ...(section.steps ?? []), section.tip ?? ''])].join(' ').toLowerCase()
  return query.split(/\s+/).every((word) => text.includes(word))
}

export function GuideContent({ guide }: { guide: Guide }) {
  return (
    <article className="space-y-5" data-testid={`guide-${guide.id}`}>
      <div>
        <h2 className="text-xl font-semibold text-slate-900">{guide.title}</h2>
        <p className="mt-1 text-sm text-slate-600">{guide.summary}</p>
        {guide.path ? (
          <Link to={guide.path} className="mt-2 inline-flex text-sm font-medium text-[var(--app-primary)] underline underline-offset-2">Open {guide.pathLabel ?? guide.title}</Link>
        ) : null}
      </div>
      {guide.sections.map((section) => (
        <section key={section.title} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-900">{section.title}</h3>
          {section.text ? <p className="text-sm leading-6 text-slate-700">{section.text}</p> : null}
          {section.steps?.length ? (
            <ol className="list-decimal space-y-1.5 pl-5 text-sm leading-6 text-slate-700">
              {section.steps.map((step) => <li key={step}>{step}</li>)}
            </ol>
          ) : null}
          {section.tip ? <p className="rounded-lg bg-sky-50 px-3 py-2 text-xs leading-5 text-sky-900">Tip: {section.tip}</p> : null}
        </section>
      ))}
    </article>
  )
}

/** Help & user guide: a how-to for each area of the app the signed-in person can use. */
export function HelpPage() {
  const { user } = useAuth()
  const { guideId } = useParams<{ guideId: string }>()
  const [query, setQuery] = useState('')
  const { canAccessPath } = useEntitlements()
  const available = useMemo(() => visibleGuides(user, canAccessPath), [user, canAccessPath])
  const filtered = query.trim() ? available.filter((guide) => matches(guide, query.trim().toLowerCase())) : available
  const selected = available.find((guide) => guide.id === guideId) ?? filtered[0] ?? available[0]

  return (
    <div className="space-y-4">
      <PageHeader title="Help & User Guide" subtitle="How to do things in Ecosys Factory Maintenance, section by section." />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[280px_minmax(0,1fr)]">
        <nav aria-label="Guides" className="space-y-2 rounded-2xl border border-slate-200 bg-white p-3 lg:sticky lg:top-20 lg:self-start">
          <label className="flex items-center gap-2 rounded-lg border border-slate-200 px-2">
            <Search className="h-4 w-4 text-slate-400" />
            <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search the guide" aria-label="Search the guide" className="h-9 w-full border-0 bg-transparent text-sm outline-none" />
          </label>
          {filtered.length === 0 ? <p className="px-2 py-1 text-sm text-slate-500">Nothing matches “{query}”.</p> : null}
          <ul className="space-y-0.5">
            {filtered.map((guide) => {
              const Icon = guide.icon
              return (
                <li key={guide.id}>
                  <Link
                    to={`/help/${guide.id}`}
                    aria-current={guide.id === selected?.id ? 'page' : undefined}
                    className={cn('flex items-center gap-2 rounded-lg px-2 py-2 text-sm', guide.id === selected?.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700 hover:bg-slate-50')}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    {guide.title}
                  </Link>
                </li>
              )
            })}
          </ul>
        </nav>
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          {selected ? <GuideContent guide={selected} /> : <p className="text-sm text-slate-500">No guides available.</p>}
        </div>
      </div>
    </div>
  )
}
