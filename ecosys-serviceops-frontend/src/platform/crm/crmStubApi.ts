import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

// TODO(backend): Only PlatformLeadsController exists for CRM today (leads/pipeline, real —
// see ../api.ts useLeads/useLead). No Contacts, Activities, or Tasks resource exists yet
// (confirmed against FRONTEND_BUILD_PLAN.md §12.1 backend-gap table). Typed stub hooks below
// return empty data until dedicated backend endpoints exist.

export interface CrmContact {
  id: string
  fullName: string
  companyName: string
  email: string
  phone: string | null
  role: string | null
  leadId: string | null
  createdAt: string
}

export function useCrmContacts() {
  return useQuery({ queryKey: ['platform', 'crm', 'contacts'], queryFn: async (): Promise<CrmContact[]> => [], staleTime: Infinity })
}

export interface CreateCrmContactPayload {
  fullName: string
  companyName: string
  email: string
  phone?: string | undefined
  role?: string | undefined
}

export function useCreateCrmContact() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: CreateCrmContactPayload): Promise<CrmContact> => {
      throw new Error('CRM contacts cannot be created yet — no backend Contacts endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'contacts'] }),
  })
}

export type CrmActivityType = 'Call' | 'Email' | 'Meeting' | 'Note'

export interface CrmActivity {
  id: string
  contactName: string
  type: CrmActivityType
  summary: string
  occurredAt: string
  loggedByName: string
}

export function useCrmActivities() {
  return useQuery({ queryKey: ['platform', 'crm', 'activities'], queryFn: async (): Promise<CrmActivity[]> => [], staleTime: Infinity })
}

export interface LogCrmActivityPayload {
  contactId: string
  type: CrmActivityType
  summary: string
}

export function useLogCrmActivity() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: LogCrmActivityPayload): Promise<CrmActivity> => {
      throw new Error('CRM activities cannot be logged yet — no backend Activities endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'activities'] }),
  })
}

export type CrmTaskStatus = 'Open' | 'InProgress' | 'Done'
export type CrmTaskPriority = 'Low' | 'Medium' | 'High'

export interface CrmTask {
  id: string
  title: string
  relatedContactName: string | null
  assignedToName: string
  dueDate: string | null
  priority: CrmTaskPriority
  status: CrmTaskStatus
}

export function useCrmTasks(status?: CrmTaskStatus | 'All') {
  return useQuery({ queryKey: ['platform', 'crm', 'tasks', status ?? 'All'], queryFn: async (): Promise<CrmTask[]> => [], staleTime: Infinity })
}

export interface CreateCrmTaskPayload {
  title: string
  dueDate?: string | undefined
  priority: CrmTaskPriority
}

export function useCreateCrmTask() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: CreateCrmTaskPayload): Promise<CrmTask> => {
      throw new Error('CRM tasks cannot be created yet — no backend Tasks endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'tasks'] }),
  })
}

export function useCompleteCrmTask(_id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (): Promise<void> => {
      throw new Error('CRM tasks cannot be updated yet — no backend Tasks endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'tasks'] }),
  })
}
