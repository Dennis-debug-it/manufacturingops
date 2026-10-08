import { z } from 'zod'

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

export const newWorkOrderSchema = z.object({
  clientId: z.string().min(1, 'Client is required'),
  siteId: z.string().optional(),
  assetId: z.string().optional(),
  title: z.string().min(1, 'Title is required').max(200, 'Title is too long'),
  workType: z.enum(['CorrectiveMaintenance', 'PreventiveMaintenance', 'NewInstallation', 'InspectionAssessment']),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
  dueDate: z.string().optional(),
  description: z.string().max(2000, 'Description is too long').optional(),
})

export type NewWorkOrderFormValues = z.infer<typeof newWorkOrderSchema>
