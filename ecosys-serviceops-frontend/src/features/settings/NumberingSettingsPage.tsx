import { useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'

interface NumberingRule {
  id: string
  documentType: string
  prefix: string | null
  nextNumber: number
  paddingLength: number
  includeYear: boolean
  isActive: boolean
}

function RuleRow({ rule }: { rule: NumberingRule }) {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const [prefix, setPrefix] = useState(rule.prefix ?? '')
  const [nextNumber, setNextNumber] = useState(String(rule.nextNumber))
  const [paddingLength, setPaddingLength] = useState(String(rule.paddingLength))
  const [preview, setPreview] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => api.put<NumberingRule>(`/api/settings/numbering-rules/${rule.id}`, {
      prefix,
      nextNumber: Number(nextNumber),
      paddingLength: Number(paddingLength),
      includeYear: rule.includeYear,
      isActive: rule.isActive,
    }),
    onSuccess: () => {
      toast.success('Numbering rule saved')
      void queryClient.invalidateQueries({ queryKey: ['settings', 'numbering-rules'] })
    },
  })

  const loadPreview = async () => {
    try {
      const result = await api.post<{ preview: string }>('/api/settings/numbering-rules/preview', { ruleId: rule.id })
      setPreview(result.preview ?? null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <tr>
      <td className="px-4 py-3 text-sm font-medium text-slate-900">{rule.documentType}</td>
      <td className="px-4 py-3"><input value={prefix} disabled={!canManage} onChange={(event) => setPrefix(event.target.value)} className="h-9 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" /></td>
      <td className="px-4 py-3"><input type="number" value={nextNumber} disabled={!canManage} onChange={(event) => setNextNumber(event.target.value)} className="h-9 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" /></td>
      <td className="px-4 py-3"><input type="number" value={paddingLength} disabled={!canManage} onChange={(event) => setPaddingLength(event.target.value)} className="h-9 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" /></td>
      <td className="px-4 py-3 text-xs text-slate-500">{preview ?? '—'}</td>
      <td className="px-4 py-3">
        <div className="flex gap-2">
          <button type="button" onClick={loadPreview} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Preview</button>
          {canManage ? <button type="button" onClick={() => save.mutate()} disabled={save.isPending} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Save</button> : null}
        </div>
      </td>
    </tr>
  )
}

export function NumberingSettingsPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'numbering-rules'],
    queryFn: () => api.get<NumberingRule[]>('/api/settings/numbering-rules'),
    staleTime: 30_000,
  })

  if (isLoading) {
    return <Spinner label="Loading numbering rules" />
  }

  if (isError) {
    return (
      <ErrorState
        title="Numbering rules unavailable"
        description="We couldn’t load the document numbering rules. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Numbering Rules" subtitle="Document prefixes, start numbers, and padding — with live preview." />
      <div className="overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
        <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-4 py-3 font-medium text-slate-600">Document Type</th>
              <th className="px-4 py-3 font-medium text-slate-600">Prefix</th>
              <th className="px-4 py-3 font-medium text-slate-600">Next Number</th>
              <th className="px-4 py-3 font-medium text-slate-600">Padding</th>
              <th className="px-4 py-3 font-medium text-slate-600">Preview</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {(data ?? []).map((rule) => <RuleRow key={rule.id} rule={rule} />)}
          </tbody>
        </table>
      </div>
    </div>
  )
}
