export interface EmailVariableDefinition {
  token: string
  label: string
  example: string
}

export const WO_ASSIGNED_VARIABLES: EmailVariableDefinition[] = [
  { token: 'wo_number', label: 'Work Order Number', example: 'WO-01000' },
  { token: 'client_name', label: 'Client Name', example: 'Acme Corp' },
  { token: 'technician_name', label: 'Technician Name', example: 'James Mwangi' },
  { token: 'due_date', label: 'Due Date', example: '15 July 2025' },
  { token: 'site_name', label: 'Site Name', example: 'Westlands Branch' },
  { token: 'company_name', label: 'Company Name', example: 'TechService Ltd' },
]
