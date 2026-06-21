import { api } from '../lib/api'
import type {
  CertificationDefinitionRecord,
  TeamManagementOverviewRecord,
  TeamManagementProfileRecord,
  UpdateTeamManagementProfileInput,
  UpsertCertificationDefinitionInput,
} from '../types/api'

export const teamManagementService = {
  getOverview(signal?: AbortSignal) {
    return api.get<TeamManagementOverviewRecord>('/api/team-management/overview', { signal })
  },
  createCertification(input: UpsertCertificationDefinitionInput) {
    return api.post<CertificationDefinitionRecord>('/api/team-management/certifications', input)
  },
  updateCertification(id: string, input: UpsertCertificationDefinitionInput) {
    return api.put<CertificationDefinitionRecord>(`/api/team-management/certifications/${id}`, input)
  },
  deleteCertification(id: string) {
    return api.delete<void>(`/api/team-management/certifications/${id}`)
  },
  updateUserProfile(userId: string, input: UpdateTeamManagementProfileInput) {
    return api.put<TeamManagementProfileRecord>(`/api/team-management/users/${userId}/profile`, input)
  },
}
