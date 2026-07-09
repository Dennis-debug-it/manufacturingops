import { ExternalLink, PackageCheck, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { pmTemplateService } from '../../services/pmTemplateService'
import { starterPackService, type StarterPackRecord } from '../../services/starterPackService'
import type { PmTemplateRecord } from '../../types/api'

type PageData = {
  starterPacks: StarterPackRecord[]
  customTemplates: PmTemplateRecord[]
}

const emptyData: PageData = { starterPacks: [], customTemplates: [] }

// ─── Page ─────────────────────────────────────────────────────────────────────

export function ChecklistTemplatesPage() {
  const { pushToast } = useToast()

  const { data, loading, error, reload } = useAsyncData<PageData>(
    async (signal) => {
      const [starterPacks, customTemplates] = await Promise.all([
        starterPackService.list(signal),
        pmTemplateService.list(signal),
      ])
      return { starterPacks, customTemplates }
    },
    emptyData,
    [],
  )

  const [installingPackId, setInstallingPackId] = useState<string | null>(null)
  const [installTarget, setInstallTarget] = useState<StarterPackRecord | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<PmTemplateRecord | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  async function confirmInstall() {
    if (!installTarget) return
    const pack = installTarget
    setInstallTarget(null)
    setInstallingPackId(pack.id)
    try {
      await starterPackService.install(pack.id)
      const checklistCount = pack.checklistTemplates.length
      pushToast({
        title: 'Pack installed',
        description: `${pack.name} — ${checklistCount} checklist${checklistCount !== 1 ? 's' : ''} added to your workspace.`,
        tone: 'success',
      })
      await reload()
    } catch (err) {
      pushToast({
        title: 'Install failed',
        description: err instanceof Error ? err.message : 'Unable to install the starter pack.',
        tone: 'danger',
      })
    } finally {
      setInstallingPackId(null)
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    const template = deleteTarget
    setIsDeleting(true)
    try {
      await pmTemplateService.remove(template.id)
      pushToast({ title: 'Template deleted', tone: 'success' })
      setDeleteTarget(null)
      await reload()
    } catch (err) {
      pushToast({
        title: 'Delete failed',
        description: err instanceof Error ? err.message : 'Unable to delete the template.',
        tone: 'danger',
      })
    } finally {
      setIsDeleting(false)
    }
  }

  if (loading) return <LoadingState label="Loading checklist templates" />
  if (error) return <ErrorState message={error} onRetry={reload} />

  const { starterPacks, customTemplates } = data

  return (
    <PageScaffold
      eyebrow="Operations"
      title="Checklist Templates"
      description="Install pre-built checklist packs or manage your own custom service checklists."
    >
      {/* ── System Starter Packs ──────────────────────────────────────────── */}
      <SectionCard
        title="System Templates"
        description="Ready-made starter packs for common contractor types. Install once — templates are copied into your workspace for editing."
      >
        {starterPacks.length === 0 ? (
          <EmptyState
            title="No starter packs available"
            description="System packs will appear here once the platform catalog is ready."
          />
        ) : (
          <div className="grid gap-4 xl:grid-cols-2 2xl:grid-cols-3">
            {starterPacks.map((pack) => (
              <StarterPackCard
                key={pack.id}
                pack={pack}
                installing={installingPackId === pack.id}
                onInstall={() => setInstallTarget(pack)}
              />
            ))}
          </div>
        )}
      </SectionCard>

      {/* ── Custom Templates ──────────────────────────────────────────────── */}
      <SectionCard
        title="My Custom Templates"
        description="Checklists in your workspace — from installed packs or created from scratch."
        action={
          <Link to="/templates" className="button-secondary inline-flex items-center gap-2">
            <ExternalLink className="h-4 w-4" />
            Open Templates
          </Link>
        }
      >
        {customTemplates.length === 0 ? (
          <EmptyState
            title="No custom templates yet"
            description="Install a starter pack above, or create your own checklist in the Templates workspace."
          />
        ) : (
          <CustomTemplatesDataTable
            templates={customTemplates}
            onDelete={(t) => setDeleteTarget(t)}
          />
        )}
      </SectionCard>

      {/* ── Dialogs ───────────────────────────────────────────────────────── */}
      <ConfirmDialog
        open={Boolean(installTarget)}
        title={`Install ${installTarget?.name ?? 'starter pack'}?`}
        description={
          installTarget
            ? `This will add ${installTarget.checklistTemplates.length} checklist template${installTarget.checklistTemplates.length !== 1 ? 's' : ''} and ${installTarget.assetCategories.length} asset categor${installTarget.assetCategories.length !== 1 ? 'ies' : 'y'} to your workspace.`
            : ''
        }
        confirmLabel="Install pack"
        tone="default"
        onCancel={() => setInstallTarget(null)}
        onConfirm={() => void confirmInstall()}
      />

      <ConfirmDialog
        open={Boolean(deleteTarget)}
        title="Delete checklist template?"
        description={`Delete "${deleteTarget?.name ?? 'this template'}"? Existing work orders will keep their saved checklist snapshot.`}
        confirmLabel={isDeleting ? 'Deleting…' : 'Delete template'}
        tone="danger"
        onCancel={() => { if (!isDeleting) setDeleteTarget(null) }}
        onConfirm={() => void confirmDelete()}
      />
    </PageScaffold>
  )
}

// ─── StarterPackCard ──────────────────────────────────────────────────────────

function StarterPackCard({
  pack,
  installing,
  onInstall,
}: {
  pack: StarterPackRecord
  installing: boolean
  onInstall: () => void
}) {
  return (
    <article className="rounded-[24px] border border-app bg-[var(--app-surface)] p-5 shadow-[var(--app-shadow-soft)]">
      {/* Header row */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap gap-2">
            <Badge tone={pack.isInstalled ? 'success' : 'neutral'}>
              {pack.isInstalled ? 'Installed' : 'Not installed'}
            </Badge>
            <Badge tone="info">{pack.contractorType}</Badge>
          </div>
          <h3 className="mt-3 text-lg font-semibold text-app">{pack.name}</h3>
          <p className="mt-1.5 text-sm leading-6 text-muted">
            {pack.description ?? 'System starter pack for contractor operations.'}
          </p>
        </div>

        <button
          type="button"
          className="button-primary shrink-0"
          disabled={pack.isInstalled || installing}
          onClick={onInstall}
        >
          <PackageCheck className="h-4 w-4" />
          {installing ? 'Installing…' : pack.isInstalled ? 'Installed' : 'Install'}
        </button>
      </div>

      {/* Checklists included */}
      <div className="mt-5 space-y-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-widest text-muted">
            Checklists included ({pack.checklistTemplates.length})
          </p>
          <div className="mt-2 space-y-1.5">
            {pack.checklistTemplates.map((tpl) => (
              <div
                key={tpl.id}
                className="flex items-center justify-between gap-3 rounded-xl border border-app bg-[var(--app-subtle)] px-3.5 py-2.5"
              >
                <span className="truncate text-sm font-medium text-app">{tpl.name}</span>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-xs text-muted">
                    {tpl.sectionCount}s&nbsp;&middot;&nbsp;{tpl.itemCount}i
                  </span>
                  <Badge tone={tpl.workType === 'BreakdownCorrective' ? 'warning' : 'info'}>
                    {tpl.workType === 'BreakdownCorrective' ? 'Corrective' : 'PM'}
                  </Badge>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Asset categories included */}
        {pack.assetCategories.length > 0 ? (
          <div>
            <p className="text-xs font-semibold uppercase tracking-widest text-muted">
              Asset categories ({pack.assetCategories.length})
            </p>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {pack.assetCategories.map((cat) => (
                <Badge key={cat.id} tone="neutral">{cat.name}</Badge>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </article>
  )
}

// ─── CustomTemplatesTable ─────────────────────────────────────────────────────

function workTypeLabel(workType?: string | null): string {
  if (workType === 'PreventiveMaintenance') return 'Preventive Maintenance'
  if (workType === 'BreakdownCorrective') return 'Breakdown / Corrective'
  return workType ?? '—'
}

// eslint-disable-next-line no-unused-vars
export function CustomTemplatesTable({
  templates,
  onDelete,
}: {
  templates: PmTemplateRecord[]
  onDelete: (template: PmTemplateRecord) => void
}) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-app">
      <table className="min-w-full divide-y divide-app text-sm">
        <thead className="bg-[var(--app-subtle)]">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-widest text-muted">
              Name
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-widest text-muted">
              Work Type
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-widest text-muted">
              Sections
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-widest text-muted">
              Items
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-widest text-muted">
              Status
            </th>
            <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-widest text-muted">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-app bg-[var(--app-surface)]">
          {templates.map((tpl) => (
            <tr key={tpl.id} className="transition-colors hover:bg-[var(--app-subtle)]">
              <td className="px-4 py-3">
                <div className="font-medium text-app">{tpl.name}</div>
                {tpl.category ? (
                  <div className="text-xs text-muted">{tpl.category}</div>
                ) : null}
              </td>
              <td className="px-4 py-3 text-muted">{workTypeLabel(tpl.workType)}</td>
              <td className="px-4 py-3 tabular-nums text-app">
                {tpl.sections?.length ?? '—'}
              </td>
              <td className="px-4 py-3 tabular-nums text-app">
                {tpl.checklist?.length ?? '—'}
              </td>
              <td className="px-4 py-3">
                <Badge tone={tpl.isActive ? 'success' : 'neutral'}>
                  {tpl.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </td>
              <td className="px-4 py-3 text-right">
                <div className="flex items-center justify-end gap-2">
                  <Link
                    to="/templates"
                    className="button-ghost inline-flex items-center gap-1.5 text-xs"
                  >
                    <ExternalLink className="h-3.5 w-3.5" />
                    Edit
                  </Link>
                  <button
                    type="button"
                    className="button-ghost inline-flex items-center gap-1.5 text-xs text-[var(--app-badge-danger-text)]"
                    onClick={() => onDelete(tpl)}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    Delete
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function CustomTemplatesDataTable({
  templates,
  onDelete,
}: {
  templates: PmTemplateRecord[]
  onDelete: (template: PmTemplateRecord) => void
}) {
  return (
    <DataTable
      rows={templates}
      rowKey={(row) => row.id}
      pageSize={10}
      minTableWidth="min-w-[920px] w-full"
      emptyTitle="No custom templates yet"
      emptyDescription="Install a starter pack above, or create your own checklist in the Templates workspace."
      columns={[
        {
          key: 'name',
          header: 'Name',
          cell: (tpl) => (
            <div>
              <div className="font-medium text-app">{tpl.name}</div>
              {tpl.category ? <div className="text-xs text-muted">{tpl.category}</div> : null}
            </div>
          ),
        },
        { key: 'workType', header: 'Work Type', cell: (tpl) => <span className="text-muted">{workTypeLabel(tpl.workType)}</span> },
        { key: 'sections', header: 'Sections', cell: (tpl) => <span className="tabular-nums text-app">{tpl.sections?.length ?? '—'}</span> },
        { key: 'items', header: 'Items', cell: (tpl) => <span className="tabular-nums text-app">{tpl.checklist?.length ?? '—'}</span> },
        {
          key: 'status',
          header: 'Status',
          cell: (tpl) => (
            <Badge tone={tpl.isActive ? 'success' : 'neutral'}>
              {tpl.isActive ? 'Active' : 'Inactive'}
            </Badge>
          ),
        },
        {
          key: 'actions',
          header: 'Actions',
          cell: (tpl) => (
            <div className="flex items-center justify-end gap-2">
              <Link
                to="/templates"
                className="button-ghost inline-flex items-center gap-1.5 text-xs"
              >
                <ExternalLink className="h-3.5 w-3.5" />
                Edit
              </Link>
              <button
                type="button"
                className="button-ghost inline-flex items-center gap-1.5 text-xs text-[var(--app-badge-danger-text)]"
                onClick={() => onDelete(tpl)}
              >
                <Trash2 className="h-3.5 w-3.5" />
                Delete
              </button>
            </div>
          ),
        },
      ]}
    />
  )
}
