import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { useAuth } from '../../shared/hooks/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'

interface EmailIdentity {
  emailAddress: string | null
  displayName: string
  replyToAddress: string | null
  inboundEnabled: boolean
  outboundEnabled: boolean
  emailSignature: string | null
}

export function EmailIdentitySettingsPage() {
  const { user, tenant } = useAuth()
  const canManage = user?.role === 'admin' || user?.role === 'super_admin'
  const queryClient = useQueryClient()
  const identity = useQuery({ queryKey: ['tenant-email-identity', tenant?.tenantId], queryFn: () => api.get<EmailIdentity>('/api/tenant/email-identity'), enabled: canManage })
  const [draft, setDraft] = useState<EmailIdentity | null>(null)
  const values = draft ?? identity.data
  const save = useMutation({
    mutationFn: (value: EmailIdentity) => api.put<EmailIdentity>('/api/tenant/email-identity', {
      displayName: value.displayName.trim(), replyToAddress: value.replyToAddress?.trim() || null,
      inboundEnabled: value.inboundEnabled, outboundEnabled: value.outboundEnabled,
      emailSignature: value.emailSignature?.trim() || null,
    }),
    onSuccess: (value) => {
      queryClient.setQueryData(['tenant-email-identity', tenant?.tenantId], value)
      setDraft(null)
      toast.success('Workspace email settings saved')
    },
  })
  const set = <K extends keyof EmailIdentity>(key: K, value: EmailIdentity[K]) => { if (values) setDraft({ ...values, [key]: value }) }
  const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900'

  return <div className="space-y-6">
    <PageHeader title="Workspace Email" subtitle="Your ServiceOps-managed email identity and communication preferences." />
    {!canManage ? <p className="text-sm text-slate-600">Your workspace administrator manages these email preferences.</p>
      : identity.isLoading ? <Spinner label="Loading workspace email" />
      : !values ? <p role="alert" className="text-sm text-slate-600">Workspace email settings could not be loaded.</p>
      : <form onSubmit={(event) => { event.preventDefault(); save.mutate(values) }} className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-emerald-800">ServiceOps Email</p>
          <p className="mt-1 break-all text-sm font-medium text-emerald-950">{values.emailAddress ?? 'Contact your platform administrator to provision your address.'}</p>
          <p className="mt-1 text-xs text-emerald-800">Automatically provisioned and managed by the platform.</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="space-y-2 text-sm text-slate-700"><span>Display Name</span><input required maxLength={150} value={values.displayName} onChange={(event) => set('displayName', event.target.value)} className={inputClass} /></label>
          <label className="space-y-2 text-sm text-slate-700"><span>Reply-to Address</span><input type="email" value={values.replyToAddress ?? ''} onChange={(event) => set('replyToAddress', event.target.value)} className={inputClass} /></label>
        </div>
        <label className="block space-y-2 text-sm text-slate-700"><span>Email Signature</span><textarea rows={4} maxLength={4000} value={values.emailSignature ?? ''} onChange={(event) => set('emailSignature', event.target.value)} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" /></label>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={values.inboundEnabled} onChange={(event) => set('inboundEnabled', event.target.checked)} />Enable inbound email</label>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={values.outboundEnabled} onChange={(event) => set('outboundEnabled', event.target.checked)} />Enable outbound email</label>
        <button disabled={save.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button>
      </form>}
  </div>
}
