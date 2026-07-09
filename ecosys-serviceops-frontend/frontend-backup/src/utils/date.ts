import { addDays, differenceInMinutes, format, isAfter, parseISO } from 'date-fns'
import type { Priority, SlaStatus, WorkOrderRecord } from '../types/app'

export function nowIso() {
  return new Date().toISOString()
}

export function formatDateTime(value?: string) {
  if (!value) {
    return 'Not set'
  }

  return format(parseISO(value), 'dd MMM yyyy, HH:mm')
}

export function formatDateOnly(value?: string) {
  if (!value) {
    return 'Not set'
  }

  return format(parseISO(value), 'dd MMM yyyy')
}

export function addHoursIso(hours: number, from: string = nowIso()) {
  return new Date(new Date(from).getTime() + hours * 60 * 60 * 1000).toISOString()
}

export function addDaysIso(days: number, from: string = nowIso()) {
  return addDays(new Date(from), days).toISOString()
}

export function formatRelativeCountdown(targetIso: string) {
  const diffMinutes = differenceInMinutes(parseISO(targetIso), new Date())
  if (diffMinutes <= 0) {
    return 'Breached'
  }

  const hours = Math.floor(diffMinutes / 60)
  const minutes = diffMinutes % 60
  return `${hours}h ${String(minutes).padStart(2, '0')}m`
}

export function deriveSlaStatus(workOrder: Pick<WorkOrderRecord, 'status' | 'resolutionDueAt'>): SlaStatus {
  if (workOrder.status === 'Completed') {
    return 'On Track'
  }

  if (workOrder.status === 'Cancelled') {
    return 'Breached'
  }

  const due = parseISO(workOrder.resolutionDueAt)
  const now = new Date()

  if (isAfter(now, due)) {
    return 'Breached'
  }

  const diffMinutes = differenceInMinutes(due, now)
  return diffMinutes <= 120 ? 'At Risk' : 'On Track'
}

export function priorityToHours(priority: Priority) {
  if (priority === 'Critical') return 4
  if (priority === 'High') return 8
  if (priority === 'Medium') return 24
  return 48
}
