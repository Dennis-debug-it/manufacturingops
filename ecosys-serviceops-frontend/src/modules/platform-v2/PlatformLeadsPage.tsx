import { ArrowRightCircle, Eye, RefreshCw } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { InfoAlert } from '../../components/ui/InfoAlert'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import {
  platformLeadService,
  platformLeadStatuses,
  type PlatformLeadDetail,
  type PlatformLeadStatus,
  type PlatformLeadSummary,
} from '../../services/platformLeadService'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { Field, SectionTitle } from './PlatformCommon'

function statusBadge(status: string) {
  const tone =
    status === 'New'
      ? 'bg-slate-500/12 text-slate-200'
      : status === 'Contacted'
        ? 'bg-sky-500/12 text-sky-200'
        : status === 'Qualified'
          ? 'bg-lime-500/12 text-lime-200'
          : status === 'Demo Scheduled'
            ? 'bg-amber-500/12 text-amber-200'
            : status === 'Converted to Workspace'
              ? 'bg-emerald-500/12 text-emerald-200'
              : 'bg-rose-500/12 text-rose-200'

  return <span className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${tone}`}>{status}</span>
}

export function PlatformLeadsPage() {
  const navigate = useNavigate()
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(
    async () => platformLeadService.listLeads(),
    [] as PlatformLeadSummary[],
    [],
  )

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'All' | PlatformLeadStatus>('All')
  const [selectedLead, setSelectedLead] = useState<PlatformLeadDetail | null>(null)
  const [loadingLeadId, setLoadingLeadId] = useState<string | null>(null)
  const [statusDraft, setStatusDraft] = useState<PlatformLeadStatus>('New')
  const [notesDraft, setNotesDraft] = useState('')
  const [savingStatus, setSavingStatus] = useState(false)
  const [savingNotes, setSavingNotes] = useState(false)

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase()
    return data.filter((item) => {
      const matchesQuery =
        !query ||
        item.companyName.toLowerCase().includes(query) ||
        item.contactPersonName.toLowerCase().includes(query) ||
        item.email.toLowerCase().includes(query) ||
        item.phone.toLowerCase().includes(query)
      const matchesStatus = statusFilter === 'All' || item.status === statusFilter
      return matchesQuery && matchesStatus
    })
  }, [data, search, statusFilter])

  async function openLead(lead: PlatformLeadSummary) {
    setLoadingLeadId(lead.id)
    try {
      const detail = await platformLeadService.getLead(lead.id)
      setSelectedLead(detail)
      setStatusDraft((detail.status as PlatformLeadStatus) || 'New')
      setNotesDraft(detail.notes || '')
    } catch (loadError) {
      pushToast({ title: 'Unable to open lead', description: loadError instanceof Error ? loadError.message : 'Unable to load lead details.', tone: 'danger' })
    } finally {
      setLoadingLeadId(null)
    }
  }

  async function saveStatus(status = statusDraft) {
    if (!selectedLead) return

    setSavingStatus(true)
    try {
      const updated = await platformLeadService.updateLeadStatus(selectedLead.id, status)
      setSelectedLead(updated)
      setStatusDraft(updated.status as PlatformLeadStatus)
      await reload()
      pushToast({ title: 'Lead updated', description: `${updated.companyName} is now marked as ${updated.status}.`, tone: 'success' })
    } catch (statusError) {
      pushToast({ title: 'Status update failed', description: statusError instanceof Error ? statusError.message : 'Unable to update lead status.', tone: 'danger' })
    } finally {
      setSavingStatus(false)
    }
  }

  async function saveNotes() {
    if (!selectedLead) return

    setSavingNotes(true)
    try {
      const updated = await platformLeadService.updateLeadNotes(selectedLead.id, notesDraft)
      setSelectedLead(updated)
      setNotesDraft(updated.notes || '')
      await reload()
      pushToast({ title: 'Notes saved', description: `Internal notes were updated for ${updated.companyName}.`, tone: 'success' })
    } catch (notesError) {
      pushToast({ title: 'Notes save failed', description: notesError instanceof Error ? notesError.message : 'Unable to save lead notes.', tone: 'danger' })
    } finally {
      setSavingNotes(false)
    }
  }

  function startConversion() {
    if (!selectedLead) return
    navigate(`/platform/tenants?leadId=${selectedLead.id}`)
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Platform Command Centre"
        title="Leads & Enquiries"
        description="Review inbound Ecosys enquiries, track follow-up progress, and prepare qualified opportunities for manual workspace creation."
      />

      {loading ? <LoadingState label="Loading leads and enquiries" /> : null}
      {!loading && error ? (
        <section className="surface-card space-y-3">
          <InfoAlert title="Unable to load leads" description={error} tone="danger" />
          <button type="button" className="button-secondary" onClick={() => void reload()}>
            <RefreshCw className="h-4 w-4" />Retry
          </button>
        </section>
      ) : null}

      {!loading && !error ? (
        <section className="surface-card space-y-4">
          <SectionTitle title="Submitted Enquiries" description="Track each request from first contact through qualification and conversion readiness." />

          <div className="grid gap-3 md:grid-cols-[minmax(0,2fr)_minmax(220px,280px)]">
            <Field label="Search">
              <input value={search} onChange={(event) => setSearch(event.target.value)} className="field-input" placeholder="Company, contact, email, phone" />
            </Field>
            <Field label="Status">
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="field-input">
                <option value="All">All statuses</option>
                {platformLeadStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
              </select>
            </Field>
          </div>

          {filtered.length === 0 ? (
            <EmptyState title="No leads found" description="New public enquiries will appear here once prospects submit the Get Started form." />
          ) : (
            <DataTable
              rows={filtered}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[1180px] w-full"
              columns={[
                { key: 'companyName', header: 'Company Name', cell: (row) => <span className="font-semibold text-app">{row.companyName}</span> },
                { key: 'contactPersonName', header: 'Contact Person', cell: (row) => row.contactPersonName },
                { key: 'email', header: 'Email', cell: (row) => row.email },
                { key: 'phone', header: 'Phone', cell: (row) => row.phone },
                { key: 'status', header: 'Status', cell: (row) => statusBadge(row.status) },
                { key: 'createdAt', header: 'Created Date', cell: (row) => formatDateOnly(row.createdAt) },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[160px]',
                  cell: (row) => (
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => void openLead(row)} disabled={loadingLeadId === row.id}>
                      <Eye className="h-4 w-4" />
                      {loadingLeadId === row.id ? 'Loading...' : 'View'}
                    </button>
                  ),
                },
              ]}
            />
          )}
        </section>
      ) : null}

      <Drawer
        open={Boolean(selectedLead)}
        title={selectedLead?.companyName || 'Lead'}
        description="Review the submitted details, keep internal notes, and advance the enquiry through the right follow-up stage."
        onClose={() => setSelectedLead(null)}
      >
        {selectedLead ? (
          <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-2">
              <LeadDetail label="Company Name" value={selectedLead.companyName} />
              <LeadDetail label="Contact Person" value={selectedLead.contactPersonName} />
              <LeadDetail label="Email Address" value={selectedLead.email} />
              <LeadDetail label="Phone Number" value={selectedLead.phone} />
              <LeadDetail label="Country" value={selectedLead.country || 'Not provided'} />
              <LeadDetail label="Industry" value={selectedLead.industry || 'Not provided'} />
              <LeadDetail label="Company Size / Users" value={selectedLead.companySize || 'Not provided'} />
              <LeadDetail label="Preferred Contact Method" value={selectedLead.preferredContactMethod || 'Not provided'} />
              <LeadDetail label="Created At" value={formatDateTime(selectedLead.createdAt)} />
              <LeadDetail label="Contacted At" value={selectedLead.contactedAt ? formatDateTime(selectedLead.contactedAt) : 'Not yet'} />
              <LeadDetail label="Status" value={selectedLead.status} />
            </div>

            <div className="surface-card">
              <p className="text-sm font-semibold text-app">Business Need</p>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted">{selectedLead.message || 'No message provided.'}</p>
            </div>

            <section className="surface-card space-y-4">
              <SectionTitle title="Lead Actions" description="Update follow-up status and keep an internal engagement record." />

              <div className="flex flex-wrap gap-2">
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void saveStatus('Contacted')} disabled={savingStatus}>Mark as Contacted</button>
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void saveStatus('Qualified')} disabled={savingStatus}>Mark as Qualified</button>
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void saveStatus('Demo Scheduled')} disabled={savingStatus}>Mark Demo Scheduled</button>
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void saveStatus('Not a Fit')} disabled={savingStatus}>Mark Not a Fit</button>
              </div>

              <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                <Field label="Status">
                  <select value={statusDraft} onChange={(event) => setStatusDraft(event.target.value as PlatformLeadStatus)} className="field-input">
                    {platformLeadStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
                  </select>
                </Field>
                <button type="button" className="button-primary self-end" onClick={() => void saveStatus()} disabled={savingStatus}>
                  {savingStatus ? 'Saving...' : 'Update Status'}
                </button>
              </div>

              <Field label="Internal Notes">
                <textarea value={notesDraft} onChange={(event) => setNotesDraft(event.target.value)} className="field-input min-h-[160px]" placeholder="Capture call notes, qualification context, next steps, or blockers." />
              </Field>

              <div className="flex flex-wrap items-center justify-between gap-3">
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void saveNotes()} disabled={savingNotes}>
                  {savingNotes ? 'Saving Notes...' : 'Save Notes'}
                </button>
                <button type="button" className="button-primary px-3 py-2" onClick={startConversion}>
                  <ArrowRightCircle className="h-4 w-4" />
                  Convert to Workspace
                </button>
              </div>

              <p className="text-xs text-muted">
                Workspace creation still requires platform owner review and manual submission on the tenant setup screen.
              </p>
            </section>
          </div>
        ) : null}
      </Drawer>
    </div>
  )
}

function LeadDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="panel-subtle rounded-2xl p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-sm text-app">{value}</p>
    </div>
  )
}
