import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { LayoutGrid, LineChart, MessageCircle, Plus, RotateCcw } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { Surface } from '../../shared/components/Surface'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatRelative } from '../../shared/utils/dates'
import { useWhatsAppConversations } from '../whatsapp/api'
import { WHATSAPP_ENABLED } from '../../shared/config/features'
import { DashboardWidgetCard } from './DashboardWidgetCard'
import { addItem, addableWidgets, moveItem, normalizeLayout, removeItem, sameLayout, setItemSize } from './layoutModel'
import { useDashboardLayout, useResetDashboardLayout, useSaveDashboardLayout } from './roleDashboardApi'
import type { DashboardLayoutItem } from './roleDashboardTypes'

const buttonClass =
  'inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'
const primaryButtonClass =
  'inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-60'

/**
 * The role dashboard (Release 3 part 4): the caller's layout, then every widget loading on its own.
 * "Customise" lets the user reorder, hide, add and resize widgets, or go back to their role's default.
 */
export function DashboardPage() {
  const { branches } = useAuth()
  // The executive view reads the reports and revenue APIs; offer it only to people they serve.
  const canViewReports = usePermission('reports.view')
  const canViewFinance = usePermission('finance.view')
  const [branchId, setBranchId] = useState('')
  const layout = useDashboardLayout()
  const save = useSaveDashboardLayout()
  const reset = useResetDashboardLayout()
  const [draft, setDraft] = useState<DashboardLayoutItem[] | null>(null)
  const [confirmReset, setConfirmReset] = useState(false)

  const available = useMemo(() => layout.data?.available ?? [], [layout.data])
  const infoByKey = useMemo(() => new Map(available.map((widget) => [widget.key, widget])), [available])
  const editing = draft !== null
  const items = draft ?? layout.data?.items ?? []
  const addable = addableWidgets(items, available)

  const startEditing = () => setDraft(normalizeLayout(layout.data?.items ?? [], available))
  const saveDraft = async () => {
    if (!draft) return
    if (layout.data && sameLayout(draft, layout.data.items)) {
      setDraft(null)
      return
    }
    try {
      await save.mutateAsync(draft)
      setDraft(null)
      toast.success('Dashboard saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }
  const resetToDefault = async () => {
    setConfirmReset(false)
    try {
      await reset.mutateAsync()
      setDraft(null)
      toast.success('Dashboard reset to your role default')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (layout.isLoading) return <Spinner label="Loading dashboard" />
  if (layout.isError || !layout.data) {
    return (
      <ErrorState
        title="Dashboard unavailable"
        description="Your dashboard layout could not be loaded."
        onRetry={() => void layout.refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Dashboard"
        subtitle={editing ? 'Reorder, resize, hide or add widgets, then save.' : 'Your day at a glance. Every figure opens the list behind it.'}
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            {branches.length > 1 && !editing ? (
              <select
                value={branchId}
                onChange={(event) => setBranchId(event.target.value)}
                aria-label="Branch"
                className="h-11 max-w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
              >
                <option value="">All Branches</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>{branch.name}</option>
                ))}
              </select>
            ) : null}
            {editing ? (
              <>
                <button type="button" className={buttonClass} onClick={() => setConfirmReset(true)} disabled={reset.isPending}>
                  <RotateCcw className="h-4 w-4" />Reset to default
                </button>
                <button type="button" className={buttonClass} onClick={() => setDraft(null)}>Cancel</button>
                <button type="button" className={primaryButtonClass} onClick={() => void saveDraft()} disabled={save.isPending}>Save</button>
              </>
            ) : (
              <>
                {/* The only entry point to the executive view — its route had no inbound link at all. */}
                {canViewReports && canViewFinance ? (
                  <Link to="/dashboard/executive" className={buttonClass}>
                    <LineChart className="h-4 w-4" />
                    Executive View
                  </Link>
                ) : null}
                <button type="button" className={buttonClass} onClick={startEditing}>
                  <LayoutGrid className="h-4 w-4" />Customise
                </button>
              </>
            )}
          </div>
        }
      />

      {items.length === 0 ? (
        <EmptyState
          title="No widgets on your dashboard"
          description={available.length > 0 ? 'Choose Customise to add widgets.' : 'No dashboard widgets are available for your role.'}
        />
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
          {items.map((item, index) => (
            <DashboardWidgetCard
              key={item.key}
              item={item}
              info={infoByKey.get(item.key)}
              branchId={branchId}
              edit={editing ? {
                isFirst: index === 0,
                isLast: index === items.length - 1,
                onMove: (direction) => setDraft((current) => moveItem(current ?? [], item.key, direction)),
                onResize: () => setDraft((current) => setItemSize(current ?? [], item.key, item.size === 'wide' ? 'small' : 'wide')),
                onHide: () => setDraft((current) => removeItem(current ?? [], item.key)),
              } : undefined}
            />
          ))}
        </div>
      )}

      {editing && addable.length > 0 ? (
        <Surface as="section" variant="solid" padding="sm">
          <h2 className="text-sm font-semibold text-slate-800">Add a widget</h2>
          <ul className="mt-2 grid grid-cols-1 gap-2 md:grid-cols-2">
            {addable.map((widget) => (
              <li key={widget.key}>
                <button
                  type="button"
                  onClick={() => setDraft((current) => addItem(current ?? [], widget))}
                  className="flex w-full min-w-0 items-start gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-left transition hover:bg-slate-50"
                >
                  <Plus className="mt-0.5 h-4 w-4 shrink-0 text-[var(--app-primary)]" />
                  <span className="min-w-0">
                    <span className="block text-sm font-medium text-slate-800">{widget.title}</span>
                    <span className="block text-xs text-slate-500">{widget.description}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </Surface>
      ) : null}

      {WHATSAPP_ENABLED && !editing ? <WhatsAppAlertsWidget /> : null}

      <ConfirmDialog
        open={confirmReset}
        title="Reset your dashboard?"
        message="Your own arrangement is forgotten and your role's default widgets come back."
        confirmLabel="Reset"
        onCancel={() => setConfirmReset(false)}
        onConfirm={() => void resetToDefault()}
      />
    </div>
  )
}

function WhatsAppAlertsWidget() {
  const { data: conversations, isLoading } = useWhatsAppConversations()
  const recent = (conversations ?? []).slice(0, 4)
  const totalUnread = (conversations ?? []).reduce((sum, c) => sum + (c.unreadCount ?? 0), 0)

  return (
    <Surface as="section" variant="glass" padding="lg">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <MessageCircle className="h-5 w-5 text-emerald-600" />
          <div className="text-sm font-medium text-slate-700">WhatsApp Alerts</div>
          {totalUnread > 0 ? <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700">{totalUnread} unread</span> : null}
        </div>
        <Link to="/whatsapp" className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-900">Open inbox</Link>
      </div>
      {isLoading ? (
        <p className="mt-3 text-sm text-slate-400">Loading conversations…</p>
      ) : recent.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">No WhatsApp conversations yet. Start one from the inbox.</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {recent.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-3 py-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-sm font-medium text-slate-800">
                  <span className="truncate">{c.contactName}</span>
                  {c.unreadCount > 0 ? <span className="rounded-full bg-emerald-500 px-1.5 text-[10px] font-semibold text-white">{c.unreadCount}</span> : null}
                </div>
                <div className="truncate text-xs text-slate-500">{c.lastMessagePreview}</div>
              </div>
              <span className="shrink-0 text-xs text-slate-400">{formatRelative(c.lastMessageAt)}</span>
            </li>
          ))}
        </ul>
      )}
    </Surface>
  )
}
