import { useState } from 'react'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { usePmTemplates, useDuplicatePmTemplate, useSetPmTemplateActive, useDeletePmTemplate } from '../maintenance/api'
import { PmTemplateEditorDrawer } from './components/PmTemplateEditorDrawer'
import { StarterPacks } from './components/StarterPacks'
import type { PmTemplate } from '../maintenance/types'

function RowActions({ template, onEdit }: { template: PmTemplate; onEdit: () => void }) {
  const canManage = usePermission('pm-templates.manage')
  const duplicate = useDuplicatePmTemplate(template.id)
  const setActive = useSetPmTemplateActive(template.id)
  const deleteTemplate = useDeletePmTemplate()
  const [confirmDelete, setConfirmDelete] = useState(false)

  if (!canManage) return null

  return (
    <div className="flex flex-wrap justify-end gap-2">
      <button type="button" onClick={onEdit} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
        Edit
      </button>
      <button type="button" onClick={() => duplicate.mutate()} disabled={duplicate.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
        Duplicate
      </button>
      <button type="button" onClick={() => setActive.mutate(!template.isActive)} disabled={setActive.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
        {template.isActive ? 'Deactivate' : 'Activate'}
      </button>
      {!template.isPlatformSeed ? (
        <button type="button" onClick={() => setConfirmDelete(true)} className="rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50">
          Delete
        </button>
      ) : null}
      <ConfirmDialog
        open={confirmDelete}
        title="Delete PM template"
        message={`Delete "${template.name}"? Existing PM schedules that reference it will keep their historical checklist data.`}
        confirmLabel="Delete"
        danger
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => {
          deleteTemplate.mutate(template.id)
          setConfirmDelete(false)
        }}
      />
    </div>
  )
}

export function PmTemplatesSettingsPage() {
  const canManage = usePermission('pm-templates.manage')
  const { data, isLoading, isError } = usePmTemplates()
  const [editorState, setEditorState] = useState<{ open: boolean; templateId: string | null }>({ open: false, templateId: null })

  const columns: DataTableColumn<PmTemplate>[] = [
    { key: 'name', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.name}{row.isPlatformSeed ? <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-medium text-slate-500">Platform</span> : null}</span> },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'workType', header: 'Work Type', cell: (row) => row.workType },
    { key: 'defaultIntervalMonths', header: 'Interval', cell: (row) => row.defaultIntervalMonths ? `${row.defaultIntervalMonths} mo` : '—' },
    { key: 'checklist', header: 'Checklist Items', cell: (row) => row.checklist.length },
    { key: 'version', header: 'Version', cell: (row) => `v${row.version}` },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'actions', header: '', cell: (row) => <RowActions template={row} onEdit={() => setEditorState({ open: true, templateId: row.id })} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="PM Templates"
        subtitle="Reusable preventive maintenance checklists used when scheduling PM plans."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setEditorState({ open: true, templateId: null })} className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Template
            </button>
          ) : undefined
        }
      />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'PM templates unavailable' : 'No PM templates yet'}
        emptyDescription={isError ? 'PM templates could not be loaded.' : 'Create your first template to standardise preventive maintenance checklists.'}
      />

      <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4">
        <StarterPacks />
      </div>

      <PmTemplateEditorDrawer
        open={editorState.open}
        templateId={editorState.templateId}
        onClose={() => setEditorState({ open: false, templateId: null })}
      />
    </div>
  )
}
