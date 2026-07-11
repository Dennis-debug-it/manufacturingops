import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { AssignmentGroup, CertificationDefinition, TeamManagementOverview, TeamUser, Technician } from './types'

export function useTeamUsers() {
  return useQuery({
    queryKey: ['team-users'],
    queryFn: () => api.get<TeamUser[]>(endpoints.users.list),
    staleTime: 30_000,
  })
}

export function useTeamUser(id: string | undefined) {
  return useQuery({
    queryKey: ['team-users', id],
    queryFn: () => api.get<TeamUser>(endpoints.users.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateUserPayload {
  fullName: string
  email: string
  phoneNumber?: string | null
  role: 'Admin' | 'User'
  jobTitle?: string | null
  department?: string | null
  isActive: boolean
  credentialDeliveryMethod?: string
}

export function useCreateTeamUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateUserPayload) => api.post<TeamUser>(endpoints.users.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team-users'] }),
  })
}

export function useSetUserStatus(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (isActive: boolean) => api.patch<TeamUser>(endpoints.users.status(id), { isActive }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team-users'] }),
  })
}

export function useResendCredentials(id: string) {
  return useMutation({
    mutationFn: () => api.post<{ success: boolean; message: string | null }>(endpoints.users.resendCredentials(id)),
  })
}

export function useResetUserPassword(id: string) {
  return useMutation({
    mutationFn: () => api.post<{ success: boolean; message: string | null }>(endpoints.users.resetPassword(id)),
  })
}

// Technicians
export function useTechnicians(branchId?: string) {
  return useQuery({
    queryKey: ['technicians', branchId ?? ''],
    queryFn: () => api.get<Technician[]>(endpoints.technicians.list, { branchId }),
    staleTime: 30_000,
  })
}

export function useTechnician(id: string | undefined) {
  return useQuery({
    queryKey: ['technicians', id],
    queryFn: () => api.get<Technician>(endpoints.technicians.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertTechnicianPayload {
  branchId?: string | null
  fullName: string
  email: string
  phone?: string | null
  skillCategory?: string | null
  assignmentGroup?: string | null
}

export function useCreateTechnician() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertTechnicianPayload) => api.post<Technician>(endpoints.technicians.list, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['technicians'] }),
  })
}

// Team Management overview (availability, certifications, readiness)
export function useTeamManagementOverview() {
  return useQuery({
    queryKey: ['team-management', 'overview'],
    queryFn: () => api.get<TeamManagementOverview>(endpoints.teamManagement.overview),
    staleTime: 30_000,
  })
}

export interface UpsertCertificationPayload {
  name: string
  issuer?: string | null
  description?: string | null
  renewalPeriodMonths?: number | null
  isActive: boolean
}

export function useCreateCertification() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertCertificationPayload) => api.post<CertificationDefinition>(endpoints.teamManagement.certifications, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team-management'] }),
  })
}

export function useUpdateCertification(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertCertificationPayload) => api.put<CertificationDefinition>(endpoints.teamManagement.certification(id), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team-management'] }),
  })
}

export function useDeleteCertification() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.teamManagement.certification(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team-management'] }),
  })
}

// Assignment Groups
export function useAssignmentGroups() {
  return useQuery({
    queryKey: ['assignment-groups'],
    queryFn: () => api.get<AssignmentGroup[]>(endpoints.assignmentGroups.list),
    staleTime: 30_000,
  })
}

export function useAssignmentGroup(id: string | undefined) {
  return useQuery({
    queryKey: ['assignment-groups', id],
    queryFn: () => api.get<AssignmentGroup>(endpoints.assignmentGroups.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertAssignmentGroupPayload {
  name: string
  description?: string | null
  skillArea?: string | null
  branchId?: string | null
  isActive: boolean
}

export function useCreateAssignmentGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertAssignmentGroupPayload) => api.post<AssignmentGroup>(endpoints.assignmentGroups.list, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['assignment-groups'] }),
  })
}

export function useUpdateAssignmentGroup(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertAssignmentGroupPayload) => api.put<AssignmentGroup>(endpoints.assignmentGroups.detail(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups'] })
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups', id] })
    },
  })
}

export function useDeleteAssignmentGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.assignmentGroups.detail(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['assignment-groups'] }),
  })
}

export function useAddAssignmentGroupMember(groupId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { userId?: string | null; technicianId?: string | null; isLead: boolean }) =>
      api.post(endpoints.assignmentGroups.members(groupId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups'] })
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups', groupId] })
    },
  })
}

export function useRemoveAssignmentGroupMember(groupId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (memberUserOrTechnicianId: string) =>
      api.delete(`${endpoints.assignmentGroups.members(groupId)}/${memberUserOrTechnicianId}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups'] })
      void queryClient.invalidateQueries({ queryKey: ['assignment-groups', groupId] })
    },
  })
}
