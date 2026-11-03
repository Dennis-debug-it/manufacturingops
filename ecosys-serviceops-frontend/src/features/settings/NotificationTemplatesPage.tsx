import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Bell, MapPin, CheckCircle2, Truck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import {
  useEtaNotificationSettings,
  useUpdateEtaNotificationSetting,
  type EtaNotificationEventType,
  type EtaNotificationSetting,
} from './etaNotificationsApi'

const eventMeta: Record<EtaNotificationEventType, { label: string; description: string; icon: typeof Bell }> = {
  Assigned: { label: 'Technician Assigned', description: 'Sent when a technician is assigned to the job.', icon: Bell },
  EnRoute: { label: 'En Route', description: 'Sent when the technician departs for the site.', icon: Truck },
  Arrived: { label: 'Arrived on Site', description: 'Sent when the technician marks arrival.', icon: MapPin },
  Completed: { label: 'Job Completed', description: 'Sent when the work order is marked complete.', icon: CheckCircle2 },
}

const placeholders = ['clientName', 'technicianName', 'woNumber', 'trackingLink']

function TemplateEditor({ setting, onClose }: { setting: EtaNotificationSetting; onClose: () => void }) {
  const canManage = usePermission('settings.manage')
  const update = useUpdateEtaNotificationSetting(setting.eventType)
  const [enabled, setEnabled] = useState(setting.enabled)
  const [subject, setSubject] = useState(setting.subjectTemplate)
  const [body, setBody] = useState(setting.bodyTemplate)

  useEffect(() => {
    setEnabled(setting.enabled)
    setSubject(setting.subjectTemplate)
    setBody(setting.bodyTemplate)
  }, [setting])

  const save = async () => {
    try {
      await update.mutateAsync({ enabled, subjectTemplate: subject, bodyTemplate: body })
      toast.success('Notification template saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const meta = eventMeta[setting.eventType]

  return (
    <FormDrawer
      open
      title={meta.label}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={update.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {update.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">{meta.description}</p>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={enabled} disabled={!canManage} onChange={(event) => setEnabled(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Enabled
        </label>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="eta-subject">Subject</label>
          <input
            id="eta-subject"
            value={subject}
            disabled={!canManage}
            onChange={(event) => setSubject(event.target.value)}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="eta-body">Body</label>
          <textarea
            id="eta-body"
            value={body}
            disabled={!canManage}
            onChange={(event) => setBody(event.target.value)}
            rows={8}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
        </div>

        <div className="rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
          <p className="font-medium text-slate-700">Available placeholders</p>
          <p className="mt-1 font-mono">{placeholders.map((p) => `{{${p}}}`).join(', ')}</p>
        </div>
      </div>
    </FormDrawer>
  )
}

export function NotificationTemplatesPage() {
  const { data, isLoading, isError } = useEtaNotificationSettings()
  const canManage = usePermission('settings.manage')
  const [selected, setSelected] = useState<EtaNotificationEventType | null>(null)

  if (isLoading) {
    return <Spinner label="Loading notification templates" />
  }

  if (isError) {
    return <EmptyState icon={Bell} title="Notification templates unavailable" description="ETA notification templates could not be loaded." />
  }

  const rows = data ?? []
  const selectedSetting = rows.find((row) => row.eventType === selected) ?? null

  return (
    <div className="space-y-4">
      <PageHeader
        title="Notification Templates"
        subtitle="Automatic client emails for work order milestones — assigned, en route, arrived, and completed."
      />

      {rows.length === 0 ? (
        <EmptyState icon={Bell} title="No notification events found" description="No ETA notification events were returned by the backend." />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {rows.map((row) => {
            const meta = eventMeta[row.eventType]
            const Icon = meta?.icon ?? Bell
            return (
              <button
                key={row.eventType}
                type="button"
                onClick={() => setSelected(row.eventType)}
                className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-left shadow-sm transition hover:border-slate-300"
              >
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Icon className="h-4 w-4 text-slate-400" />
                    <span className="font-medium text-slate-900">{meta?.label ?? row.eventType}</span>
                  </div>
                  <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${row.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
                    {row.enabled ? 'Enabled' : 'Disabled'}
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">{meta?.description}</p>
                <p className="mt-2 text-sm text-slate-600">{row.subjectTemplate}</p>
              </button>
            )
          })}
        </div>
      )}

      {!canManage ? (
        <p className="text-xs text-slate-500">You have read-only access to notification templates.</p>
      ) : null}

      {selectedSetting ? <TemplateEditor setting={selectedSetting} onClose={() => setSelected(null)} /> : null}
    </div>
  )
}
