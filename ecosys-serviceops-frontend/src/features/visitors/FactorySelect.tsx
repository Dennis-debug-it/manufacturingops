import { useOrgNodes } from '../factory/api'
import { inputClass } from '../factory/components/ui'

/** Picks a factory the person can see. Visits and zones belong to a factory, not a section. */
export function FactorySelect({ value, onChange, emptyLabel = 'Choose the factory' }: { value: string; onChange: (value: string) => void; emptyLabel?: string }) {
  const { data } = useOrgNodes()
  const factories = (data ?? []).filter((node) => node.level === 'Factory' && node.isActive && node.canAccess !== false)
  return (
    <select value={value} onChange={(event) => onChange(event.target.value)} className={inputClass}>
      <option value="">{emptyLabel}</option>
      {factories.map((node) => <option key={node.id} value={node.id}>{node.name}</option>)}
    </select>
  )
}

/** The only factory a person can see, so single-factory sites need not choose. */
export function useOnlyFactory(): string {
  const { data } = useOrgNodes()
  const factories = (data ?? []).filter((node) => node.level === 'Factory' && node.isActive && node.canAccess !== false)
  return factories.length === 1 ? factories[0]!.id : ''
}
