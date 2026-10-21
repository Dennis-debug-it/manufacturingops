import { useState } from 'react'
import { useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Pencil, ArrowRightLeft } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DetailLayout, type DetailTab } from '../../shared/components/DetailLayout'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { useChangeProjectStage, useProject, useProjectStages } from './api'
import { ProjectFormDrawer } from './components/ProjectFormDrawer'
import { StageBadge } from './components/ProjectBadges'
import { OverviewTab } from './components/OverviewTab'
import { MilestonesTab } from './components/MilestonesTab'
import { TasksTab } from './components/TasksTab'
import { TeamTab } from './components/TeamTab'
import { VariationsTab } from './components/VariationsTab'
import { LinkedRecordsTab } from './components/LinkedRecordsTab'
import { DocumentsTab } from './components/DocumentsTab'
import { fieldClass, labelClass, secondaryButtonClass, textAreaClass, textOrNull } from './components/formStyles'
import { stageCategoryLabels } from './types'

const finalCategories = ['Closed', 'Cancelled']

export function ProjectDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { data: project, isLoading, isError } = useProject(id)
  const { data: stages } = useProjectStages()
  const { hasModule } = useEntitlements()
  const changeStage = useChangeProjectStage(id)
  const [editing, setEditing] = useState(false)
  const [stageDialog, setStageDialog] = useState(false)
  const [stageId, setStageId] = useState('')
  const [stageNote, setStageNote] = useState('')

  if (isLoading) return <Spinner label="Loading project" />
  if (isError || !project) return <EmptyState title="Project unavailable" description="This project could not be loaded, or you don't have access to it." />

  const { summary, abilities, features } = project
  const isFinal = finalCategories.includes(summary.stageCategory)
  const readOnly = isFinal || !abilities.canManage
  const targetStage = stages?.find((stage) => stage.id === stageId)
  const needsNote = isFinal || targetStage?.category === 'Closed'
  // Closing, cancelling and reopening need projects.close; other moves only need projects.manage.
  const selectableStages = (stages ?? []).filter((stage) => stage.isActive && stage.id !== summary.stageId
    && (abilities.canClose || (!isFinal && !finalCategories.includes(stage.category))))

  const openStageDialog = () => {
    setStageId('')
    setStageNote('')
    setStageDialog(true)
  }

  const confirmStage = async () => {
    if (!stageId) return
    try {
      await changeStage.mutateAsync({ stageId, note: textOrNull(stageNote) })
      toast.success(`Moved to ${targetStage?.name ?? 'the new stage'}`)
      setStageDialog(false)
    } catch {
      // the API error toast explains what blocked the move
    }
  }

  const tabs: DetailTab[] = [
    { id: 'overview', label: 'Overview', content: <OverviewTab project={project} /> },
    ...(features.milestones ? [{ id: 'milestones', label: 'Milestones', content: <MilestonesTab projectId={id} readOnly={readOnly} /> }] : []),
    ...(features.tasks ? [{ id: 'tasks', label: 'Tasks', content: <TasksTab project={project} readOnly={readOnly} /> }] : []),
    { id: 'team', label: 'Team', content: <TeamTab project={project} readOnly={readOnly} /> },
    ...(features.variations ? [{ id: 'variations', label: 'Variations', content: <VariationsTab project={project} /> }] : []),
    // A project's work orders and material issues live in the Operations module.
    ...(hasModule('operations') ? [{ id: 'work', label: features.materials ? 'Work & Materials' : 'Work Orders', content: <LinkedRecordsTab project={project} readOnly={readOnly} showMaterials={features.materials} /> }] : []),
    ...(features.documents ? [{ id: 'documents', label: 'Documents', content: <DocumentsTab project={project} readOnly={readOnly} /> }] : []),
  ]

  return (
    <>
      <DetailLayout
        header={
          <PageHeader
            title={summary.name}
            subtitle={`${summary.projectNumber} · ${summary.clientName}${summary.siteName ? ` · ${summary.siteName}` : ''}`}
            breadcrumbs={[{ label: 'Projects', to: '/projects' }, { label: summary.projectNumber }]}
            titleAdornment={<StageBadge name={summary.stageName} category={summary.stageCategory} />}
            secondaryActions={abilities.canManage && !isFinal ? (
              <button type="button" onClick={() => setEditing(true)} className={secondaryButtonClass}><Pencil className="h-4 w-4" />Edit</button>
            ) : undefined}
            primaryAction={abilities.canManage && selectableStages.length > 0 ? (
              <button type="button" onClick={openStageDialog} className={secondaryButtonClass}><ArrowRightLeft className="h-4 w-4" />{isFinal ? 'Reopen' : 'Change stage'}</button>
            ) : undefined}
          />
        }
        tabs={tabs}
      />

      <ProjectFormDrawer open={editing} onClose={() => setEditing(false)} project={project} />

      <ConfirmDialog
        open={stageDialog}
        title={isFinal ? `Reopen ${summary.projectNumber}` : `Change stage of ${summary.projectNumber}`}
        message={`Currently ${summary.stageName}. ${isFinal ? 'Reopening needs a reason and is recorded in the audit log.' : 'Closing checks that no variations or material issues are still open.'}`}
        confirmLabel="Move"
        confirmDisabled={!stageId || (needsNote && !stageNote.trim()) || changeStage.isPending}
        danger={targetStage?.category === 'Cancelled'}
        onCancel={() => setStageDialog(false)}
        onConfirm={() => void confirmStage()}
      >
        <div className="space-y-3 text-left">
          <div>
            <label className={labelClass} htmlFor="stage-target">New stage</label>
            <select id="stage-target" value={stageId} onChange={(event) => setStageId(event.target.value)} className={fieldClass}>
              <option value="">Select a stage</option>
              {selectableStages.map((stage) => (
                <option key={stage.id} value={stage.id}>{stage.name} ({stageCategoryLabels[stage.category]})</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="stage-note">{needsNote ? 'Reason (required)' : 'Note (optional)'}</label>
            <textarea id="stage-note" rows={2} value={stageNote} onChange={(event) => setStageNote(event.target.value)} className={textAreaClass} />
          </div>
        </div>
      </ConfirmDialog>
    </>
  )
}
