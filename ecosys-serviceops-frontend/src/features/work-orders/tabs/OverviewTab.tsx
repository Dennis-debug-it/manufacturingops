import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { MapPin, Phone, UserCog } from 'lucide-react'
import { Badge } from '../../../shared/components/Badge'
import { SlaBadge } from '../../../shared/components/SlaBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime, isPast } from '../../../shared/utils/dates'
import { formatCurrency } from '../../../shared/utils/format'
import { useWorkOrderSla } from '../api'
import type { WorkOrder } from '../types'

type Row = [label: string, value: ReactNode]

/** Drops rows with nothing to show, so sections stay short. */
const present = (rows: Row[]) => rows.filter(([, value]) => value !== null && value !== undefined && value !== '')

function Section({ title, action, rows, children }: { title: string; action?: ReactNode; rows?: Row[]; children?: ReactNode }) {
  const visibleRows = rows ? present(rows) : []
  if (visibleRows.length === 0 && !children) return null
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
        {action}
      </div>
      {visibleRows.length > 0 ? (
        <dl className="divide-y divide-slate-100">
          {visibleRows.map(([label, value]) => (
            <div key={label} className="flex gap-3 py-1.5 text-sm">
              <dt className="w-32 shrink-0 text-slate-500">{label}</dt>
              <dd className="min-w-0 flex-1 break-words text-slate-900">{value}</dd>
            </div>
          ))}
        </dl>
      ) : null}
      {children}
    </section>
  )
}

const linkClass = 'font-medium text-slate-900 underline decoration-slate-300 underline-offset-4 hover:decoration-slate-500'

function SlaClock({ label, deadline, breached, paused }: { label: string; deadline: string | null; breached: boolean; paused: boolean }) {
  if (!deadline) return null
  // A deadline that has passed without a breach was met; the server flags misses as breaches.
  const met = !breached && isPast(deadline)
  return (
    <div className="flex gap-3 py-1.5 text-sm">
      <span className="w-32 shrink-0 text-slate-500">{label}</span>
      <span className="flex min-w-0 flex-1 flex-wrap items-center justify-between gap-2 text-slate-900">
        {formatDateTime(deadline)}
        {met ? <Badge tone="success">Met</Badge> : <SlaBadge deadline={deadline} breached={breached} paused={paused} />}
      </span>
    </div>
  )
}

