import type { BoardWorkOrder } from './boardApi'

export const BOARD_START_HOUR = 7
export const BOARD_END_HOUR = 20
export const SLOT_MINUTES = 30
export const SLOT_COUNT = ((BOARD_END_HOUR - BOARD_START_HOUR) * 60) / SLOT_MINUTES
export const SLOT_WIDTH = 64
export const ROW_HEIGHT = 68

export function dayStartOf(date: Date): Date {
  const d = new Date(date)
  d.setHours(BOARD_START_HOUR, 0, 0, 0)
  return d
}

export function slotIndexToDate(day: Date, slotIndex: number): Date {
  const start = dayStartOf(day)
  return new Date(start.getTime() + slotIndex * SLOT_MINUTES * 60_000)
}

export function dateToSlotOffset(day: Date, date: Date): number {
  const start = dayStartOf(day)
  return (date.getTime() - start.getTime()) / (SLOT_MINUTES * 60_000)
}

export function slotLabel(slotIndex: number): string {
  const totalMinutes = BOARD_START_HOUR * 60 + slotIndex * SLOT_MINUTES
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

export function clampSlotIndex(index: number): number {
  return Math.min(Math.max(index, 0), SLOT_COUNT - 1)
}

export function hasOverlap(workOrders: BoardWorkOrder[], start: Date, end: Date, excludeId?: string): boolean {
  return workOrders.some((wo) => {
    if (wo.id === excludeId) return false
    if (!wo.scheduledStart || !wo.scheduledEnd) return false
    const woStart = new Date(wo.scheduledStart)
    const woEnd = new Date(wo.scheduledEnd)
    return woStart < end && woEnd > start
  })
}
