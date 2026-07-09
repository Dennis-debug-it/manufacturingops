import type { ReactNode } from 'react'
import { useMemo, useState } from 'react'
import { Search, Plus } from 'lucide-react'
import { PageHeader } from '../components/PageHeader'
import { DataTable, type DataTableColumn } from '../components/DataTable'
import { EmptyState } from '../components/EmptyState'
import { SectionHeader } from '../components/SectionHeader'
import { DetailLayout } from '../components/DetailLayout'
import { useDebounce } from '../hooks/useDebounce'

export interface ModuleRow {
  id: string
  [key: string]: string | number | boolean | null
}

export interface ModulePageConfig {
  title: string
  subtitle?: string
  breadcrumbs?: { label: string; to?: string }[]
  primaryActionLabel?: string
  primaryActionHref?: string
  secondaryActions?: ReactNode
  columns?: DataTableColumn<ModuleRow>[]
  rows?: ModuleRow[]
  detailTabs?: Array<{ id: string; label: string; content: ReactNode }>
  note?: string
  emptyTitle?: string
  emptyDescription?: string
}

export function createModulePage(config: ModulePageConfig) {
  return function ModulePage() {
    const [search, setSearch] = useState('')
    const debouncedSearch = useDebounce(search, 300)

    const rows = useMemo(() => {
      if (!config.rows) {
        return []
      }
      const query = debouncedSearch.trim().toLowerCase()
      if (!query) {
        return config.rows
      }
      return config.rows.filter((row) => Object.values(row).some((value) => String(value ?? '').toLowerCase().includes(query)))
    }, [debouncedSearch])

    const primaryAction = config.primaryActionLabel ? (
      <a
        href={config.primaryActionHref ?? '#'}
        className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
      >
        <Plus className="h-4 w-4" />
        {config.primaryActionLabel}
      </a>
    ) : null

    const header = (
      <PageHeader
        title={config.title}
        {...(config.subtitle ? { subtitle: config.subtitle } : {})}
        {...(config.breadcrumbs ? { breadcrumbs: config.breadcrumbs } : {})}
        {...(primaryAction ? { primaryAction } : {})}
        {...(config.secondaryActions ? { secondaryActions: config.secondaryActions } : {})}
      />
    )

    if (config.detailTabs && config.detailTabs.length > 0) {
      return <DetailLayout header={header} tabs={config.detailTabs} />
    }

    return (
      <div className="space-y-6">
        {header}
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <Search className="h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search"
              className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
            />
          </label>
        </div>
        {config.note ? (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
            {config.note}
          </div>
        ) : null}
        {config.columns && config.columns.length > 0 ? (
          <DataTable
            columns={config.columns}
            data={rows}
            emptyTitle={config.emptyTitle ?? 'No records yet'}
            emptyDescription={config.emptyDescription ?? 'This module is wired as a shell and will populate once the API is connected.'}
          />
        ) : (
          <EmptyState
            title={config.emptyTitle ?? 'Module shell ready'}
            description={config.emptyDescription ?? 'This route is available and ready for API integration.'}
          />
        )}
      </div>
    )
  }
}

export interface SummarySectionProps {
  title: string
  description?: string
  children: ReactNode
}

export function SummarySection({ title, description, children }: SummarySectionProps) {
  return (
      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <SectionHeader title={title} {...(description ? { description } : {})} />
      {children}
    </section>
  )
}
