export type ActivityType = 'Meeting' | 'SiteVisit' | 'Inspection' | 'Call'
export type ActivityStatus = 'Scheduled' | 'Held' | 'Cancelled'
export type AttendeeResponse = 'Pending' | 'Accepted' | 'Declined'
export type ActionItemStatus = 'Open' | 'Done' | 'Cancelled'
export type LinkedEntityType = 'Project' | 'Client' | 'Contract' | 'WorkOrder'
export type AttendeeKind = 'Staff' | 'Consultant' | 'ClientContact' | 'External'

export const activityTypeLabels: Record<ActivityType, string> = {
  Meeting: 'Meeting',
  SiteVisit: 'Site visit',
  Inspection: 'Inspection',
  Call: 'Call',
}

export interface LinkedRecord {
  entityType: LinkedEntityType
  entityId: string
  label: string
  url: string | null
}

export interface ActivitySummary {
  id: string
  type: ActivityType
  title: string
  startsAt: string
  endsAt: string
  allDay: boolean
  location: string | null
  status: ActivityStatus
  organizerUserId: string
  organizerName: string | null
  linkedRecord: LinkedRecord | null
  attendeeCount: number
  minutesPublishedAt: string | null
  myResponse: AttendeeResponse | null
}

export interface ActivityAttendee {
  id: string
  kind: AttendeeKind
  userId: string | null
  consultantId: string | null
  clientContactId: string | null
  name: string
  email: string | null
  response: AttendeeResponse | null
  attended: boolean
}

export interface MinutesAddendum {
  id: string
  text: string
  authorName: string | null
  createdAt: string
}

export interface ActionItem {
  id: string
  title: string
  assigneeUserId: string | null
  assigneeName: string | null
  assigneeExternalName: string | null
  dueDate: string | null
  status: ActionItemStatus
  activityId: string | null
  activityTitle: string | null
  linkedRecord: LinkedRecord | null
  createdAt: string
  completedAt: string | null
  canEdit: boolean
}

export interface ActivityDetail {
  summary: ActivitySummary
  agenda: string | null
  minutes: string | null
  reminderMinutesBefore: number | null
  cancellationReason: string | null
  attendees: ActivityAttendee[]
  addenda: MinutesAddendum[]
  actionItems: ActionItem[]
  canManage: boolean
  isAttendee: boolean
}

export interface PersonOption {
  id: string
  name: string
  detail: string | null
}

export interface ActivityPeople {
  users: PersonOption[]
  consultants: PersonOption[]
  clientContacts: PersonOption[]
}

export interface AttendeePayload {
  userId: string | null
  consultantId: string | null
  clientContactId: string | null
  externalName: string | null
  externalEmail: string | null
}

export interface SaveActivityPayload {
  type: ActivityType
  title: string
  startsAt: string
  endsAt: string
  allDay: boolean
  location: string | null
  organizerUserId: string | null
  entityType: LinkedEntityType | null
  entityId: string | null
  agenda: string | null
  reminderMinutesBefore: number | null
  attendees: AttendeePayload[]
}

export interface SaveActionItemPayload {
  title: string
  assigneeUserId: string | null
  assigneeExternalName: string | null
  dueDate: string | null
  status: ActionItemStatus | null
  activityId: string | null
  entityType: LinkedEntityType | null
  entityId: string | null
}

export type CalendarSource = 'activities' | 'action-items' | 'work-orders' | 'maintenance' | 'projects' | 'contracts'

export const calendarSourceLabels: Record<CalendarSource, string> = {
  activities: 'Meetings & visits',
  'action-items': 'Action items',
  'work-orders': 'Work orders',
  maintenance: 'Maintenance due',
  projects: 'Project dates',
  contracts: 'Contract dates',
}

export interface CalendarItem {
  uid: string
  source: CalendarSource
  title: string
  startsAt: string
  endsAt: string
  allDay: boolean
  location: string | null
  url: string | null
  userId: string | null
  status: string | null
}

export interface CalendarResponse {
  sources: CalendarSource[]
  canViewTeam: boolean
  items: CalendarItem[]
}

export interface CalendarFeedStatus {
  active: boolean
  createdAt: string | null
  lastUsedAt: string | null
  /** Only returned when a link is created; it is never shown again. */
  path: string | null
}
