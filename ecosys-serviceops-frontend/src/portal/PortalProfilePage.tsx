import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { formatDateTime } from '../shared/utils/dates'
import { usePortalProfile } from './api'

export function PortalProfilePage() {
  const { data, isLoading, isError } = usePortalProfile()

  if (isLoading) {
    return <Spinner label="Loading profile" />
  }

  if (isError || !data) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Profile could not be loaded.</div>
  }

  const rows: Array<[string, string]> = [
    ['Full Name', data.fullName],
    ['Email', data.email],
    ['Phone', data.phone ?? '—'],
    ['Account', data.clientName],
    ['Last Sign-In', data.lastLoginAt ? formatDateTime(data.lastLoginAt) : '—'],
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Profile" subtitle="Your client portal contact details." />
      <div className="max-w-xl rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <dl className="space-y-4">
          {rows.map(([label, value]) => (
            <div key={label} className="flex items-center justify-between border-b border-slate-100 pb-3 last:border-0 last:pb-0">
              <dt className="text-sm text-slate-500">{label}</dt>
              <dd className="text-sm font-medium text-slate-900">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div className="max-w-xl rounded-2xl border border-dashed border-slate-300 bg-white p-4 text-sm text-slate-600">
        To update your contact details, please reach out to your account manager — self-service profile editing is not yet available.
      </div>
    </div>
  )
}
