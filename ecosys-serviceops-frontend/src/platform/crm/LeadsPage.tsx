import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ArrowRightCircle, Mail, Search, UserCheck } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { useLead, useLeads, useUpdateLeadNotes, useUpdateLeadStatus } from '../api'
import { Field, SectionTitle } from '../common'
import type { PlatformLeadDetail, PlatformLeadListItem } from '../types'
import { CrmSubNav } from './CrmSubNav'

const statusOptions = ['All', 'New', 'Contacted', 'Qualified', 'Demo Scheduled', 'Converted to Workspace', 'Not a Fit'] as const

export function LeadsPage() {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedLeadId = searchParams.get('lead')
  const { data, isLoading, isError } = useLeads()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<(typeof statusOptions)[number]>('All')
  const debouncedSearch = useDebounce(search, 250)

  const [showConvert, setShowConvert] = useState(false)
  const [notesDraft, setNotesDraft] = useState('')
  const [statusDraft, setStatusDraft] = useState<string>('New')

  const selectedLead = useLead(selectedLeadId ?? undefined)
  const updateStatus = useUpdateLeadStatus(selectedLeadId ?? '')
  const updateNotes = useUpdateLeadNotes(selectedLeadId ?? '')

  useEffect(() => {
    if (selectedLead.data) {
      setNotesDraft(selectedLead.data.notes ?? '')
      setStatusDraft(selectedLead.data.status)
    }
  }, [selectedLead.data])

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((lead) => {
      const matchesSearch =
        !query
        || lead.companyName.toLowerCase().includes(query)
        || lead.contactPersonName.toLowerCase().includes(query)
        || lead.email.toLowerCase().includes(query)
        || lead.phone.toLowerCase().includes(query)
      const matchesStatus = statusFilter === 'All' || lead.status === statusFilter
      return matchesSearch && matchesStatus
    })
  }, [data, debouncedSearch, statusFilter])

  const columns: DataTableColumn<PlatformLeadListItem>[] = [
    { key: 'companyName', header: 'Company', cell: (row) => <button type="button" className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" onClick={() => setSearchParams({ lead: row.id }, { replace: true })}>{row.companyName}</button> },
    { key: 'contact', header: 'Contact', cell: (row) => <div>{row.contactPersonName}<div className="text-xs text-slate-500">{row.email}</div></div> },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone },
    { key: 'status', header: 'Status', cell: (row) => <StatusPill status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setSearchParams({ lead: row.id }, { replace: true })}>
          Open
        </button>
      ),
    },
  ]

  const closeDetail = () => {
    const next = new URLSearchParams(searchParams)
    next.delete('lead')
    setSearchParams(next, { replace: true })
  }

  const saveLeadStatus = async () => {
    if (!selectedLeadId) return
    try {
      await updateStatus.mutateAsync(statusDraft)
      toast.success('Lead status updated')
    } catch {
      // interceptor handles errors
    }
  }

  const saveLeadNotes = async () => {
    if (!selectedLeadId) return
    try {
      await updateNotes.mutateAsync(notesDraft)
      toast.success('Lead notes saved')
    } catch {
      // interceptor handles errors
    }
  }

  return (
    <div className="space-y-5" data-testid="leads-page">
      <PageHeader
        title="Leads"
        subtitle="Workspace requests, status tracking, and manual conversion flow."
      />

      <CrmSubNav />

      <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm lg:grid-cols-[minmax(0,2fr)_minmax(220px,280px)]">
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search company, contact, email, or phone" className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400" />
        </label>
        <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
          {statusOptions.map((status) => <option key={status} value={status}>{status === 'All' ? 'All statuses' : status}</option>)}
        </select>
      </div>

      {isLoading ? <Spinner label="Loading leads" /> : null}
      {!isLoading && isError ? <EmptyState title="Leads unavailable" description="Lead records could not be loaded." /> : null}
      {!isLoading && !isError ? (
        rows.length === 0 ? (
          <EmptyState title="No leads found" description="New public workspace requests will appear here once submitted." />
        ) : (
          <DataTable columns={columns} data={rows} emptyTitle="No leads found" emptyDescription="New public workspace requests will appear here once submitted." />
        )
      ) : null}

      <FormDrawer open={Boolean(selectedLeadId)} title={selectedLead.data?.companyName ?? 'Lead details'} onClose={closeDetail}>
        {selectedLead.isLoading ? <Spinner label="Loading lead" /> : null}
        {selectedLead.isError || !selectedLead.data ? <EmptyState title="Lead unavailable" description="This lead could not be loaded." /> : null}
        {selectedLead.data ? (
          <LeadDrawerContent
            lead={selectedLead.data}
            statusDraft={statusDraft}
            setStatusDraft={setStatusDraft}
            notesDraft={notesDraft}
            setNotesDraft={setNotesDraft}
            onSaveStatus={() => void saveLeadStatus()}
            onSaveNotes={() => void saveLeadNotes()}
            onConvert={() => setShowConvert(true)}
          />
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={showConvert}
        title="Create tenant from lead"
        message="This will open the tenant creation flow with the lead details prefilled. The backend currently supports workspace creation, but not an automatic conversion endpoint."
        confirmLabel="Continue"
        onCancel={() => setShowConvert(false)}
        onConfirm={() => {
          if (!selectedLeadId) return
          navigate(`/platform/tenants?leadId=${selectedLeadId}`)
          setShowConvert(false)
          closeDetail()
        }}
      />
    </div>
  )
}

function LeadDrawerContent({
  lead,
  statusDraft,
  setStatusDraft,
  notesDraft,
  setNotesDraft,
  onSaveStatus,
  onSaveNotes,
  onConvert,
}: {
  lead: PlatformLeadDetail
  statusDraft: string
  setStatusDraft: (value: string) => void
  notesDraft: string
  setNotesDraft: (value: string) => void
  onSaveStatus: () => void
  onSaveNotes: () => void
  onConvert: () => void
}) {
  return (
    <div className="space-y-5">
      <section className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <InfoCard label="Company" value={lead.companyName} />
        <InfoCard label="Contact" value={lead.contactPersonName} />
        <InfoCard label="Email" value={lead.email} />
        <InfoCard label="Phone" value={lead.phone} />
        <InfoCard label="Country" value={lead.country ?? 'Not set'} />
        <InfoCard label="Industry" value={lead.industry ?? 'Not set'} />
        <InfoCard label="Company size" value={lead.companySize ?? 'Not set'} />
        <InfoCard label="Preferred contact" value={lead.preferredContactMethod ?? 'Not set'} />
        <InfoCard label="Created" value={formatDateTime(lead.createdAt)} />
        <InfoCard label="Contacted" value={lead.contactedAt ? formatDateTime(lead.contactedAt) : 'Not yet'} />
        <InfoCard label="Status" value={lead.status} />
        <InfoCard label="Converted tenant" value={lead.convertedTenantId ?? 'Not converted'} />
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Request details" description="What the prospective customer submitted." />
        <p className="mt-4 whitespace-pre-wrap text-sm leading-6 text-slate-700">{lead.message ?? 'No message provided.'}</p>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Lead actions" description="Advance the lead through the platform owner's follow-up flow." />
        <div className="mt-4 flex flex-wrap gap-2">
          <button type="button" className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => setStatusDraft('Contacted')}>
            <Mail className="h-4 w-4" />
            Mark contacted
          </button>
          <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => setStatusDraft('Qualified')}>
            <UserCheck className="h-4 w-4" />
            Mark qualified
          </button>
          <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={onConvert}>
            <ArrowRightCircle className="h-4 w-4" />
            Create tenant from lead
          </button>
        </div>

        <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
          <Field label="Status">
            <select value={statusDraft} onChange={(event) => setStatusDraft(event.target.value)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900">
              {['New', 'Contacted', 'Qualified', 'Demo Scheduled', 'Converted to Workspace', 'Not a Fit'].map((status) => <option key={status} value={status}>{status}</option>)}
            </select>
          </Field>
          <button type="button" className="self-end rounded-lg bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800" onClick={onSaveStatus}>
            Save status
          </button>
        </div>

        <Field label="Internal notes">
          <textarea value={notesDraft} onChange={(event) => setNotesDraft(event.target.value)} className="min-h-[140px] w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-slate-900" />
        </Field>
        <div className="mt-4 flex justify-end">
          <button type="button" className="rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={onSaveNotes}>
            Save notes
          </button>
        </div>
      </section>
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  return <span className="inline-flex items-center rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700">{status}</span>
}

function InfoCard({ label, value, mono = false }: { label: string; value: React.ReactNode; mono?: boolean }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className={`mt-2 text-sm text-slate-900 ${mono ? 'font-mono' : ''}`}>{value}</div>
    </div>
  )
}
