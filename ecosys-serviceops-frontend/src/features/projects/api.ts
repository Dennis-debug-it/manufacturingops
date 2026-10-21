import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  Consultant,
  MemberRole,
  MilestoneStatus,
  ProjectConsultantLink,
  ProjectDetail,
  ProjectDocument,
  ProjectListItem,
  ProjectMaterialIssue,
  ProjectMember,
  ProjectMilestone,
  ProjectPeople,
  ProjectRegisterSummary,
  ProjectSettings,
  ProjectStage,
  ProjectTask,
  ProjectTemplate,
  ProjectTemplateItem,
  ProjectType,
  ProjectVariation,
  ProjectWorkOrder,
  StageCategory,
  TaskStatus,
  VariationKind,
} from './types'

/** Every project query lives under this key, so one invalidation refreshes the register and workspace. */
const projectsKey = ['projects'] as const

function useInvalidateProjects() {
  const queryClient = useQueryClient()
  return () => void queryClient.invalidateQueries({ queryKey: projectsKey })
}

// ── Register ─────────────────────────────────────────────────────────────────

export interface ProjectListFilters {
  stageCategory?: string
  projectTypeId?: string
  clientId?: string
  search?: string
  /** Only projects the caller manages or is on. */
  mine?: boolean
}

export function useProjects(filters?: ProjectListFilters) {
  return useQuery({
    queryKey: [...projectsKey, 'list', filters ?? {}],
    queryFn: () => api.get<ProjectListItem[]>(endpoints.projects.list, { ...filters }),
    staleTime: 30_000,
  })
}

export function useProjectSummary() {
  return useQuery({
    queryKey: [...projectsKey, 'summary'],
    queryFn: () => api.get<ProjectRegisterSummary>(endpoints.projects.summary),
    staleTime: 30_000,
  })
}

export function useProjectPeople(enabled = true) {
  return useQuery({
    enabled,
    queryKey: [...projectsKey, 'people'],
    queryFn: () => api.get<ProjectPeople>(endpoints.projects.people),
    staleTime: 60_000,
  })
}

export interface UpsertProjectPayload {
  name: string
  clientId: string
  siteId: string | null
  projectTypeId: string | null
  stageId?: string | null
  projectManagerUserId: string | null
  branchId?: string | null
  originalValue: number
  currency: string
  plannedStartDate: string | null
  plannedEndDate: string | null
  description: string | null
  templateId?: string | null
  visibleInClientPortal?: boolean
}

export function useCreateProject() {
  const invalidate = useInvalidateProjects()
  return useMutation({
    mutationFn: (payload: UpsertProjectPayload) => api.post<ProjectDetail>(endpoints.projects.create, payload),
    onSuccess: invalidate,
  })
}

// ── Workspace ────────────────────────────────────────────────────────────────

export function useProject(id: string | undefined) {
  return useQuery({
    enabled: Boolean(id),
    queryKey: [...projectsKey, id],
    queryFn: () => api.get<ProjectDetail>(endpoints.projects.detail(id ?? '')),
  })
}

function useProjectList<T>(id: string, name: string, url: string, enabled = true) {
  return useQuery({
    enabled: Boolean(id) && enabled,
    queryKey: [...projectsKey, id, name],
    queryFn: () => api.get<T[]>(url),
  })
}

export const useProjectMilestones = (id: string, enabled = true) => useProjectList<ProjectMilestone>(id, 'milestones', endpoints.projects.milestones(id), enabled)
export const useProjectTasks = (id: string, enabled = true) => useProjectList<ProjectTask>(id, 'tasks', endpoints.projects.tasks(id), enabled)
export const useProjectVariations = (id: string, enabled = true) => useProjectList<ProjectVariation>(id, 'variations', endpoints.projects.variations(id), enabled)
export const useProjectWorkOrders = (id: string, enabled = true) => useProjectList<ProjectWorkOrder>(id, 'work-orders', endpoints.projects.workOrders(id), enabled)
export const useProjectMaterialIssues = (id: string, enabled = true) => useProjectList<ProjectMaterialIssue>(id, 'material-issues', endpoints.projects.materialIssues(id), enabled)
export const useProjectDocuments = (id: string, enabled = true) => useProjectList<ProjectDocument>(id, 'documents', endpoints.projects.documents(id), enabled)

/** A mutation that refreshes every project query when it succeeds. */
function useProjectMutation<TPayload, TResult = unknown>(mutationFn: (payload: TPayload) => Promise<TResult>) {
  const invalidate = useInvalidateProjects()
  return useMutation({ mutationFn, onSuccess: invalidate })
}

export const useUpdateProject = (id: string) =>
  useProjectMutation((payload: UpsertProjectPayload) => api.put<ProjectDetail>(endpoints.projects.update(id), payload))

