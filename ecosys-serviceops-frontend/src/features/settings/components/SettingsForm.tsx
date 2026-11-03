import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import { PageHeader } from '../../../shared/components/PageHeader'
import { Spinner } from '../../../shared/components/Spinner'
import { ErrorState } from '../../../shared/components/ErrorState'
import { usePermission } from '../../../shared/hooks/usePermission'

export type SettingsFieldType = 'text' | 'number' | 'checkbox' | 'textarea' | 'email' | 'color' | 'select'

export interface SettingsFieldOption {
  value: string
  label: string
}

export interface SettingsField {
  key: string
  label: string
  type: SettingsFieldType
  helperText?: string
  /** Required for 'select' fields. */
  options?: SettingsFieldOption[]
}

export interface SettingsFormProps {
  title: string
  subtitle: string
  queryKey: string
  fetchUrl: string
  saveUrl: string
  fields: SettingsField[]
  permissionAction?: string
  /** Shown until the tenant has saved values (backend returns an empty object first time). */
  defaultValues?: Record<string, unknown>
}

export function SettingsForm({ title, subtitle, queryKey, fetchUrl, saveUrl, fields, permissionAction = 'settings.manage', defaultValues }: SettingsFormProps) {
  const canManage = usePermission(permissionAction)
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', queryKey],
    queryFn: () => api.get<Record<string, unknown>>(fetchUrl),
    staleTime: 30_000,
  })

  const [values, setValues] = useState<Record<string, unknown>>(defaultValues ?? {})
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    // Merge stored values over the defaults so first-time (empty) responses still show sensible defaults.
    if (data) setValues({ ...(defaultValues ?? {}), ...data })
  }, [data, defaultValues])

  const setField = (key: string, value: unknown) => setValues((current) => ({ ...current, [key]: value }))

  const save = async () => {
    setSaving(true)
    try {
      await api.put(saveUrl, values)
      toast.success('Settings saved')
      void queryClient.invalidateQueries({ queryKey: ['settings', queryKey] })
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setSaving(false)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading settings" />
  }

  if (isError) {
    return (
      <ErrorState
        title="Settings unavailable"
        description="We couldn’t load these settings. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={title}
        subtitle={subtitle}
        primaryAction={
          canManage ? (
            <button type="button" onClick={save} disabled={saving} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {saving ? 'Saving…' : 'Save Changes'}
            </button>
          ) : undefined
        }
      />
      <div className="grid grid-cols-1 gap-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm sm:grid-cols-2">
        {fields.map((field) => {
          const value = values[field.key]
          if (field.type === 'checkbox') {
            return (
              <label key={field.key} className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
                <input
                  type="checkbox"
                  checked={Boolean(value)}
                  disabled={!canManage}
                  onChange={(event) => setField(field.key, event.target.checked)}
                  className="h-4 w-4 rounded border-slate-300"
                />
                {field.label}
              </label>
            )
          }
          if (field.type === 'select') {
            return (
              <div key={field.key}>
                <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor={field.key}>{field.label}</label>
                <select
                  id={field.key}
                  value={typeof value === 'string' ? value : ''}
                  disabled={!canManage}
                  onChange={(event) => setField(field.key, event.target.value)}
                  className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
                >
                  {(field.options ?? []).map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
                {field.helperText ? <p className="mt-1 text-xs text-slate-500">{field.helperText}</p> : null}
              </div>
            )
          }
          if (field.type === 'textarea') {
            return (
              <div key={field.key} className="sm:col-span-2">
                <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor={field.key}>{field.label}</label>
                <textarea
                  id={field.key}
                  value={typeof value === 'string' ? value : ''}
                  disabled={!canManage}
                  onChange={(event) => setField(field.key, event.target.value)}
                  rows={3}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
                />
                {field.helperText ? <p className="mt-1 text-xs text-slate-500">{field.helperText}</p> : null}
              </div>
            )
          }
          return (
            <div key={field.key}>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor={field.key}>{field.label}</label>
              <input
                id={field.key}
                type={field.type}
                value={typeof value === 'string' || typeof value === 'number' ? value : ''}
                disabled={!canManage}
                onChange={(event) => setField(field.key, field.type === 'number' ? Number(event.target.value) : event.target.value)}
                className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              />
              {field.helperText ? <p className="mt-1 text-xs text-slate-500">{field.helperText}</p> : null}
            </div>
          )
        })}
      </div>
    </div>
  )
}
