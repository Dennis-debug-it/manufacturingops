import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface ApprovalLevel {
  levelOrder: number
  name: string
  minAmount: number
  maxAmount: number | null
  approverType: 'Permission' | 'SpecificUsers'
  requiredPermission: string | null
  approverUserIds: string[]
  reminderAfterHours: number | null
}

export interface ApprovalMatrix {
  documentType: string
  currency: string
  isActive: boolean
  isConfigured: boolean
  levels: ApprovalLevel[]
}

export interface UpsertApprovalMatrixPayload {
  currency: string
  isActive: boolean
  levels: Array<{
    name: string
    minAmount: number
    maxAmount: number | null
    approverType: 'Permission' | 'SpecificUsers'
    requiredPermission: string | null
    approverUserIds: string[]
    reminderAfterHours: number | null
  }>
}

export interface ApprovalStep {
  levelOrder: number
  levelName: string
  status: string
  decidedByName: string | null
  decidedAt: string | null
  note: string | null
}

export interface ApprovalRequest {
  id: string
  documentType: string
  documentId: string
  documentNumber: string
  amount: number
  currency: string
  status: string
  currentLevelOrder: number
  totalLevels: number
  currentLevelName: string
  createdAt: string
  steps: ApprovalStep[]
}

export function useApprovalMatrices() {
  return useQuery({
    queryKey: ['approval-matrices'],
    queryFn: () => api.get<ApprovalMatrix[]>(endpoints.approvals.matrices),
    staleTime: 30_000,
  })
}

export function useUpsertApprovalMatrix(documentType: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertApprovalMatrixPayload) => api.put<ApprovalMatrix>(endpoints.approvals.matrix(documentType), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['approval-matrices'] }),
  })
}

export function usePendingApprovals() {
  return useQuery({
    queryKey: ['approvals', 'pending'],
    queryFn: () => api.get<ApprovalRequest[]>(endpoints.approvals.pending),
    staleTime: 15_000,
  })
}

export function useDecideApproval(requestId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { approve: boolean; note: string | null }) => api.post(endpoints.approvals.decide(requestId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['approvals', 'pending'] })
    },
  })
}
