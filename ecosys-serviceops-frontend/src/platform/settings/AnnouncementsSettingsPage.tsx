import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'

// TODO(backend): No Announcements endpoint exists yet — typed stub returning empty data
// until a real Platform Announcements API is available.
interface Announcement {
  id: string
  title: string
  body: string
  audience: 'AllTenants' | 'TrialTenants' | 'ActiveTenants'
  publishedAt: string | null
  status: 'Draft' | 'Published' | 'Archived'
}

function useAnnouncements() {
  return useQuery({ queryKey: ['platform', 'settings', 'announcements'], queryFn: async (): Promise<Announcement[]> => [], staleTime: Infinity })
}

export function AnnouncementsSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const { data, isLoading } = useAnnouncements()
  const [showNew, setShowNew] = useState(false)
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')

  const columns: DataTableColumn<Announcement>[] = [
    { key: 'title', header: 'Title', cell: (row) => <span className="font-medium text-slate-900">{row.title}</span> },
    { key: 'audience', header: 'Audience', cell: (row) => row.audience },
    { key: 'publishedAt', header: 'Published', cell: (row) => (row.publishedAt ? formatDate(row.publishedAt) : '—') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Announcements"
        subtitle="Platform-wide banners and release notes shown to tenant workspaces."
        primaryAction={canManage ? <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"><Plus className="h-4 w-4" />New Announcement</button> : undefined}
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No Platform Announcements endpoint exists yet — this is the full intended UI wired against typed stub data until a backend endpoint is connected.
      </div>
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle="No announcements yet" emptyDescription="Platform-wide banners and release notes will appear here once the backend endpoint is connected." />

      <FormDrawer open={showNew} title="New Announcement" onClose={() => setShowNew(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Title</label>
            <input value={title} onChange={(event) => setTitle(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Body</label>
            <textarea value={body} onChange={(event) => setBody(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!title.trim() || !body.trim()}
            onClick={() => toast.error('Announcements cannot be published yet — no backend endpoint exists.')}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            Publish Announcement
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
