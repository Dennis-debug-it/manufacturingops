import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
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
 * Checklist + report templates built for a specific client, installed into this tenant only.
 * Installing again updates the tenant's copy as a new version (jobs already using it are unaffected).
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
      toast.success(result.created ? `Installed "${result.name}"` : `Updated "${result.name}" to version ${result.version}`)
      void queryClient.invalidateQueries({ queryKey })
    },
  })

  return (
    <section className={className}>
      <SectionTitle
        title="Client templates"
        description="Checklists with their own report layout, built for one client and installed into this workspace only. The workspace then picks the checklist when creating a job; its service report uses the client's layout."
      />
      <div className="mt-4 space-y-3">
        {isLoading ? <Spinner label="Loading client templates" /> : null}
        {isError ? <p className="text-sm text-slate-500">Client templates are managed by platform admins.</p> : null}
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
            <button
              type="button"
              disabled={install.isPending}
              onClick={() => {
                const message = template.installedTemplateId
                  ? `Update "${template.name}" in this workspace? Questions changed in their template editor will be reset to the original.`
                  : `Install "${template.name}" into this workspace?`
                if (window.confirm(message)) install.mutate(template.key)
              }}
              className="inline-flex h-9 shrink-0 items-center justify-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-semibold text-white disabled:opacity-60"
            >
              {install.isPending && install.variables === template.key ? 'Working…' : template.installedTemplateId ? 'Reinstall / update' : 'Install'}
            </button>
          </div>
        ))}
      </div>
    </section>
  )
}