export const useChangeProjectStage = (id: string) =>
  useProjectMutation((payload: { stageId: string; note: string | null }) => api.post<ProjectDetail>(endpoints.projects.stage(id), payload))

export const useOverrideProgress = (id: string) =>
  useProjectMutation((payload: { percent: number | null; reason: string | null }) => api.post<ProjectDetail>(endpoints.projects.progressOverride(id), payload))

export interface MemberPayload {
  userId?: string | null
  technicianId?: string | null
  role: MemberRole
  trade: string | null
  fromDate: string | null
  toDate: string | null
}

export const useAddMember = (id: string) =>
  useProjectMutation((payload: MemberPayload) => api.post<ProjectMember>(endpoints.projects.members(id), payload))

export const useRemoveMember = (id: string) =>
  useProjectMutation((memberId: string) => api.delete(endpoints.projects.member(id, memberId)))

export const useLinkConsultant = (id: string) =>
  useProjectMutation((payload: { consultantId: string; roleOnProject: string | null }) => api.post<ProjectConsultantLink>(endpoints.projects.consultants(id), payload))

export const useUnlinkConsultant = (id: string) =>
  useProjectMutation((linkId: string) => api.delete(endpoints.projects.consultant(id, linkId)))

export interface MilestonePayload {
  name: string
  weight: number | null
  plannedDate: string | null
  forecastDate: string | null
  sortOrder?: number | null
}

export const useCreateMilestone = (id: string) =>
  useProjectMutation((payload: MilestonePayload) => api.post<ProjectMilestone>(endpoints.projects.milestones(id), payload))

export const useUpdateMilestone = (id: string) =>
  useProjectMutation(({ milestoneId, ...payload }: MilestonePayload & { milestoneId: string }) => api.put<ProjectMilestone>(endpoints.projects.milestone(id, milestoneId), payload))

export const useSetMilestoneStatus = (id: string) =>
  useProjectMutation(({ milestoneId, status }: { milestoneId: string; status: MilestoneStatus }) => api.post<ProjectMilestone>(endpoints.projects.milestoneStatus(id, milestoneId), { status }))

export const useDeleteMilestone = (id: string) =>
  useProjectMutation((milestoneId: string) => api.delete(endpoints.projects.milestone(id, milestoneId)))

export interface TaskPayload {
  title: string
  description: string | null
  milestoneId: string | null
  assigneeUserId: string | null
  dueDate: string | null
  priority: string
  status: TaskStatus | null
}

export const useCreateTask = (id: string) =>
  useProjectMutation((payload: TaskPayload) => api.post<ProjectTask>(endpoints.projects.tasks(id), payload))

export const useUpdateTask = (id: string) =>
  useProjectMutation(({ taskId, ...payload }: TaskPayload & { taskId: string }) => api.put<ProjectTask>(endpoints.projects.task(id, taskId), payload))

export const useDeleteTask = (id: string) =>
  useProjectMutation((taskId: string) => api.delete(endpoints.projects.task(id, taskId)))

export interface VariationPayload {
  kind: VariationKind
  amount: number
  title: string
  description: string | null
  clientReference: string | null
}

export const useCreateVariation = (id: string) =>
  useProjectMutation((payload: VariationPayload) => api.post<ProjectVariation>(endpoints.projects.variations(id), payload))

export const useUpdateVariation = (id: string) =>
  useProjectMutation(({ variationId, ...payload }: VariationPayload & { variationId: string }) => api.put<ProjectVariation>(endpoints.projects.variation(id, variationId), payload))

export const useSubmitVariation = (id: string) =>
  useProjectMutation((variationId: string) => api.post<ProjectVariation>(endpoints.projects.variationSubmit(id, variationId)))

export const useDecideVariation = (id: string) =>
  useProjectMutation(({ variationId, approve, note }: { variationId: string; approve: boolean; note: string | null }) =>
    api.post<ProjectVariation>(endpoints.projects.variationDecision(id, variationId), { approve, note }))

export const useVoidVariation = (id: string) =>
  useProjectMutation(({ variationId, reason }: { variationId: string; reason: string | null }) =>
    api.post<ProjectVariation>(endpoints.projects.variationVoid(id, variationId), { reason }))

export const useLinkWorkOrder = (id: string) =>
  useProjectMutation((workOrderId: string) => api.post(endpoints.projects.workOrders(id), { recordId: workOrderId }))

export const useUnlinkWorkOrder = (id: string) =>
  useProjectMutation((workOrderId: string) => api.delete(endpoints.projects.workOrder(id, workOrderId)))

export const useLinkMaterialIssue = (id: string) =>
  useProjectMutation((issueId: string) => api.post(endpoints.projects.materialIssues(id), { recordId: issueId }))

