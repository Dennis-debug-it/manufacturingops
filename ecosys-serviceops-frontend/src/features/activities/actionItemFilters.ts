import type { UrlFilterSpec } from '../../shared/utils/urlFilters'
import type { ActionItem } from './types'

/** Days ahead that count as "due this week", today included, as the dashboard counts them. */
export const DUE_SOON_DAYS = 7

/** `?due=overdue|week`: open items past their due date, or due within {@link DUE_SOON_DAYS} days. */
export const actionItemFilterSpec: UrlFilterSpec<'due'> = {
  due: { param: 'due', values: ['overdue', 'week'] },
}

function dateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

/** Due dates are plain dates (yyyy-MM-dd), so they compare as strings against local today. */
export function filterActionItemsByDue<T extends Pick<ActionItem, 'dueDate' | 'status'>>(items: T[], due: string, today = new Date()): T[] {
  if (!due) return items
  const todayKey = dateKey(today)
  const lastKey = dateKey(new Date(today.getFullYear(), today.getMonth(), today.getDate() + DUE_SOON_DAYS - 1))
  return items.filter((item) => {
    if (item.status !== 'Open' || !item.dueDate) return false
    const key = item.dueDate.slice(0, 10)
    return due === 'overdue' ? key < todayKey : key >= todayKey && key <= lastKey
  })
}