export function OverviewTab({ workOrder, onReassign }: { workOrder: WorkOrder; onReassign: () => void }) {
  const canAssign = usePermission('work-orders.assign')
  const { data: sla } = useWorkOrderSla(workOrder.id)

  const mapUrl = workOrder.siteLatitude != null && workOrder.siteLongitude != null
    ? `https://www.google.com/maps/search/?api=1&query=${workOrder.siteLatitude},${workOrder.siteLongitude}`
    : workOrder.siteLocation
      ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(workOrder.siteLocation)}`
      : null

  const assetModel = [workOrder.assetManufacturer, workOrder.assetModel].filter(Boolean).join(' ')
  const warranty = workOrder.assetWarrantyStatus
    ? `${workOrder.assetWarrantyStatus}${workOrder.assetWarrantyExpiryDate ? ` (until ${formatDate(workOrder.assetWarrantyExpiryDate)})` : ''}`
    : workOrder.assetWarrantyExpiryDate ? `Expires ${formatDate(workOrder.assetWarrantyExpiryDate)}` : null
  const hasContractOrBilling = Boolean(workOrder.contractId || workOrder.isBillable || workOrder.estimatedCharge != null || workOrder.finalCharge != null)

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
      <Section
        title="Job"
        rows={[
          ['Description', workOrder.description ? <span className="whitespace-pre-line">{workOrder.description}</span> : null],
          ['Type', workOrder.workType],
          ['Checklist', workOrder.pmTemplateName],
          ['Scheduled', workOrder.scheduledStartAt ? formatDateTime(workOrder.scheduledStartAt) : null],
          ['Created', formatDateTime(workOrder.createdAt)],
          ['Branch', workOrder.branchName],
        ]}
      />

      <Section
        title="Assigned to"
        action={canAssign ? (
          <button type="button" onClick={onReassign} className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
            <UserCog className="h-3.5 w-3.5" />Reassign
          </button>
        ) : undefined}
        rows={[
          ['Technicians', workOrder.isUnassigned ? 'Unassigned' : workOrder.assignmentSummary || 'Unassigned'],
          ['Lead', workOrder.leadTechnicianName],
          ['Group', workOrder.assignmentGroupName],
          ['Status', workOrder.assignmentStatus],
        ]}
      />

      <Section
        title="Site"
        rows={[
          ['Site', workOrder.siteName ? `${workOrder.siteName}${workOrder.siteCode ? ` · ${workOrder.siteCode}` : ''}` : null],
          ['Client', workOrder.clientName ? <Link to={`/clients/${workOrder.clientId}`} className={linkClass}>{workOrder.clientName}</Link> : null],
          ['Contact', workOrder.siteContactPerson ?? workOrder.clientContactPerson],
          ['Phone', (workOrder.siteContactPhone ?? workOrder.clientContactPhone)
            ? (
              <a href={`tel:${(workOrder.siteContactPhone ?? workOrder.clientContactPhone)!.replace(/\s+/g, '')}`} className={`${linkClass} inline-flex items-center gap-1`}>
                <Phone className="h-3.5 w-3.5" />{workOrder.siteContactPhone ?? workOrder.clientContactPhone}
              </a>
            )
            : null],
          ['Hours', workOrder.siteOperatingHours],
          ['Access notes', workOrder.siteAccessNotes ? <span className="whitespace-pre-line">{workOrder.siteAccessNotes}</span> : null],
          ['Location', mapUrl
            ? (
              <a href={mapUrl} target="_blank" rel="noreferrer" className={`${linkClass} inline-flex items-center gap-1`}>
                <MapPin className="h-3.5 w-3.5" />{workOrder.siteLocation || 'Open map'}
              </a>
            )
            : null],
        ]}
      />

      {workOrder.assetId ? (
        <Section
          title="Asset"
          rows={[
            ['Asset', <Link key="asset" to={`/assets/${workOrder.assetId}`} className={linkClass}>{workOrder.assetName ?? workOrder.assetCode ?? 'View asset'}</Link>],
            ['Code', workOrder.assetCode],
            ['Serial', workOrder.assetSerialNumber],
            ['Make / model', assetModel || null],
            ['Warranty', warranty],
            ['Last PM', workOrder.assetLastServiceDate ? formatDate(workOrder.assetLastServiceDate) : null],
            ['Next PM', workOrder.assetNextServiceDate ? formatDate(workOrder.assetNextServiceDate) : null],
          ]}
        />
      ) : null}

      {sla && (sla.responseDeadline || sla.resolutionDeadline) ? (
        <Section title="SLA" rows={[['Policy', `${sla.policyName ?? 'Default'} · ${sla.priority} priority`]]}>
          <SlaClock label="Response" deadline={sla.responseDeadline} breached={sla.responseBreached} paused={sla.isPaused} />
          <SlaClock label="Resolution" deadline={sla.resolutionDeadline} breached={sla.resolutionBreached} paused={sla.isPaused} />
          {sla.isPaused ? <p className="mt-1 text-xs text-amber-700">Clock paused{sla.pauseReason ? `: ${sla.pauseReason}` : ''}.</p> : null}
        </Section>
      ) : null}

      {hasContractOrBilling ? (
        <Section
          title="Contract & billing"
          rows={[
            ['Contract', workOrder.contractId ? <Link to={`/contracts/${workOrder.contractId}`} className={linkClass}>{workOrder.contractNumber ?? 'View contract'}</Link> : 'No contract'],
            ['Coverage', workOrder.coverageStatus],
            ['Billing', workOrder.billingModel ?? (workOrder.isBillable ? 'Billable' : null)],
            ['Rate card', workOrder.rateCardName],
            ['Estimate', workOrder.estimatedCharge != null ? formatCurrency(workOrder.estimatedCharge) : null],
            ['Final charge', workOrder.finalCharge != null ? formatCurrency(workOrder.finalCharge) : null],
          ]}
        />
      ) : null}

      {(workOrder.plannedMaterials ?? []).length > 0 ? (
        <Section title="Planned materials">
          <ul className="divide-y divide-slate-100 text-sm">
            {(workOrder.plannedMaterials ?? []).map((material) => (
              <li key={material.materialId} className="flex justify-between gap-3 py-1.5">
                <span className="text-slate-900">{material.materialName}</span>
                <span className="text-slate-500">× {material.quantity}</span>
              </li>
            ))}
          </ul>
        </Section>
      ) : null}
    </div>
  )
}
