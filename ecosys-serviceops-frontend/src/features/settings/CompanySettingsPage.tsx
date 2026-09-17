import { useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Copy } from 'lucide-react'
import { SettingsForm } from './components/SettingsForm'
import { TIME_ZONE_OPTIONS } from '../../shared/utils/timeZoneOptions'
import { api } from '../../shared/api/apiClient'

function WorkspaceCodeCard() {
  // Same query key the SettingsForm below uses for /api/settings/company — react-query dedupes
  // the fetch, so this doesn't add a second network round trip.
  const { data } = useQuery({
    queryKey: ['settings', 'company'],
    queryFn: () => api.get<{ workspaceCode: string }>('/api/settings/company'),
    staleTime: 30_000,
  })

  if (!data?.workspaceCode) return null

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(data.workspaceCode)
      toast.success('Workspace code copied')
    } catch {
      toast.error('Could not copy — select and copy the code manually')
    }
  }

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div>
        <div className="text-sm font-medium text-slate-700">Workspace Code</div>
        <div className="mt-1 font-mono text-sm text-slate-900">{data.workspaceCode}</div>
        <p className="mt-1 text-xs text-slate-500">Staff signing in with a username instead of an email enter your company name alongside it — this is the exact value that's matched against, useful if two companies share a similar name.</p>
      </div>
      <button type="button" onClick={copy} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-50">
        <Copy className="h-3.5 w-3.5" /> Copy
      </button>
    </div>
  )
}

export function CompanySettingsPage() {
  return (
    <div className="space-y-4">
      <WorkspaceCodeCard />
      <SettingsForm
        title="Company Profile"
        subtitle="Legal details, addresses, timezone, and currency."
        queryKey="company"
        fetchUrl="/api/settings/company"
        saveUrl="/api/settings/company"
        fields={[
          { key: 'companyName', label: 'Company Name', type: 'text' },
          { key: 'legalName', label: 'Legal Name', type: 'text' },
          { key: 'tradingName', label: 'Trading Name', type: 'text' },
          { key: 'email', label: 'Email', type: 'email' },
          { key: 'phone', label: 'Phone', type: 'text' },
          { key: 'website', label: 'Website', type: 'text' },
          { key: 'vatNumber', label: 'VAT Number', type: 'text' },
          { key: 'registrationNumber', label: 'Registration Number', type: 'text' },
          { key: 'country', label: 'Country', type: 'text' },
          {
            key: 'timeZoneId',
            label: 'Timezone',
            type: 'select',
            // A blank override means "inherit the platform default", which is what the API stores as null.
            options: [{ value: '', label: 'Use platform default' }, ...TIME_ZONE_OPTIONS],
            helperText: 'All dates and times in this workspace — including password reset and invitation link expiry — are shown in this zone.',
          },
          { key: 'industry', label: 'Industry', type: 'text' },
          { key: 'physicalAddress', label: 'Physical Address', type: 'textarea' },
          { key: 'postalAddress', label: 'Postal Address', type: 'textarea' },
        ]}
      />
    </div>
  )
}
