import { Bell, Mail, MonitorSmartphone, Save } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { notificationPreferenceService } from '../../services/notificationPreferenceService'
import type { UpdateNotificationPreferenceInput, UserNotificationPreference } from '../../types/api'

// ── Toggle switch ─────────────────────────────────────────────────────────────

function Toggle({
  checked,
  onChange,
  label,
}: {
  checked: boolean
  onChange: (value: boolean) => void
  label: string
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-[var(--app-primary)] focus-visible:ring-offset-2 ${
        checked ? 'bg-[var(--app-primary)]' : 'bg-[var(--app-border)]'
      }`}
    >
      <span
        className={`inline-block h-4 w-4 rounded-full bg-white shadow-sm transition-transform ${
          checked ? 'translate-x-5' : 'translate-x-0'
        }`}
      />
    </button>
  )
}

// ── Category group ────────────────────────────────────────────────────────────

function NotificationGroup({
  category,
  prefs,
  onToggleEmail,
  onToggleInApp,
}: {
  category: string
  prefs: UserNotificationPreference[]
  onToggleEmail: (key: string, value: boolean) => void
  onToggleInApp: (key: string, value: boolean) => void
}) {
  return (
    <section>
      <h2 className="mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-muted">{category}</h2>
      <div className="surface-card divide-y divide-[var(--app-border)] overflow-hidden rounded-2xl">
        {prefs.map((pref) => (
          <div key={pref.notificationKey} className="flex items-center justify-between gap-4 px-5 py-4">
            <div className="flex min-w-0 items-center gap-3">
              <Bell className="h-4 w-4 shrink-0 text-muted" />
              <span className="text-sm font-medium text-app">{pref.label}</span>
            </div>

            <div className="flex shrink-0 items-center gap-6">
              <label className="flex items-center gap-2 text-xs text-muted">
                <Mail className="h-3.5 w-3.5" />
                Email
                <Toggle
                  checked={pref.emailEnabled}
                  onChange={(v) => onToggleEmail(pref.notificationKey, v)}
                  label={`Email for ${pref.label}`}
                />
              </label>
              <label className="flex items-center gap-2 text-xs text-muted">
                <MonitorSmartphone className="h-3.5 w-3.5" />
                In-App
                <Toggle
                  checked={pref.inAppEnabled}
                  onChange={(v) => onToggleInApp(pref.notificationKey, v)}
                  label={`In-app for ${pref.label}`}
                />
              </label>
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function MyNotificationsPage() {
  const { pushToast } = useToast()

  const {
    data: serverPrefs,
    loading,
    error,
    reload,
  } = useAsyncData<UserNotificationPreference[]>(
    (signal) => notificationPreferenceService.getMyNotificationPreferences(signal),
    [],
    [],
  )

  // Local editable copy; null means "no unsaved changes"
  const [localPrefs, setLocalPrefs] = useState<UserNotificationPreference[] | null>(null)
  const [saving, setSaving] = useState(false)

  const prefs = localPrefs ?? serverPrefs
  const isDirty = localPrefs !== null

  useUnsavedChangesGuard(isDirty)

  // Group by category, preserving registry order
  const grouped = useMemo(() => {
    const map = new Map<string, UserNotificationPreference[]>()
    for (const p of prefs) {
      const bucket = map.get(p.category) ?? []
      bucket.push(p)
      map.set(p.category, bucket)
    }
    return map
  }, [prefs])

  const handleToggleEmail = useCallback((key: string, value: boolean) => {
    setLocalPrefs((current) =>
      (current ?? serverPrefs).map((p) =>
        p.notificationKey === key ? { ...p, emailEnabled: value } : p,
      ),
    )
  }, [serverPrefs])

  const handleToggleInApp = useCallback((key: string, value: boolean) => {
    setLocalPrefs((current) =>
      (current ?? serverPrefs).map((p) =>
        p.notificationKey === key ? { ...p, inAppEnabled: value } : p,
      ),
    )
  }, [serverPrefs])

  async function save() {
    setSaving(true)
    try {
      const payload: UpdateNotificationPreferenceInput[] = prefs.map((p) => ({
        notificationKey: p.notificationKey,
        emailEnabled: p.emailEnabled,
        inAppEnabled: p.inAppEnabled,
      }))
      await notificationPreferenceService.updateMyNotificationPreferences(payload)
      setLocalPrefs(null)
      await reload()
      pushToast({
        title: 'Preferences saved',
        description: 'Your notification preferences have been updated.',
        tone: 'success',
      })
    } catch (err) {
      pushToast({
        title: 'Save failed',
        description: err instanceof Error ? err.message : 'Unable to save preferences.',
        tone: 'danger',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-8 px-4 py-8 sm:px-6 lg:px-8">
      <PageHeader
        eyebrow="My Account"
        title="Notification Preferences"
        description="Choose which emails and in-app alerts you receive. These settings apply to your account only."
        actions={
          <button
            type="button"
            className="button-primary"
            disabled={!isDirty || saving}
            onClick={() => void save()}
          >
            <Save className="h-4 w-4" />
            {saving ? 'Saving…' : 'Save Preferences'}
          </button>
        }
      />

      {loading ? (
        <LoadingState label="Loading preferences…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void reload()} />
      ) : (
        <div className="space-y-8">
          {Array.from(grouped.entries()).map(([category, categoryPrefs]) => (
            <NotificationGroup
              key={category}
              category={category}
              prefs={categoryPrefs}
              onToggleEmail={handleToggleEmail}
              onToggleInApp={handleToggleInApp}
            />
          ))}

          {isDirty ? (
            <p className="text-xs text-amber-400">You have unsaved changes.</p>
          ) : null}

          <div className="flex justify-end border-t border-[var(--app-border)] pt-6">
            <button
              type="button"
              className="button-primary"
              disabled={!isDirty || saving}
              onClick={() => void save()}
            >
              <Save className="h-4 w-4" />
              {saving ? 'Saving…' : 'Save Preferences'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