export const useUnlinkMaterialIssue = (id: string) =>
  useProjectMutation((issueId: string) => api.delete(endpoints.projects.materialIssue(id, issueId)))

export const useUploadProjectDocument = (id: string) =>
  useProjectMutation((file: File) => {
    const formData = new FormData()
    formData.append('entityType', 'Project')
    formData.append('entityId', id)
    formData.append('file', file)
    return api.post(endpoints.projects.documentUpload, formData)
  })

export const useSetDocumentSharing = (id: string) =>
  useProjectMutation(({ attachmentId, sharedWithClient }: { attachmentId: string; sharedWithClient: boolean }) =>
    api.put(endpoints.projects.documentSharing(id, attachmentId), { sharedWithClient }))

export const useDeleteProjectDocument = () =>
  useProjectMutation((attachmentId: string) => api.delete(endpoints.projects.documentDelete(attachmentId)))

/** Downloads a project document through the authenticated client. */
export async function downloadProjectDocument(document: ProjectDocument) {
  const response = await apiClient.get(document.downloadUrl, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = document.fileName
  link.click()
  URL.revokeObjectURL(url)
}

// ── Consultant directory ─────────────────────────────────────────────────────

export function useConsultants(search?: string, includeInactive = false) {
  return useQuery({
    queryKey: [...projectsKey, 'consultants', search ?? '', includeInactive],
    queryFn: () => api.get<Consultant[]>(endpoints.consultants.list, { search, includeInactive }),
    staleTime: 30_000,
  })
}

export interface ConsultantPayload {
  firmName: string
  discipline: string | null
  contactPerson: string | null
  phone: string | null
  email: string | null
  physicalAddress: string | null
  notes: string | null
  isActive?: boolean
}

export const useCreateConsultant = () =>
  useProjectMutation((payload: ConsultantPayload) => api.post<Consultant>(endpoints.consultants.create, payload))

export const useUpdateConsultant = () =>
  useProjectMutation(({ id, ...payload }: ConsultantPayload & { id: string }) => api.put<Consultant>(endpoints.consultants.detail(id), payload))

export const useDeleteConsultant = () =>
  useProjectMutation((id: string) => api.delete(endpoints.consultants.detail(id)))

// ── Settings ─────────────────────────────────────────────────────────────────

export function useProjectSettings() {
  return useQuery({
    queryKey: [...projectsKey, 'settings'],
    queryFn: () => api.get<ProjectSettings>(endpoints.projectSettings.settings),
    staleTime: 60_000,
  })
}

export const useSaveProjectSettings = () =>
  useProjectMutation((payload: ProjectSettings) => api.put<ProjectSettings>(endpoints.projectSettings.settings, payload))

export function useProjectTypes() {
  return useQuery({
    queryKey: [...projectsKey, 'types'],
    queryFn: () => api.get<ProjectType[]>(endpoints.projectSettings.types),
    staleTime: 60_000,
  })
}

export function useProjectStages() {
  return useQuery({
    queryKey: [...projectsKey, 'stages'],
    queryFn: () => api.get<ProjectStage[]>(endpoints.projectSettings.stages),
    staleTime: 60_000,
  })
}

export function useProjectTemplates() {
  return useQuery({
    queryKey: [...projectsKey, 'templates'],
    queryFn: () => api.get<ProjectTemplate[]>(endpoints.projectSettings.templates),
    staleTime: 60_000,
  })
}

export interface ProjectTypePayload {
  name: string
  description: string | null
  defaultTemplateId: string | null
  isActive: boolean
  sortOrder?: number | null
}

export const useSaveProjectType = () =>
  useProjectMutation(({ id, ...payload }: ProjectTypePayload & { id?: string }) =>
    id ? api.put<ProjectType>(endpoints.projectSettings.type(id), payload) : api.post<ProjectType>(endpoints.projectSettings.types, payload))

export interface ProjectStagePayload {
  name: string
  category: StageCategory
  isActive: boolean
  sortOrder?: number | null
}

export const useSaveProjectStage = () =>
  useProjectMutation(({ id, ...payload }: ProjectStagePayload & { id?: string }) =>
    id ? api.put<ProjectStage>(endpoints.projectSettings.stage(id), payload) : api.post<ProjectStage>(endpoints.projectSettings.stages, payload))

export const useSaveProjectTemplate = () =>
  useProjectMutation(({ id, ...payload }: { id?: string; name: string; isActive: boolean; items: ProjectTemplateItem[] }) =>
    id ? api.put<ProjectTemplate>(endpoints.projectSettings.template(id), payload) : api.post<ProjectTemplate>(endpoints.projectSettings.templates, payload))
