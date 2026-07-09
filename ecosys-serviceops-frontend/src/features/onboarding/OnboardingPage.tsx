import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Circle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useBranches } from '../inventory/api'
import { useTeamUsers } from '../team/api'
import { useClients } from '../clients/api'

interface StarterPack {
  id: string
  name: string
  code: string
  description: string | null
  contractorType: string
  isActive: boolean
  version: number
  isInstalled: boolean
}

function useStarterPacks() {
  return useQuery({ queryKey: ['starter-packs'], queryFn: () => api.get<StarterPack[]>(endpoints.starterPacks.list), staleTime: 30_000 })
}

function useInstallStarterPack() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<StarterPack>(endpoints.starterPacks.install(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['starter-packs'] }),
  })
}

export function OnboardingPage() {
  const { data: branches, isLoading: branchesLoading } = useBranches()
  const { data: users, isLoading: usersLoading } = useTeamUsers()
  const { data: clients, isLoading: clientsLoading } = useClients()
  const { data: starterPacks, isLoading: packsLoading } = useStarterPacks()
  const installPack = useInstallStarterPack()

  const install = async (pack: StarterPack) => {
    try {
      await installPack.mutateAsync(pack.id)
      toast.success(`${pack.name} installed`)
    } catch (error) {
      if (error && typeof error === 'object' && 'response' in error && (error as { response?: { status?: number } }).response?.status === 409) {
        toast.error(`${pack.name} is already installed`)
      }
    }
  }

  const isLoading = branchesLoading || usersLoading || clientsLoading || packsLoading

  if (isLoading) {
    return <Spinner label="Loading onboarding checklist" />
  }

  const hasBranch = (branches ?? []).length > 0
  const hasUser = (users ?? []).length > 1
  const hasClient = (clients ?? []).length > 0
  const hasStarterPack = (starterPacks ?? []).some((pack) => pack.isInstalled)

  const requiredSteps = [
    { id: 'branch', label: 'Add at least one branch', done: hasBranch, href: '/settings/company' },
    { id: 'user', label: 'Invite a team member', done: hasUser, href: '/team/users' },
    { id: 'client', label: 'Add your first client', done: hasClient, href: '/clients' },
    { id: 'starter-pack', label: 'Install a starter pack (checklist templates)', done: hasStarterPack, href: '#starter-packs' },
  ]
  const recommendedSteps = [
    { id: 'branding', label: 'Set up company branding', href: '/settings/branding' },
    { id: 'numbering', label: 'Configure document numbering', href: '/settings/numbering' },
    { id: 'pm-templates', label: 'Review PM checklist templates', href: '/settings/pm-templates' },
    { id: 'security', label: 'Configure security policy', href: '/settings/security' },
    { id: 'notifications', label: 'Set up notification preferences', href: '/settings/notifications' },
    { id: 'email', label: 'Connect email intake', href: '/settings/email-intake' },
    { id: 'credit', label: 'Configure credit terms', href: '/finance/rate-cards' },
    { id: 'assets', label: 'Register your first asset', href: '/assets' },
  ]

  const doneCount = requiredSteps.filter((step) => step.done).length
  const progress = Math.round((doneCount / requiredSteps.length) * 100)

  return (
    <div className="space-y-6">
      <PageHeader title="Onboarding" subtitle="Tenant checklist and setup flow." />

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium text-slate-900">Setup progress</span>
          <span className="text-slate-600">{doneCount} / {requiredSteps.length} required steps</span>
        </div>
        <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-slate-100">
          <div className="h-full rounded-full bg-slate-900 transition-all" style={{ width: `${progress}%` }} />
        </div>
      </div>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="text-sm font-semibold text-slate-900">Required Steps</h3>
        <ul className="space-y-2">
          {requiredSteps.map((step) => (
            <li key={step.id}>
              <a href={step.href} className={cn('flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition hover:bg-slate-50', step.done && 'text-slate-400')}>
                {step.done ? <CheckCircle2 className="h-5 w-5 text-emerald-600" /> : <Circle className="h-5 w-5 text-slate-300" />}
                <span className={cn(step.done && 'line-through')}>{step.label}</span>
              </a>
            </li>
          ))}
        </ul>
      </section>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="text-sm font-semibold text-slate-900">Recommended Steps</h3>
        <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {recommendedSteps.map((step) => (
            <li key={step.id}>
              <a href={step.href} className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-50">
                <Circle className="h-4 w-4 text-slate-300" />
                {step.label}
              </a>
            </li>
          ))}
        </ul>
      </section>

      <section id="starter-packs" className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="text-sm font-semibold text-slate-900">Starter Packs</h3>
        <p className="text-sm text-slate-600">Pre-built checklist templates for your contractor type.</p>
        <ul className="space-y-2">
          {(starterPacks ?? []).map((pack) => (
            <li key={pack.id} className="flex items-center justify-between rounded-lg border border-slate-200 px-4 py-3">
              <div>
                <div className="text-sm font-medium text-slate-900">{pack.name}</div>
                <div className="text-xs text-slate-500">{pack.description ?? pack.contractorType}</div>
              </div>
              {pack.isInstalled ? (
                <span className="text-xs font-medium text-emerald-700">Installed</span>
              ) : (
                <button type="button" onClick={() => install(pack)} disabled={installPack.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
                  Install
                </button>
              )}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
