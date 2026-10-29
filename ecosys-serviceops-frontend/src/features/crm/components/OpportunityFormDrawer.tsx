import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { getActiveCurrency } from '../../../shared/utils/currency'
import { useClients, useSites } from '../../clients/api'
import { workTypeOptions } from '../../work-orders/schemas'
import { useAuth } from '../../../shared/hooks/useAuth'
import { useCreateOpportunity, useUpdateOpportunity } from '../api'
import { OwnerSelect } from './OwnerSelect'
import { sourceOptions, type SalesOpportunityDetail } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface FormState {
  title: string
  partyMode: 'prospect' | 'client'
  clientId: string
  siteId: string
  companyName: string
  contactName: string
  contactEmail: string
  contactPhone: string
  location: string
  source: string
  serviceType: string
  description: string
  estimatedValue: string
  currency: string
  expectedCloseDate: string
  ownerUserId: string
}

function toForm(opportunity: SalesOpportunityDetail | null | undefined, currentUserId: string, defaultClientId?: string): FormState {
  const clientId = opportunity ? (opportunity.clientId ?? '') : (defaultClientId ?? '')
  return {
    title: opportunity?.title ?? '',
    partyMode: clientId ? 'client' : 'prospect',
    clientId,
    siteId: opportunity?.siteId ?? '',
    companyName: opportunity?.companyName ?? '',
    contactName: opportunity?.contactName ?? '',
    contactEmail: opportunity?.contactEmail ?? '',
    contactPhone: opportunity?.contactPhone ?? '',
    location: opportunity?.location ?? '',
    source: opportunity?.source ?? '',
    serviceType: opportunity?.serviceType ?? '',
    description: opportunity?.description ?? '',
    estimatedValue: opportunity ? String(opportunity.estimatedValue) : '',
    currency: opportunity?.currency ?? getActiveCurrency(),
    expectedCloseDate: opportunity?.expectedCloseDate ? opportunity.expectedCloseDate.slice(0, 10) : '',
    ownerUserId: opportunity ? (opportunity.ownerUserId ?? '') : currentUserId,
  }
}

const orNull = (value: string) => (value.trim() ? value.trim() : null)

