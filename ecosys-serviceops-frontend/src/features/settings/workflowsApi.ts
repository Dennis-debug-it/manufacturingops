import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface WorkflowStatus {
  id: string
  workflowDefinitionId: string
  name: string
  color: string
  sortOrder: number
  isInitial: boolean
  isFinal: boolean
  allowedNextStatusIds: string[]
}

export interface WorkflowDefinition {
  id: string
  name: string
  isDefault: boolean
  isActive: boolean
  statuses: WorkflowStatus[]
  createdAt: string
  updatedAt: string | null
}

export interface UpsertWorkflowPayload {
  name: string
  isDefault: boolean
  isActive: boolean
}

export interface UpsertWorkflowStatusPayload {
  name: string
  color: string
  sortOrder: number | null
  isInitial: boolean
  isFinal: boolean
  allowedNextStatusIds: string[] | null
}

const workflowsKey = ['settings', 'workflows'] as const

export function useWorkflows() {
  return useQuery({
    queryKey: workflowsKey,
    queryFn: () => api.get<WorkflowDefinition[]>(endpoints.workflows.list),
    staleTime: 30_000,
  })
}

export function useCreateWorkflow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkflowPayload) => api.post<WorkflowDefinition>(endpoints.workflows.create, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

export function useUpdateWorkflow(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkflowPayload) => api.put<WorkflowDefinition>(endpoints.workflows.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

export function useDeleteWorkflow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.workflows.delete(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

export function useCreateWorkflowStatus(workflowId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkflowStatusPayload) =>
      api.post<WorkflowStatus>(endpoints.workflows.createStatus(workflowId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

export function useUpdateWorkflowStatus(workflowId: string, statusId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkflowStatusPayload) =>
      api.put<WorkflowStatus>(endpoints.workflows.updateStatus(workflowId, statusId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

export function useDeleteWorkflowStatus(workflowId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (statusId: string) => api.delete(endpoints.workflows.deleteStatus(workflowId, statusId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workflowsKey })
    },
  })
}

const workflowStatusesKey = ['work-orders', 'workflow-statuses'] as const

export function useWorkflowStatuses() {
  return useQuery({
    queryKey: workflowStatusesKey,
    queryFn: () => api.get<WorkflowStatus[]>(endpoints.workOrders.workflowStatuses),
    staleTime: 30_000,
  })
}
