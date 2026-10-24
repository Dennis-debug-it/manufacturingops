import { useProjects } from '../../features/projects/api'
import { useEntitlements } from '../hooks/useEntitlements'
import { usePermission } from '../hooks/usePermission'
import { cn } from '../utils/cn'

const defaultSelectClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const defaultLabelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export interface ProjectPickerProps {
  /** Selected project id; '' means no project. */
  value: string
  onChange: (projectId: string) => void
  /** Only list this client's projects. Without it every open project is listed. */
  clientId?: string | null
  id?: string
  disabled?: boolean
  className?: string
  labelClassName?: string
}

/**
 * Optional "Project" select for create forms (work orders, issues, expenses, bills, POs, invoices).
 * Lists open projects only, since the server refuses closed or cancelled ones. Renders nothing when
 * the tenant has no Projects module or the user can't view projects, so forms can drop it in as is.
 */
export function ProjectPicker(props: ProjectPickerProps) {
  const { hasModule } = useEntitlements()
  const canView = usePermission('projects.view')
  if (!hasModule('projects') || !canView) {
    return null
  }
  return <ProjectSelect {...props} />
}

function ProjectSelect({ value, onChange, clientId, id = 'project-picker', disabled = false, className, labelClassName }: ProjectPickerProps) {
  const { data: projects, isLoading } = useProjects(clientId ? { stageCategory: 'Open', clientId } : { stageCategory: 'Open' })

  return (
    <div>
      <label className={labelClassName ?? defaultLabelClass} htmlFor={id}>Project (optional)</label>
      <select id={id} value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)} className={cn(className ?? defaultSelectClass)}>
        <option value="">{isLoading ? 'Loading projects…' : 'Not linked to a project'}</option>
        {(projects ?? []).map((project) => (
          <option key={project.id} value={project.id}>
            {project.projectNumber} — {project.name}
          </option>
        ))}
      </select>
    </div>
  )
}
