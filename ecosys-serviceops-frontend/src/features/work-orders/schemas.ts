export const workTypeOptions = [
  { value: 'CorrectiveMaintenance', label: 'Corrective Maintenance' },
  { value: 'PreventiveMaintenance', label: 'Preventive Maintenance' },
  { value: 'NewInstallation', label: 'New Installation' },
  { value: 'InspectionAssessment', label: 'Inspection / Assessment' },
] as const

export const priorityOptions = [
  { value: 'Low', label: 'Low' },
  { value: 'Medium', label: 'Medium' },
  { value: 'High', label: 'High' },
  { value: 'Critical', label: 'Critical' },
] as const
