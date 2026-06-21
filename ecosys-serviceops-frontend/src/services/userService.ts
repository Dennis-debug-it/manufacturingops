import { api } from '../lib/api'
import type { ApiPermissions, UpsertUserInput, UserRecord } from '../types/api'
import { asArray } from '../utils/apiDefaults'

type CredentialDeliveryResponse = {
  id: string
  fullName: string
  email: string
  success: boolean
  lastCredentialSentAt?: string | null
  status: string
  message?: string | null
}

export const userService = {
  async list(signal?: AbortSignal): Promise<UserRecord[]> {
    const response = await api.get<unknown>('/api/users', { signal })
    return asArray<UserRecord>(response)
  },
  get(id: string, signal?: AbortSignal) {
    return api.get<UserRecord>(`/api/users/${id}`, { signal })
  },
  create(input: UpsertUserInput) {
    return api.post<UserRecord>('/api/users', {
      ...input,
      credentialDeliveryMethod: input.credentialDeliveryMethod ?? 'InviteEmail',
      password: input.password || '',
      isActive: input.isActive ?? true,
    })
  },
  update(id: string, input: UpsertUserInput) {
    return api.put<UserRecord>(`/api/users/${id}`, {
      ...input,
      isActive: input.isActive ?? true,
    })
  },
  updateStatus(id: string, isActive: boolean) {
    return api.patch<UserRecord>(`/api/users/${id}/status`, { isActive })
  },
  resetPassword(id: string, temporaryPassword: string) {
    return api.post<CredentialDeliveryResponse>(`/api/users/${id}/reset-password`, { temporaryPassword })
  },
  resendCredentials(id: string) {
    return api.post<CredentialDeliveryResponse>(`/api/users/${id}/resend-credentials`, {})
  },
  remove(id: string) {
    return api.delete<null>(`/api/users/${id}`)
  },
  async updatePermissions(id: string, permissions: ApiPermissions) {
    const current = await this.get(id)
    return this.update(id, {
      fullName: current.fullName,
      email: current.email,
      phoneNumber: current.phoneNumber,
      role: current.role,
      jobTitle: current.jobTitle,
      department: current.department,
      isActive: current.isActive,
      permissions,
      branchIds: current.branchIds,
      defaultBranchId: current.defaultBranchId,
      hasAllBranchAccess: current.hasAllBranchAccess,
    })
  },
}
