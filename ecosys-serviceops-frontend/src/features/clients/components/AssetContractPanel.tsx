import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FileSignature } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { Spinner } from '../../../shared/components/Spinner'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { useAddAssetToContract, useAssetContractOptions } from '../api'
import type { Asset, AssetContractCover } from '../types'

const scopeText: Record<AssetContractCover['scope'], string> = {
  Asset: 'listed on the contract by name',
  Category: "through its asset category",
  Site: 'through its site',
  Client: "the contract covers all the client's assets",
}

/** "Covered by" with a link to the contract, or a plain "not covered". */
export function ContractCoverValue({ asset }: { asset: Asset }) {
  const cover = asset.coveredBy
  if (!cover) return <span>{asset.hasContractCover ? 'Yes' : 'No'}</span>
  return (
    <span>
      <Link to={`/contracts/${cover.contractId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{cover.contractNumber}</Link>
      <span className="block text-xs text-slate-500">{cover.contractName}</span>
    </span>
  )
}

/**
 * The asset's contract cover in full: which contract, how it reaches the asset, until when — and,
 * for someone who manages contracts, adding the asset to one of the client's contracts.
 */
export function AssetContractPanel({ asset }: { asset: Asset }) {
  const canManage = usePermission('contracts.manage')
  const [open, setOpen] = useState(false)
  const cover = asset.coveredBy

  return (
    <div className="mt-6 rounded-xl border border-slate-200 px-4 py-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="text-sm">
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Contract cover</div>
          {cover ? (
            <p className="mt-2 text-slate-700">
              Covered by{' '}
              <Link to={`/contracts/${cover.contractId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{cover.contractNumber} · {cover.contractName}</Link>
              {' '}— {scopeText[cover.scope]}, until {formatDate(cover.endDate)}.
            </p>
          ) : asset.status === 'Inactive' ? (
            <p className="mt-2 text-slate-500">Retired assets are not covered.</p>
          ) : (
            <p className="mt-2 text-slate-700">Not covered by any of {asset.clientName ?? "the client"}'s active contracts.</p>
          )}
        </div>
        {canManage && asset.status !== 'Inactive' ? (
          <button type="button" onClick={() => setOpen(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            <FileSignature className="h-3.5 w-3.5" />Add to contract
          </button>
        ) : null}
      </div>
      {open ? <AddToContractDrawer asset={asset} onClose={() => setOpen(false)} /> : null}
    </div>
  )
}

function AddToContractDrawer({ asset, onClose }: { asset: Asset; onClose: () => void }) {
  const { data, isLoading, isError } = useAssetContractOptions(asset.id, true)
  const add = useAddAssetToContract(asset.id)

  const addTo = async (contractId: string, contractNumber: string) => {
    try {
      await add.mutateAsync(contractId)
      toast.success(`${asset.assetName} added to ${contractNumber}`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Add ${asset.assetName} to a contract`}
      onClose={onClose}
      footer={<div className="flex justify-end"><button type="button" onClick={onClose} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Close</button></div>}
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">
          {asset.clientName ?? "The client"}'s open contracts. An asset can be added to a contract that lists specific assets. A contract that covers by site, category or all assets already decides coverage that way — change its scope on the contract itself.
        </p>
        {isLoading ? <Spinner label="Loading contracts" /> : isError ? (
          <p className="text-sm text-rose-700">Contracts could not be loaded.</p>
        ) : (data ?? []).length === 0 ? (
          <p className="rounded-lg border border-dashed border-slate-300 px-3 py-4 text-sm text-slate-500">
            {asset.clientName ?? 'This client'} has no open contracts. <Link to="/contracts" className="font-medium text-slate-800 underline">Create one under Contracts</Link>.
          </p>
        ) : (
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {(data ?? []).map((option) => (
              <li key={option.contractId} className="flex flex-wrap items-start justify-between gap-3 px-3 py-3">
                <div className="min-w-0 text-sm">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link to={`/contracts/${option.contractId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{option.contractNumber}</Link>
                    <span className="text-slate-700">{option.contractName}</span>
                    <StatusBadge status={option.status} />
                  </div>
                  <p className="mt-1 text-xs text-slate-500">{option.scopeLabel} · {formatDate(option.startDate)} – {formatDate(option.endDate)}</p>
                  {option.reason ? <p className={`mt-1 text-xs ${option.scopeCoversAsset ? 'text-emerald-700' : 'text-slate-600'}`}>{option.reason}</p> : null}
                </div>
                {option.canAdd ? (
                  <button type="button" disabled={add.isPending} onClick={() => void addTo(option.contractId, option.contractNumber)} className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
                    Add
                  </button>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </div>
    </FormDrawer>
  )
}
