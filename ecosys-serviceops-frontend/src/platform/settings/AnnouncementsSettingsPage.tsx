import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'

interface Announcement {
  id: string
  title: string
  body: string
  audience: 'AllTenants' | 'TrialTenants' | 'ActiveTenants'
  publishedAt: string | null
  expiresAt: string | null
  status: 'Draft' | 'Published' | 'Archived'
}

interface UpsertAnnouncementPayload {
  title: string
  body: string
  audience?: string
  publish?: boolean
}

function useAnnouncements() {
  return useQuery({
    queryKey: ['platform', 'announcements'],
    queryFn: () => api.get<Announcement[]>('/api/platform/announcements'),
    staleTime: 30_000,
  })
}

function useCreateAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertAnnouncementPayload) => api.post<Announcement>('/api/platform/announcements', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'announcements'] }),
  })
}

function usePublishAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<Announcement>(`/api/platform/announcements/${id}/publish`, {}),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'announcements'] }),
  })
}

export function AnnouncementsSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const { data, isLoading } = useAnnouncements()
  const createAnnouncement = useCreateAnnouncement()
  const publishAnnouncement = usePublishAnnouncement()
  const [showNew, setShowNew] = useState(false)
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [audience, setAudience] = useState('AllTenants')

  const submit = async (publish: boolean) => {
    await createAnnouncement.mutateAsync({ title, body, audience, publish })
    toast.success(publish ? 'Announcement published' : 'Draft saved')
    setShowNew(false)
    setTitle('')
    setBody('')
  }

  const publish = async (id: string) => {
    await publishAnnouncement.mutateAsync(id)
    toast.success('Announcement published')
  }

  const columns: DataTableColumn<Announcement>[] = [
    { key: 'title', header: 'Title', cell: (row) => <span className="font-medium text-slate-900">{row.title}</span> },
    { key: 'audience', header: 'Audience', cell: (row) => row.audience },
    { key: 'publishedAt', header: 'Published', cell: (row) => (row.publishedAt ? formatDate(row.publishedAt) : '—') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) => (canManage && row.status !== 'Published' ? (
        <button type="button" disabled={publishAnnouncement.isPending} onClick={() => void publish(row.id)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">Publish</button>
      ) : null),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Announcements"
        subtitle="Platform-wide banners and release notes shown to tenant workspaces."
        primaryAction={canManage ? <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"><Plus className="h-4 w-4" />New Announcement</button> : undefined}
      />
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle="No announcements yet" emptyDescription="Publish a platform-wide banner or release note." />

      <FormDrawer open={showNew} title="New Announcement" onClose={() => setShowNew(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Title</label>
            <input value={title} onChange={(event) => setTitle(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Audience</label>
            <select value={audience} onChange={(event) => setAudience(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option value="AllTenants">All Tenants</option>
              <option value="TrialTenants">Trial Tenants</option>
              <option value="ActiveTenants">Active Tenants</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Body</label>
            <textarea value={body} onChange={(event) => setBody(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <div className="flex gap-2">
            <button type="button" disabled={!title.trim() || !body.trim() || createAnnouncement.isPending} onClick={() => void submit(false)} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">Save Draft</button>
            <button type="button" disabled={!title.trim() || !body.trim() || createAnnouncement.isPending} onClick={() => void submit(true)} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">Publish</button>
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
