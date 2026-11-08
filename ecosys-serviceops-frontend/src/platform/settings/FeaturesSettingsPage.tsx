import { Check, Minus } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PlatformLicensePlan } from '../types'

const modules: ReadonlyArray<[string, string]> = [
  ['operations', 'Work orders, technician app, clients & sites, reports'],
  ['finance', 'Rate cards, preferential and exchange rates'],
  ['maintenance', 'Preventive maintenance, assets, condition monitoring, reliability, leaf fleet, contracts, SLA'],
  ['inventory', 'Materials, purchase orders, stock, tools'],
  ['advanced', 'Workflows, intake, time & attendance, approvals'],
  ['enterprise_tools', 'Multiple branches, API & webhooks, monitoring'],
]

/** What each edition includes. Fixed in code (ServiceOpsPlans.EditionModules), so this page is read-only. */
export function FeaturesSettingsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['platform', 'licensing', 'plans'],
    queryFn: () => api.get<PlatformLicensePlan[]>(endpoints.platformLicensing.plans),
    staleTime: 30_000,
  })

  if (isLoading) return <Spinner label="Loading editions" />
  if (isError) return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Editions could not be loaded.</div>

  return (
    <div className="space-y-4">
      <PageHeader title="Edition Features" subtitle="What each edition includes. The WhatsApp add-ons can be added to any edition from the tenant's page." />
      {(data ?? []).length === 0 ? (
        <EmptyState title="No editions" description="The editions are created by the database migration." />
      ) : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 font-medium text-slate-600">Feature</th>
                {(data ?? []).map((plan) => <th key={plan.id} className="px-4 py-3 text-center font-medium text-slate-600">{plan.name.replace('Ecosys ServiceOps ', '')}</th>)}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {modules.map(([module, label]) => (
                <tr key={module}>
                  <td className="px-4 py-3 text-slate-900">{label}</td>
                  {(data ?? []).map((plan) => (
                    <td key={plan.id} className="px-4 py-3 text-center">
                      {plan.modulesIncluded.includes(module)
                        ? <Check aria-label="Included" className="mx-auto h-4 w-4 text-emerald-600" />
                        : <Minus aria-label="Not included" className="mx-auto h-4 w-4 text-slate-300" />}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
