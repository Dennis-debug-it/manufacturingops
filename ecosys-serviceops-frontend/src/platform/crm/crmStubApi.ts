import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

// Platform CRM: contacts, activities, and tasks — wired to the real
// PlatformCrm* controllers (see backend/src/Ecosys.Api/Controllers/PlatformCrmController.cs).

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
  return useQuery({
    queryKey: ['platform', 'crm', 'contacts'],
    queryFn: () => api.get<CrmContact[]>('/api/platform/crm/contacts'),
    staleTime: 30_000,
  })
}

export interface CreateCrmContactPayload {
  fullName: string
  companyName: string
  email: string
  phone?: string | undefined
  role?: string | undefined
  leadId?: string | undefined
}

export function useCreateCrmContact() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateCrmContactPayload) => api.post<CrmContact>('/api/platform/crm/contacts', payload),
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
  return useQuery({
    queryKey: ['platform', 'crm', 'activities'],
    queryFn: () => api.get<CrmActivity[]>('/api/platform/crm/activities'),
    staleTime: 30_000,
  })
}

export interface LogCrmActivityPayload {
  contactId: string
  type: CrmActivityType
  summary: string
}

export function useLogCrmActivity() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: LogCrmActivityPayload) => api.post<CrmActivity>('/api/platform/crm/activities', payload),
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
  return useQuery({
    queryKey: ['platform', 'crm', 'tasks', status ?? 'All'],
    queryFn: () => api.get<CrmTask[]>('/api/platform/crm/tasks', status && status !== 'All' ? { status } : undefined),
    staleTime: 30_000,
  })
}

export interface CreateCrmTaskPayload {
  title: string
  dueDate?: string | undefined
  priority: CrmTaskPriority
}

export function useCreateCrmTask() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateCrmTaskPayload) => api.post<CrmTask>('/api/platform/crm/tasks', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'tasks'] }),
  })
}

export function useCompleteCrmTask(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.put<CrmTask>(`/api/platform/crm/tasks/${id}/complete`, {}),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'crm', 'tasks'] }),
  })
}
