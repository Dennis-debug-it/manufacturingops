import { useMemo } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Route as RouteIcon } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { formatDate } from '../../shared/utils/dates'
import { useTeamManagementOverview, useTechnician } from './api'

export function TechnicianDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: technician, isLoading, isError } = useTechnician(id)
  const { data: overview } = useTeamManagementOverview()

  const profile = useMemo(() => overview?.profiles.find((item) => item.technicianId === id), [overview, id])

  if (isLoading) {
    return <Spinner label="Loading technician" />
  }

  if (isError || !technician) {
    return <EmptyState title="Technician unavailable" description="This technician could not be loaded." />
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={technician.fullName}
        subtitle={technician.skillCategory ?? 'Technician'}
        breadcrumbs={[{ label: 'Technicians', to: '/team/technicians' }, { label: technician.fullName }]}
        secondaryActions={
          <>
            <Link
              to={`/dispatch/route/${technician.id}`}
              className="inline-flex items-center gap-2 rounded-control border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm transition hover:bg-slate-50"
            >
              <RouteIcon className="h-4 w-4" />
              Daily Route
            </Link>
            <StatusBadge status={technician.isActive ? 'Active' : 'Inactive'} />
          </>
        }
      />

      <SummarySection title="Profile" description="Contact and assignment details.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            ['Email', technician.email],
            ['Phone', technician.phone ?? '—'],
            ['Branch', technician.branchName ?? '—'],
            ['Skill Category', technician.skillCategory ?? '—'],
            ['Assignment Group', technician.assignmentGroup ?? '—'],
            ['Tracking Active', technician.isTrackingActive ? 'Yes' : 'No'],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 text-sm text-slate-900">{value}</div>
            </div>
          ))}
        </div>
      </SummarySection>

      {profile ? (
        <>
          <SummarySection title="Availability & Readiness" description="Current workforce status.">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Availability</div>
                <div className="mt-2"><StatusBadge status={profile.availabilityStatus} /></div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Readiness</div>
                <div className="mt-2"><StatusBadge status={profile.readinessStatus} /></div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Active WOs</div>
                <div className="mt-2 text-sm text-slate-900">{profile.activeWorkOrderCount}</div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">On Site</div>
                <div className="mt-2 text-sm text-slate-900">{profile.onSiteCount}</div>
              </div>
            </div>
            {profile.readinessReasons.length > 0 ? (
              <p className="mt-3 text-sm text-amber-700">{profile.readinessReasons.join(', ')}</p>
            ) : null}
          </SummarySection>

          <SummarySection title="Certifications" description="Certification status for this technician.">
            {profile.certifications.length === 0 ? (
              <EmptyState title="No certifications" description="No certifications have been recorded for this technician." />
            ) : (
              <ul className="space-y-2 text-sm text-slate-700">
                {profile.certifications.map((cert) => (
                  <li key={cert.certificationId} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2">
                    <span>{cert.certificationName}{cert.expiresOn ? ` — expires ${formatDate(cert.expiresOn)}` : ''}</span>
                    <StatusBadge status={cert.status} />
                  </li>
                ))}
              </ul>
            )}
          </SummarySection>
        </>
      ) : null}
    </div>
  )
}
