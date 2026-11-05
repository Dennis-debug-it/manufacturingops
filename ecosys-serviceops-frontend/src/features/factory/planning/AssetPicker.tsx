import { useState } from 'react'
import { X } from 'lucide-react'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { useAssetSearch } from '../api'
import { inputClass } from '../components/ui'

export interface PickedAsset { id: string; code: string; name: string; categoryId: string | null }

/** Search-and-pick one factory asset. */
export function AssetPicker({ value, onChange, placeholder = 'Search assets by name or number', disabled }: {
  value: PickedAsset | null
  onChange: (asset: PickedAsset | null) => void
  placeholder?: string | undefined
  disabled?: boolean | undefined
}) {
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 250)
  const results = useAssetSearch(debounced, debounced.trim().length >= 2)

  if (value) {
    return (
      <div className="flex h-10 items-center justify-between rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm">
        <span className="truncate"><span className="font-mono text-xs text-slate-500">{value.code}</span> {value.name}</span>
        {!disabled ? <button type="button" aria-label="Change asset" className="text-slate-500" onClick={() => onChange(null)}><X className="h-4 w-4" /></button> : null}
      </div>
    )
  }

  return (
    <div className="relative">
      <input className={inputClass} value={search} onChange={(event) => setSearch(event.target.value)} placeholder={placeholder} disabled={disabled} />
      {debounced.trim().length >= 2 ? (
        <ul className="absolute z-20 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-lg">
          {(results.data?.items ?? []).map((item) => (
            <li key={item.id}>
              <button type="button" className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50"
                onClick={() => { onChange({ id: item.id, code: item.assetCode, name: item.assetName, categoryId: item.assetCategoryId }); setSearch('') }}>
                <span className="font-mono text-xs text-slate-500">{item.assetCode}</span> {item.assetName}
              </button>
            </li>
          ))}
          {results.data && results.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">No matching assets</li> : null}
        </ul>
      ) : null}
    </div>
  )
}

/** Pick several assets in order (a route). */
export function AssetListPicker({ value, onChange }: { value: PickedAsset[]; onChange: (assets: PickedAsset[]) => void }) {
  const move = (index: number, by: number) => {
    const next = [...value]
    const [item] = next.splice(index, 1)
    if (item) next.splice(index + by, 0, item)
    onChange(next)
  }
  return (
    <div className="space-y-2">
      {value.length > 0 ? (
        <ol className="space-y-1 rounded-lg border border-slate-200 p-2 text-sm">
          {value.map((asset, index) => (
            <li key={asset.id} className="flex items-center justify-between gap-2">
              <span className="truncate"><span className="mr-2 tabular-nums text-slate-400">{index + 1}.</span><span className="font-mono text-xs">{asset.code}</span> {asset.name}</span>
              <span className="flex shrink-0 gap-1">
                <button type="button" className="rounded px-2 text-slate-500 hover:bg-slate-100 disabled:opacity-30" disabled={index === 0} onClick={() => move(index, -1)} aria-label="Move up">↑</button>
                <button type="button" className="rounded px-2 text-slate-500 hover:bg-slate-100 disabled:opacity-30" disabled={index === value.length - 1} onClick={() => move(index, 1)} aria-label="Move down">↓</button>
                <button type="button" className="rounded px-2 text-slate-500 hover:bg-slate-100" onClick={() => onChange(value.filter((x) => x.id !== asset.id))} aria-label="Remove"><X className="h-3.5 w-3.5" /></button>
              </span>
            </li>
          ))}
        </ol>
      ) : null}
      <AssetPicker value={null} onChange={(asset) => asset && !value.some((x) => x.id === asset.id) && onChange([...value, asset])} placeholder="Add an asset to the route" />
    </div>
  )
}
