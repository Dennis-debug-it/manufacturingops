import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'
import { Field, SectionTitle } from '../common'

interface CrmSettings {
  autoAssignLeads: boolean
  defaultOwnerEmail: string
  followUpReminderDays: number
  staleLeadDays: number
}

export function CrmSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['platform', 'settings', 'crm'],
    queryFn: () => api.get<CrmSettings>('/api/platform/settings/crm'),
    staleTime: 30_000,
  })
  const [values, setValues] = useState<CrmSettings | null>(null)
  useEffect(() => { if (data) setValues(data) }, [data])

  const save = useMutation({
    mutationFn: (payload: CrmSettings) => api.put<CrmSettings>('/api/platform/settings/crm', payload),
    onSuccess: () => {
      toast.success('CRM settings saved')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'settings', 'crm'] })
    },
  })

  if (isError) return <EmptyState title="Couldn’t load CRM settings" description="Something went wrong fetching these settings." actionLabel="Retry" onAction={() => { void refetch() }} />
  if (isLoading || !values) return <Spinner label="Loading CRM settings" />

  const set = <K extends keyof CrmSettings>(key: K, value: CrmSettings[K]) => setValues((current) => (current ? { ...current, [key]: value } : current))

  return (
    <div className="space-y-6">
      <PageHeader
        title="CRM"
        subtitle="Pipeline and lead routing configuration."
        primaryAction={canManage ? <button type="button" onClick={() => save.mutate(values)} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button> : undefined}
      />
      <div className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Lead Routing" description="Control how new inbound leads are assigned to sales owners." />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
            <input type="checkbox" checked={values.autoAssignLeads} disabled={!canManage} onChange={(event) => set('autoAssignLeads', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Auto-assign new leads to a default owner
          </label>
          <Field label="Default Owner Email">
            <input value={values.defaultOwnerEmail} disabled={!canManage} onChange={(event) => set('defaultOwnerEmail', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Follow-up Reminder (days)">
            <input type="number" min={0} value={values.followUpReminderDays} disabled={!canManage} onChange={(event) => set('followUpReminderDays', Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Stale Lead Threshold (days)">
            <input type="number" min={0} value={values.staleLeadDays} disabled={!canManage} onChange={(event) => set('staleLeadDays', Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
        </div>
      </div>
    </div>
  )
}
