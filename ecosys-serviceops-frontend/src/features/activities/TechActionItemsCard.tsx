import { usePermission } from '../../shared/hooks/usePermission'
import { useActionItems } from './api'
import { ActionItemList } from './ActionItems'

/** "My action items" on the technician app home (design 5.2). Hidden when there is nothing open. */
export function TechActionItemsCard() {
  const canView = usePermission('activities.view')
  const { data } = useActionItems({ mine: true, status: 'Open' }, canView)
  if (!canView || !data || data.length === 0) return null
  return (
    <section className="space-y-2" aria-label="My action items">
      <h2 className="text-sm font-semibold text-slate-900">My action items <span className="font-normal text-slate-500">({data.length})</span></h2>
      <ActionItemList items={data.slice(0, 5)} showSource={false} />
    </section>
  )
}