export function OpportunityFormDrawer({ open, onClose, opportunity, defaultClientId }: { open: boolean; onClose: () => void; opportunity?: SalesOpportunityDetail | null; defaultClientId?: string }) {
  const navigate = useNavigate()
  const isEdit = Boolean(opportunity)
  const { user } = useAuth()
  const currentUserId = user?.userId ?? ''
  const [form, setForm] = useState<FormState>(() => toForm(opportunity, currentUserId, defaultClientId))
  const { data: clients } = useClients()
  const { data: sites } = useSites(form.partyMode === 'client' && form.clientId ? form.clientId : undefined)
  const create = useCreateOpportunity()
  const update = useUpdateOpportunity(opportunity?.id ?? '')
  const saving = create.isPending || update.isPending

  useEffect(() => {
    if (open) setForm(toForm(opportunity, currentUserId, defaultClientId))
  }, [open, opportunity, currentUserId, defaultClientId])

  const set = (patch: Partial<FormState>) => setForm((current) => ({ ...current, ...patch }))

  const submit = async () => {
    if (!form.title.trim()) {
      toast.error('Give the opportunity a title')
      return
    }
    if (form.partyMode === 'client' && !form.clientId) {
      toast.error('Pick the client')
      return
    }
    if (form.partyMode === 'prospect' && !form.companyName.trim() && !form.contactName.trim()) {
      toast.error("Enter the prospect's company or contact name")
      return
    }
    const payload = {
      title: form.title.trim(),
      clientId: form.partyMode === 'client' ? form.clientId : null,
      siteId: form.partyMode === 'client' ? orNull(form.siteId) : null,
      companyName: orNull(form.companyName),
      contactName: orNull(form.contactName),
      contactEmail: orNull(form.contactEmail),
      contactPhone: orNull(form.contactPhone),
      location: orNull(form.location),
      source: orNull(form.source),
      serviceType: orNull(form.serviceType),
      description: orNull(form.description),
      estimatedValue: Number(form.estimatedValue) || 0,
      currency: form.currency.trim().toUpperCase() || getActiveCurrency(),
      probability: null,
      expectedCloseDate: form.expectedCloseDate ? new Date(form.expectedCloseDate).toISOString() : null,
      ownerUserId: form.ownerUserId || null,
    }
    try {
      if (isEdit) {
        await update.mutateAsync(payload)
        toast.success('Opportunity updated')
        onClose()
      } else {
        const created = await create.mutateAsync(payload)
        toast.success('Opportunity added to the pipeline')
        onClose()
        navigate(`/crm/opportunities/${created.id}`)
      }
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title={isEdit ? 'Edit Opportunity' : 'New Opportunity'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={saving} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {saving ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Opportunity'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="opp-title">Title</label>
          <input id="opp-title" value={form.title} onChange={(event) => set({ title: event.target.value })} className={fieldClass} placeholder="e.g. Standby generator supply and install" />
        </div>

        <div>
          <span className={labelClass}>Who is this deal with?</span>
          <div className="inline-flex rounded-lg border border-slate-200 p-1">
            {(['prospect', 'client'] as const).map((mode) => (
              <button
                key={mode}
                type="button"
                onClick={() => set({ partyMode: mode })}
                className={`h-9 rounded-md px-3 text-sm font-medium transition ${form.partyMode === mode ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50'}`}
              >
                {mode === 'prospect' ? 'New prospect' : 'Existing client'}
              </button>
            ))}
          </div>
        </div>

        {form.partyMode === 'client' ? (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className={labelClass} htmlFor="opp-client">Client</label>
              <select id="opp-client" value={form.clientId} onChange={(event) => set({ clientId: event.target.value, siteId: '' })} className={fieldClass}>
                <option value="">Select client</option>
                {(clients ?? []).map((client) => (
                  <option key={client.id} value={client.id}>{client.clientName}</option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="opp-site">Site (optional)</label>
              <select id="opp-site" value={form.siteId} onChange={(event) => set({ siteId: event.target.value })} className={fieldClass} disabled={!form.clientId}>
                <option value="">No specific site</option>
                {(sites ?? []).map((site) => (
                  <option key={site.id} value={site.id}>{site.siteName}</option>
                ))}
              </select>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className={labelClass} htmlFor="opp-company">Company</label>
              <input id="opp-company" value={form.companyName} onChange={(event) => set({ companyName: event.target.value })} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="opp-location">Location</label>
              <input id="opp-location" value={form.location} onChange={(event) => set({ location: event.target.value })} className={fieldClass} />
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="opp-contact">Contact person</label>
            <input id="opp-contact" value={form.contactName} onChange={(event) => set({ contactName: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-email">Email</label>
            <input id="opp-email" type="email" value={form.contactEmail} onChange={(event) => set({ contactEmail: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-phone">Phone</label>
            <input id="opp-phone" value={form.contactPhone} onChange={(event) => set({ contactPhone: event.target.value })} className={fieldClass} />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="opp-value">Estimated value</label>
            <input id="opp-value" type="number" min="0" value={form.estimatedValue} onChange={(event) => set({ estimatedValue: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-currency">Currency</label>
            <input id="opp-currency" value={form.currency} onChange={(event) => set({ currency: event.target.value })} className={fieldClass} maxLength={10} />
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-close">Expected close</label>
            <input id="opp-close" type="date" value={form.expectedCloseDate} onChange={(event) => set({ expectedCloseDate: event.target.value })} className={fieldClass} />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="opp-service">Type of work</label>
            <select id="opp-service" value={form.serviceType} onChange={(event) => set({ serviceType: event.target.value })} className={fieldClass}>
              <option value="">Not decided yet</option>
              {workTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-owner">Owner</label>
            <OwnerSelect id="opp-owner" value={form.ownerUserId} onChange={(ownerUserId) => set({ ownerUserId })} className={fieldClass} emptyLabel="Unassigned" />
          </div>
          <div>
            <label className={labelClass} htmlFor="opp-source">Source</label>
            <select id="opp-source" value={form.source} onChange={(event) => set({ source: event.target.value })} className={fieldClass}>
              <option value="">Unknown</option>
              {sourceOptions.map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className={labelClass} htmlFor="opp-description">Scope / notes</label>
          <textarea id="opp-description" value={form.description} onChange={(event) => set({ description: event.target.value })} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="What the customer needs. This becomes the work order description when the deal is won." />
        </div>
      </div>
    </FormDrawer>
  )
}
