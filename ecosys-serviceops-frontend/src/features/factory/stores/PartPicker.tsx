import { useState } from 'react'
import { X } from 'lucide-react'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { inputClass } from '../components/ui'
import { useSpareParts } from './api'

export interface PickedPart { id: string; code: string; name: string; unitOfMeasure: string; isSerialised: boolean }

/** Search-and-pick one spare part (by name, part number or barcode). */
export function PartPicker({ value, onChange }: { value: PickedPart | null; onChange: (part: PickedPart | null) => void }) {
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 250)
  const results = useSpareParts(debounced, 'all')

  if (value) {
    return (
      <div className="flex h-10 items-center justify-between rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm">
        <span className="truncate"><span className="font-mono text-xs text-slate-500">{value.code}</span> {value.name}</span>
        <button type="button" aria-label="Change part" className="text-slate-500" onClick={() => onChange(null)}><X className="h-4 w-4" /></button>
      </div>
    )
  }

  const rows = debounced.trim().length >= 2 ? (results.data ?? []).slice(0, 12) : []
  return (
    <div className="relative">
      <input className={inputClass} value={search} placeholder="Search by name, part number or barcode" onChange={(event) => setSearch(event.target.value)} />
      {rows.length > 0 ? (
        <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-lg">
          {rows.map((part) => (
            <li key={part.id}>
              <button type="button" className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-slate-50"
                onClick={() => { onChange({ id: part.id, code: part.itemCode, name: part.itemName, unitOfMeasure: part.unitOfMeasure, isSerialised: part.isSerialised }); setSearch('') }}>
                <span><span className="font-mono text-xs text-slate-500">{part.itemCode}</span> {part.itemName}</span>
                <span className="shrink-0 text-xs text-slate-500">{part.groupOnHand - part.groupReserved} {part.unitOfMeasure} free</span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
