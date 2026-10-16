import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { CheckCircle2 } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { Spinner } from '../../shared/components/Spinner'
import { SectionTitle } from '../common'

interface ClientTemplate {
  key: string
  name: string
  description: string
  installedTemplateId: string | null
  installedVersion: number | null
  installedActive: boolean | null
}

interface InstallResult {
  templateId: string
  name: string
  version: number
  created: boolean
}

/**
 * Checklist + report templates built for a specific client. The API lists only the ones built for this
 * workspace (e.g. Plexus Energy's solar report), so the panel stays hidden everywhere else. Each installs
 * once: after that the button is replaced by its installed state.
 */
export function ClientTemplatesPanel({ tenantId, className }: { tenantId: string; className: string }) {
  const queryClient = useQueryClient()
  const queryKey = ['platform', 'tenants', tenantId, 'client-templates']
  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => api.get<ClientTemplate[]>(endpoints.platform.clientTemplates(tenantId), undefined, { suppressGlobalErrorToast: true }),
    retry: false,
  })
  const install = useMutation({
    mutationFn: (key: string) => api.post<InstallResult>(endpoints.platform.installClientTemplate(tenantId, key)),
    onSuccess: (result) => {
      toast.success(`Installed "${result.name}"`)
      void queryClient.invalidateQueries({ queryKey })
    },
  })

  // Nothing built for this workspace (or no access to manage it): no panel at all.
  if (isError || (!isLoading && (data ?? []).length === 0)) return null

  return (
    <section className={className}>
      <SectionTitle
        title="Client templates"
        description="Checklists with their own report layout, built for this client. Once installed, the workspace picks the checklist when creating a job and its service report uses the client's layout."
      />
      <div className="mt-4 space-y-3">
        {isLoading ? <Spinner label="Loading client templates" /> : null}
        {(data ?? []).map((template) => (
          <div key={template.key} className="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0">
              <p className="text-sm font-semibold text-slate-900">{template.name}</p>
              <p className="mt-0.5 text-sm text-slate-600">{template.description}</p>
              <p className="mt-1 text-xs text-slate-500">
                {template.installedTemplateId
                  ? `Installed · version ${template.installedVersion}${template.installedActive ? '' : ' · inactive'}`
                  : 'Not installed'}
              </p>
            </div>
            {template.installedTemplateId ? (
              <span className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg bg-emerald-50 px-3 text-sm font-medium text-emerald-800">
                <CheckCircle2 className="h-4 w-4" />Installed
              </span>
            ) : (
              <button
                type="button"
                disabled={install.isPending}
                onClick={() => { if (window.confirm(`Install "${template.name}" into this workspace?`)) install.mutate(template.key) }}
                className="inline-flex h-9 shrink-0 items-center justify-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-semibold text-white disabled:opacity-60"
              >
                {install.isPending ? 'Installing…' : 'Install'}
              </button>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}
