import { StatusBadge } from '../../components/ui/Workspace'

export const contractTypeOptions = [
  'ComprehensiveMaintenance',
  'PreventiveMaintenance',
  'CorrectiveCallout',
  'WarrantySupport',
  'Inspection',
  'InstallationSupport',
  'SlaSupport',
  'AdhocPayAsYouGo',
] as const

export const contractStatusOptions = [
  'Draft',
  'PendingApproval',
  'Active',
  'ExpiringSoon',
  'RenewalInProgress',
  'Renewed',
  'Expired',
  'Suspended',
  'Terminated',
  'Cancelled',
] as const

export const contractBillingOptions = [
  'Included',
  'PreferentialRates',
  'TimeAndMaterials',
  'FixedPrice',
  'PerVisit',
  'Warranty',
  'ChargeableCallout',
  'Retainer',
] as const

export const contractRenewalBehaviorOptions = [
  'AutoRenew',
  'ManualRenewalRequired',
  'AutoExpire',
] as const

export const contractGracePeriodPolicyOptions = [
  'None',
  'PreferentialRates',
  'Sla',
  'Both',
] as const

export const contractRenewalRiskOptions = ['Low', 'Medium', 'High', 'Critical', 'Expired'] as const
export const pmPeriodOptions = ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual'] as const
export const workOrderTypeOptions = ['PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment', 'NewInstallation'] as const

export function formatContractLabel(value?: string | null) {
  if (!value) return '-'
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\bSla\b/g, 'SLA')
    .replace(/\bPm\b/g, 'PM')
}

export function renderContractStatusBadge(status?: string | null) {
  const normalized = (status || '').toLowerCase()
  const tone =
    normalized === 'active' || normalized === 'renewed'
      ? 'success'
      : normalized === 'expiringsoon' || normalized === 'pendingapproval' || normalized === 'renewalinprogress'
        ? 'warning'
        : normalized === 'expired' || normalized === 'terminated' || normalized === 'cancelled'
          ? 'danger'
          : 'default'
  return <StatusBadge tone={tone}>{formatContractLabel(status)}</StatusBadge>
}

export function renderRenewalRiskBadge(risk?: string | null) {
  const normalized = (risk || '').toLowerCase()
  const tone =
    normalized === 'low'
      ? 'success'
      : normalized === 'medium'
        ? 'warning'
        : normalized === 'high' || normalized === 'critical' || normalized === 'expired'
          ? 'danger'
          : 'default'
  return <StatusBadge tone={tone}>{formatContractLabel(risk)}</StatusBadge>
}

export function renderBillingModelBadge(model?: string | null) {
  const normalized = (model || '').toLowerCase()
  const tone =
    normalized === 'included' || normalized === 'warranty'
      ? 'success'
      : normalized === 'preferentialrates' || normalized === 'retainer'
        ? 'info'
        : normalized === 'chargeablecallout' || normalized === 'timeandmaterials'
          ? 'warning'
          : 'default'
  return <StatusBadge tone={tone}>{formatContractLabel(model)}</StatusBadge>
}

export function renderCoverageStatusBadge(status?: string | null) {
  const normalized = (status || '').toLowerCase()
  const tone =
    normalized === 'covered'
      ? 'success'
      : normalized === 'partiallycovered'
        ? 'warning'
        : normalized === 'notcovered'
          ? 'danger'
          : 'default'
  return <StatusBadge tone={tone}>{formatContractLabel(status)}</StatusBadge>
}
