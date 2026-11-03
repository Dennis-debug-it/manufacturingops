import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { CheckCircle2, ClipboardCheck, Package } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { usePermission } from '../../../shared/hooks/usePermission'

interface StarterPack {
  id: string
  name: string
  code: string
  description: string | null
  contractorType: string
  isActive: boolean
  isInstalled: boolean
  assetCategories: Array<{ id: string; name: string; fieldCount: number }>
  checklistTemplates: Array<{ id: string; name: string; workType: string; sectionCount: number; itemCount: number }>
}

export function useStarterPacks(enabled = true) {
  return useQuery({
    queryKey: ['starter-packs'],
    queryFn: () => api.get<StarterPack[]>(endpoints.starterPacks.list),
    enabled,
    staleTime: 60_000,
  })
}

function useInstallStarterPack() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<StarterPack>(endpoints.starterPacks.install(id), {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['starter-packs'] })
      void queryClient.invalidateQueries({ queryKey: ['pm-templates'] })
      // Asset categories are read under a few keys across the app.
      void queryClient.invalidateQueries({ predicate: (query) => String(query.queryKey[0]).includes('asset') })
    },
  })
}

/**
 * Ready-made asset categories (with datasheet fields) and service checklists for a trade. Installing
 * copies them into the workspace, where they can be edited or removed like anything built by hand.
 */
export function StarterPacks({ compact = false }: { compact?: boolean }) {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError } = useStarterPacks(canManage)
  const install = useInstallStarterPack()

  if (!canManage) return null
  if (isLoading) return <p className="text-sm text-slate-500">Loading starter packs…</p>
  if (isError) return <p className="text-sm text-rose-600">Starter packs could not be loaded.</p>
  const packs = (Array.isArray(data) ? data : []).filter((pack) => pack.isActive)
  if (packs.length === 0) return null

  const onInstall = async (pack: StarterPack) => {
    try {
      await install.mutateAsync(pack.id)
      toast.success(`${pack.name} installed — ${pack.assetCategories.length} asset categories and ${pack.checklistTemplates.length} checklists added`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3" aria-label="Starter packs">
      <div>
        <h2 className="text-base font-semibold text-slate-900">Starter packs for your trade</h2>
        <p className="mt-1 text-sm text-slate-600">
          Ready-made asset categories with datasheet fields, plus preventive and breakdown checklists. Installing adds copies to your
          workspace that you can edit or remove at any time.
        </p>
      </div>
      <div className={compact ? 'grid grid-cols-1 gap-3 lg:grid-cols-3' : 'grid grid-cols-1 gap-3 xl:grid-cols-3'}>
        {packs.map((pack) => (
          <article key={pack.id} className="flex flex-col rounded-xl border border-slate-200 bg-white p-4 shadow-sm" data-testid={`starter-pack-${pack.code}`}>
            <div className="flex items-start justify-between gap-2">
              <h3 className="text-sm font-semibold text-slate-900">{pack.name}</h3>
              {pack.isInstalled ? (
                <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">
                  <CheckCircle2 className="h-3.5 w-3.5" /> Installed
                </span>
              ) : null}
            </div>
            {pack.description ? <p className="mt-1 text-xs text-slate-500">{pack.description}</p> : null}

            <div className="mt-3 space-y-1">
              <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500"><ClipboardCheck className="h-3.5 w-3.5" /> Checklists</p>
              <ul className="space-y-0.5 text-sm text-slate-700">
                {pack.checklistTemplates.map((checklist) => (
                  <li key={checklist.id}>
                    {checklist.name}
                    <span className="text-xs text-slate-500"> · {checklist.sectionCount} {checklist.sectionCount === 1 ? 'section' : 'sections'}, {checklist.itemCount} items</span>
                  </li>
                ))}
              </ul>
            </div>

            <div className="mt-3 space-y-1">
              <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500"><Package className="h-3.5 w-3.5" /> Asset categories</p>
              <div className="flex flex-wrap gap-1">
                {pack.assetCategories.map((category) => (
                  <span key={category.id} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700" title={category.fieldCount ? `${category.fieldCount} datasheet fields` : undefined}>
                    {category.name}
                  </span>
                ))}
              </div>
            </div>

            <div className="mt-auto pt-4">
              {pack.isInstalled ? (
                <p className="text-xs text-slate-500">Find these under PM Templates and Asset Categories.</p>
              ) : (
                <button
                  type="button"
                  disabled={install.isPending}
                  onClick={() => void onInstall(pack)}
                  className="inline-flex h-10 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
                >
                  {install.isPending && install.variables === pack.id ? 'Installing…' : `Install ${pack.contractorType} pack`}
                </button>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}
