import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PlatformLicenseAddOn, PlatformLicensePlan } from '../types'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

const moduleLabels: Record<string, string> = {
  operations: 'Work orders',
  finance: 'Quotes & invoices',
  maintenance: 'Maintenance',
  inventory: 'Inventory',
  projects: 'Projects',
  advanced: 'Advanced tools',
  enterprise_tools: 'Branches, API & monitoring',
}

function NumberField({ label, value, onChange, min = 0 }: { label: string; value: number; onChange: (value: number) => void; min?: number }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-700">{label}</label>
      <input type="number" min={min} value={value} onChange={(event) => onChange(Number(event.target.value))} className={inputClass} />
    </div>
  )
}

function ActiveToggle({ checked, onChange }: { checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm text-slate-700">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
      Available to assign
    </label>
  )
}

type EditionForm = Pick<PlatformLicensePlan, 'officeSeatMonthlyPrice' | 'officeSeatAnnualPrice' | 'fieldSeatMonthlyPrice' | 'fieldSeatAnnualPrice' | 'minOfficeSeats' | 'isActive'>

function EditionDrawer({ plan, onClose }: { plan: PlatformLicensePlan | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<EditionForm | null>(null)
  useEffect(() => setForm(plan), [plan])

  const save = useMutation({
    mutationFn: () => api.put<PlatformLicensePlan>(`${endpoints.platformLicensing.plans}/${plan?.id}`, form),
    onSuccess: () => {
      toast.success('Edition updated')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'licensing', 'plans'] })
      onClose()
    },
  })
  const set = (patch: Partial<EditionForm>) => setForm((current) => (current ? { ...current, ...patch } : current))

  return (
    <FormDrawer open={Boolean(plan)} title={plan ? `Edit ${plan.name}` : ''} onClose={onClose}>
      {form ? (
        <div className="space-y-4">
          <p className="text-sm text-slate-600">New prices apply to new licenses and at each tenant's next renewal. They never change a current term.</p>
          <div className="grid grid-cols-2 gap-4">
            <NumberField label="Office seat / month" value={form.officeSeatMonthlyPrice} onChange={(value) => set({ officeSeatMonthlyPrice: value })} />
            <NumberField label="Office seat / year" value={form.officeSeatAnnualPrice} onChange={(value) => set({ officeSeatAnnualPrice: value })} />
            <NumberField label="Field seat / month" value={form.fieldSeatMonthlyPrice} onChange={(value) => set({ fieldSeatMonthlyPrice: value })} />
            <NumberField label="Field seat / year" value={form.fieldSeatAnnualPrice} onChange={(value) => set({ fieldSeatAnnualPrice: value })} />
          </div>
          <NumberField label="Minimum office seats" min={1} value={form.minOfficeSeats} onChange={(value) => set({ minOfficeSeats: value })} />
          <ActiveToggle checked={form.isActive} onChange={(isActive) => set({ isActive })} />
          <button type="button" disabled={save.isPending} onClick={() => save.mutate()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {save.isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      ) : null}
    </FormDrawer>
  )
}

type AddOnForm = Pick<PlatformLicenseAddOn, 'monthlyPrice' | 'annualPrice' | 'includedMessages' | 'extraMessagePrice' | 'extraNumberPrice' | 'isActive'>

function AddOnDrawer({ addOn, onClose }: { addOn: PlatformLicenseAddOn | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<AddOnForm | null>(null)
  useEffect(() => setForm(addOn), [addOn])

  const save = useMutation({
    mutationFn: () => api.put<PlatformLicenseAddOn>(`${endpoints.platformLicensing.addOns}/${addOn?.id}`, form),
    onSuccess: () => {
      toast.success('Add-on updated')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'licensing', 'add-ons'] })
      onClose()
    },
  })
  const set = (patch: Partial<AddOnForm>) => setForm((current) => (current ? { ...current, ...patch } : current))

  return (
    <FormDrawer open={Boolean(addOn)} title={addOn ? `Edit ${addOn.name}` : ''} onClose={onClose}>
      {form ? (
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <NumberField label="Monthly fee" value={form.monthlyPrice} onChange={(value) => set({ monthlyPrice: value })} />
            <NumberField label="Annual fee" value={form.annualPrice} onChange={(value) => set({ annualPrice: value })} />
            <NumberField label="Messages included / month" value={form.includedMessages} onChange={(value) => set({ includedMessages: value })} />
            <NumberField label="Price per extra message" value={form.extraMessagePrice} onChange={(value) => set({ extraMessagePrice: value })} />
          </div>
          {addOn?.code === 'whatsapp_business' ? (
            <NumberField label="Extra dedicated number / month" value={form.extraNumberPrice} onChange={(value) => set({ extraNumberPrice: value })} />
          ) : null}
          <ActiveToggle checked={form.isActive} onChange={(isActive) => set({ isActive })} />
          <button type="button" disabled={save.isPending} onClick={() => save.mutate()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {save.isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      ) : null}
    </FormDrawer>
  )
}

const money = (value: number) => value.toLocaleString()

export function PlansSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const plans = useQuery({
    queryKey: ['platform', 'licensing', 'plans'],
    queryFn: () => api.get<PlatformLicensePlan[]>(endpoints.platformLicensing.plans),
    staleTime: 30_000,
  })
  const addOns = useQuery({
    queryKey: ['platform', 'licensing', 'add-ons'],
    queryFn: () => api.get<PlatformLicenseAddOn[]>(endpoints.platformLicensing.addOns),
    staleTime: 30_000,
  })
  const [editingPlan, setEditingPlan] = useState<PlatformLicensePlan | null>(null)
  const [editingAddOn, setEditingAddOn] = useState<PlatformLicenseAddOn | null>(null)
  const editButton = (onClick: () => void) => (canManage ? <button type="button" onClick={onClick} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Edit</button> : null)

  const planColumns: DataTableColumn<PlatformLicensePlan>[] = [
    { key: 'name', header: 'Edition', cell: (row) => <span className="font-medium text-slate-900">{row.name}</span> },
    { key: 'office', header: 'Office seat / mo', cell: (row) => money(row.officeSeatMonthlyPrice) },
    { key: 'field', header: 'Field seat / mo', cell: (row) => money(row.fieldSeatMonthlyPrice) },
    { key: 'min', header: 'Min office seats', cell: (row) => row.minOfficeSeats },
    { key: 'modules', header: 'Includes', cell: (row) => row.modulesIncluded.map((module) => moduleLabels[module] ?? module).join(', ') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={!row.isActive ? 'Inactive' : row.isPriced ? 'Active' : 'Not priced'} /> },
    { key: 'actions', header: '', cell: (row) => editButton(() => setEditingPlan(row)) },
  ]
  const addOnColumns: DataTableColumn<PlatformLicenseAddOn>[] = [
    { key: 'name', header: 'Add-on', cell: (row) => <span className="font-medium text-slate-900">{row.name}</span> },
    { key: 'monthly', header: 'Monthly', cell: (row) => money(row.monthlyPrice) },
    { key: 'included', header: 'Messages included', cell: (row) => row.includedMessages.toLocaleString() },
    { key: 'extra', header: 'Extra message', cell: (row) => money(row.extraMessagePrice) },
    { key: 'number', header: 'Extra number / mo', cell: (row) => (row.code === 'whatsapp_business' ? money(row.extraNumberPrice) : '—') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'actions', header: '', cell: (row) => editButton(() => setEditingAddOn(row)) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Editions & Pricing" subtitle="Seat prices for each edition, and the optional WhatsApp add-ons. Which features each edition includes is fixed." />
      <DataTable
        columns={planColumns}
        data={plans.data ?? []}
        loading={plans.isLoading}
        emptyTitle={plans.isError ? 'Editions unavailable' : 'No editions'}
        emptyDescription={plans.isError ? 'Editions could not be loaded.' : 'The editions are created by the database migration.'}
      />
      <h2 className="pt-2 text-sm font-semibold text-slate-900">Add-ons</h2>
      <DataTable
        columns={addOnColumns}
        data={addOns.data ?? []}
        loading={addOns.isLoading}
        emptyTitle={addOns.isError ? 'Add-ons unavailable' : 'No add-ons'}
        emptyDescription={addOns.isError ? 'Add-ons could not be loaded.' : 'The add-ons are created by the database migration.'}
      />
      <EditionDrawer plan={editingPlan} onClose={() => setEditingPlan(null)} />
      <AddOnDrawer addOn={editingAddOn} onClose={() => setEditingAddOn(null)} />
    </div>
  )
}
