import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'

interface PlatformLicensePlan {
  id: string
  name: string
  code: string
  monthlyPrice: number
  annualPrice: number
  maxUsers: number | null
  modulesIncluded: string[]
  isActive: boolean
}

const featureModules = ['EmailIngestion', 'MonitoringIntegration']

function FeatureCell({ plan, moduleName, canManage }: { plan: PlatformLicensePlan; moduleName: string; canManage: boolean }) {
  const queryClient = useQueryClient()
  const enabled = plan.modulesIncluded.includes(moduleName)

  const toggle = useMutation({
    mutationFn: () => {
      const nextModules = enabled ? plan.modulesIncluded.filter((m) => m !== moduleName) : [...plan.modulesIncluded, moduleName]
      return api.put<PlatformLicensePlan>(`/api/platform/licensing/plans/${plan.id}`, {
        name: plan.name,
        code: plan.code,
        monthlyPrice: plan.monthlyPrice,
        annualPrice: plan.annualPrice,
        maxUsers: plan.maxUsers,
        modulesIncluded: nextModules,
        isActive: plan.isActive,
      })
    },
    onSuccess: () => {
      toast.success(`${moduleName} ${enabled ? 'disabled' : 'enabled'} for ${plan.name}`)
      void queryClient.invalidateQueries({ queryKey: ['platform', 'licensing', 'plans'] })
    },
  })

  return (
    <td className="px-4 py-3 text-center">
      <input
        type="checkbox"
        checked={enabled}
        disabled={!canManage || toggle.isPending}
        onChange={() => toggle.mutate()}
        className="h-4 w-4 rounded border-slate-300"
      />
    </td>
  )
}

export function FeaturesSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const { data, isLoading, isError } = useQuery({
    queryKey: ['platform', 'licensing', 'plans'],
    queryFn: () => api.get<PlatformLicensePlan[]>('/api/platform/licensing/plans'),
    staleTime: 30_000,
  })

  if (isLoading) {
    return <Spinner label="Loading feature matrix" />
  }

  if (isError) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">License plans could not be loaded.</div>
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Feature Flags" subtitle="Module entitlements per license plan. Toggling a module here changes what tenants on that plan can access." />
      {(data ?? []).length === 0 ? (
        <EmptyState title="No license plans configured" description="Create license plans on the Plans page before assigning module entitlements." />
      ) : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 font-medium text-slate-600">Plan</th>
                {featureModules.map((moduleName) => <th key={moduleName} className="px-4 py-3 text-center font-medium text-slate-600">{moduleName}</th>)}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(data ?? []).map((plan) => (
                <tr key={plan.id}>
                  <td className="px-4 py-3 font-medium text-slate-900">{plan.name}</td>
                  {featureModules.map((moduleName) => <FeatureCell key={moduleName} plan={plan} moduleName={moduleName} canManage={canManage} />)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
