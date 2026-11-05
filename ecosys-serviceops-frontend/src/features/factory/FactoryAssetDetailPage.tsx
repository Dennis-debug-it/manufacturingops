import { useState, type ReactNode } from 'react'
import { IsolationPointsPanel } from './people/JobSafetyCard'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ArrowRightLeft, Pencil } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { ErrorState } from '../../shared/components/ErrorState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { useFactoryAsset, useFactorySettings, useLifecycleAction, useOrgNodes, useReactivateAsset, useRetireAsset } from './api'
import { pathOf } from './tree'
import { useFactoryLinks } from './links'
import { FactoryAssetFormDrawer } from './components/FactoryAssetFormDrawer'
import { ComponentsPanel, DocumentsPanel, HistoryPanel, MetersPanel, TagsPanel } from './components/AssetPanels'
import { CriticalityBadge, Field, LocationSelect, OperationalBadge, dangerButton, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from './components/ui'
import type { FactoryAsset } from './types'

type Dialog = 'edit' | 'component' | 'transfer' | 'commission' | 'retire' | 'dispose' | null

export function FactoryAssetDetailPage() {
  const { id = '' } = useParams()
  const { data: asset, isLoading, isError } = useFactoryAsset(id)
  const { data: nodes } = useOrgNodes()
  const canManage = usePermission('assets.manage')
  const canRecord = usePermission('work-orders.complete') || canManage
  const reactivate = useReactivateAsset(id)
  const [dialog, setDialog] = useState<Dialog>(null)
  const links = useFactoryLinks()

  if (isLoading) return <p className="p-6 text-sm text-slate-500">Loading…</p>
  if (isError || !asset) return <ErrorState title="Asset not found" description="It may have been removed, or it is in a factory you don't have access to." />

  const retired = asset.status === 'Inactive'
  const location = pathOf(asset.orgNodeId, nodes ?? []) || asset.orgNodeName

  const doReactivate = async () => {
    try {
      await reactivate.mutateAsync()
      toast.success('Back in service')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={asset.assetName}
        subtitle={[asset.assetCode, location, asset.assetCategoryName].filter(Boolean).join(' · ')}
        breadcrumbs={[links.tech ? { label: 'Scan', to: links.scan() } : { label: 'Assets', to: '/assets' }, ...(asset.parentAssetId ? [{ label: asset.parentAssetCode ?? 'Parent', to: links.asset(asset.parentAssetId) }] : []), { label: asset.assetCode }]}
        titleAdornment={<span className="flex gap-1"><CriticalityBadge value={asset.criticality} /><OperationalBadge value={asset.operationalStatus} retired={retired} /></span>}
        primaryAction={canManage && !retired ? <button type="button" className={primaryButton} onClick={() => setDialog('edit')}><Pencil className="h-4 w-4" />Edit</button> : undefined}
        secondaryActions={canManage ? (
          <div className="flex flex-wrap gap-2">
            {!retired && !asset.parentAssetId ? <button type="button" className={secondaryButton} onClick={() => setDialog('transfer')}><ArrowRightLeft className="h-4 w-4" />Transfer</button> : null}
            {!retired && !asset.commissioningDate ? <button type="button" className={secondaryButton} onClick={() => setDialog('commission')}>Commission</button> : null}
            {!retired ? <button type="button" className={dangerButton} onClick={() => setDialog('retire')}>Retire</button> : null}
            {retired && !asset.disposalDate ? <button type="button" className={secondaryButton} onClick={() => void doReactivate()}>Back in service</button> : null}
            {retired && !asset.disposalDate ? <button type="button" className={dangerButton} onClick={() => setDialog('dispose')}>Record disposal</button> : null}
          </div>
        ) : undefined}
      />

      <DetailLayout
        header={null}
        tabs={[
          { id: 'overview', label: 'Overview', content: <Overview asset={asset} location={location} /> },
          { id: 'components', label: 'Components', content: <ComponentsPanel asset={asset} canManage={canManage} onAdd={() => setDialog('component')} /> },
          { id: 'meters', label: 'Meters', content: <MetersPanel asset={asset} canManage={canManage && !retired} canRecord={canRecord && !retired} /> },
          { id: 'documents', label: 'Documents', content: <DocumentsPanel asset={asset} canManage={canManage} /> },
          { id: 'tag', label: 'Tag', content: <TagsPanel asset={asset} canManage={canManage} /> },
          { id: 'isolation', label: 'Isolation', content: <IsolationPointsPanel assetId={asset.id} canManage={canManage} /> },
          { id: 'history', label: 'History', content: <HistoryPanel assetId={asset.id} /> },
        ]}
      />

      <FactoryAssetFormDrawer open={dialog === 'edit'} asset={asset} onClose={() => setDialog(null)} />
      <FactoryAssetFormDrawer open={dialog === 'component'} defaultParent={{ id: asset.id, label: `${asset.assetCode} · ${asset.assetName}` }} onClose={() => setDialog(null)} />
      {dialog === 'transfer' || dialog === 'commission' || dialog === 'dispose' || dialog === 'retire' ? (
        <LifecycleDialog kind={dialog} asset={asset} onClose={() => setDialog(null)} />
      ) : null}
    </div>
  )
}

function Overview({ asset, location }: { asset: FactoryAsset; location: string | null }) {
  const links = useFactoryLinks()
  const { data: settings } = useFactorySettings()
  const rows: [string, ReactNode][] = [
    ['Number', <span className="font-mono">{asset.assetCode}</span>],
    ['Location', location || '—'],
    ['Part of', asset.parentAssetId ? <Link className="underline" to={links.asset(asset.parentAssetId)}>{asset.parentAssetCode} · {asset.parentAssetName}</Link> : '—'],
    ['Class', asset.assetCategoryName ?? '—'],
    ['Manufacturer', asset.manufacturer ?? '—'],
    ['Model', asset.model ?? '—'],
    ['Serial number', asset.serialNumber ?? '—'],
    ['Installed', asset.installationDate ? formatDate(asset.installationDate) : '—'],
    ['Commissioned', asset.commissioningDate ? formatDate(asset.commissioningDate) : '—'],
    ['Warranty', asset.warrantyExpiryDate ? `${asset.isUnderWarranty ? 'Until' : 'Ended'} ${formatDate(asset.warrantyExpiryDate)}` : 'None recorded'],
    ['Production lost per hour down', formatNumber(asset.lostProductionPerHour, settings?.productionUnit)],
    ['ERP ID', asset.externalId ? `${asset.externalId}${asset.externalSystem ? ` (${asset.externalSystem})` : ''}` : '—'],
    ...(asset.status === 'Inactive' ? [['Retired', `${asset.decommissionDate ? formatDate(asset.decommissionDate) : ''} ${asset.decommissionReason ?? ''}`.trim()] as [string, ReactNode]] : []),
    ...(asset.disposalDate ? [['Disposed of', `${formatDate(asset.disposalDate)} · ${asset.disposalMethod ?? ''}`] as [string, ReactNode]] : []),
  ]
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <dl className="grid grid-cols-[minmax(0,10rem)_1fr] gap-x-4 gap-y-2 rounded-lg border border-slate-200 bg-white p-4 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-slate-500">{label}</dt>
            <dd className="text-slate-900">{value}</dd>
          </div>
        ))}
      </dl>
      <div className="space-y-4">
        {asset.customFieldValues.length > 0 ? (
          <dl className="grid grid-cols-[minmax(0,10rem)_1fr] gap-x-4 gap-y-2 rounded-lg border border-slate-200 bg-white p-4 text-sm">
            {asset.customFieldValues.map((field) => (
              <div key={field.fieldDefinitionId} className="contents">
                <dt className="text-slate-500">{field.fieldLabel}</dt>
                <dd className="text-slate-900">{field.value || '—'}</dd>
              </div>
            ))}
          </dl>
        ) : null}
        {asset.notes ? <p className="whitespace-pre-wrap rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-700">{asset.notes}</p> : null}
      </div>
    </div>
  )
}

const TITLES = { transfer: 'Transfer asset', commission: 'Commission asset', retire: 'Retire asset', dispose: 'Record disposal' } as const

function LifecycleDialog({ kind, asset, onClose }: { kind: keyof typeof TITLES; asset: FactoryAsset; onClose: () => void }) {
  const lifecycle = useLifecycleAction(asset.id)
  const retire = useRetireAsset(asset.id)
  const [date, setDate] = useState('')
  const [text, setText] = useState('')
  const [method, setMethod] = useState('')
  const [toOrgNodeId, setTo] = useState('')
  const pending = lifecycle.isPending || retire.isPending

  const ready = kind === 'transfer' ? Boolean(toOrgNodeId) : kind === 'retire' ? Boolean(text.trim()) : kind === 'dispose' ? Boolean(method.trim()) : true

  const submit = async () => {
    try {
      if (kind === 'retire') {
        await retire.mutateAsync({ decommissionDate: date || null, reason: text })
        toast.success('Asset retired')
      } else if (kind === 'transfer') {
        const result = await lifecycle.mutateAsync({ action: 'transfer', payload: { toOrgNodeId, effectiveDate: date || null, reason: text || null } }) as { assetsMoved: number; toOrgNodeName: string }
        toast.success(`Moved to ${result.toOrgNodeName}${result.assetsMoved > 1 ? ` with ${result.assetsMoved - 1} component${result.assetsMoved === 2 ? '' : 's'}` : ''}`)
      } else if (kind === 'commission') {
        await lifecycle.mutateAsync({ action: 'commission', payload: { commissioningDate: date || null, notes: text || null } })
        toast.success('Commissioning recorded')
      } else {
        await lifecycle.mutateAsync({ action: 'dispose', payload: { disposalDate: date || null, method, notes: text || null } })
        toast.success('Disposal recorded')
      }
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open title={TITLES[kind]} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={kind === 'retire' || kind === 'dispose' ? dangerButton : primaryButton} disabled={!ready || pending} onClick={() => void submit()}>{TITLES[kind]}</button>
      </div>
    }>
      <div className="grid gap-4">
        {kind === 'transfer' ? (
          <>
            <p className="text-sm text-slate-600">Components at the same location move with it.</p>
            <Field label="Move to" required><LocationSelect value={toOrgNodeId} onChange={setTo} /></Field>
          </>
        ) : null}
        {kind === 'dispose' ? (
          <>
            <p className="text-sm text-slate-600">Disposal is final: the asset can't be brought back into service afterwards.</p>
            <Field label="How" required><input className={inputClass} value={method} onChange={(event) => setMethod(event.target.value)} placeholder="Scrapped, sold, returned to supplier…" /></Field>
          </>
        ) : null}
        <Field label="Date" hint="Blank means today."><input type="date" className={inputClass} value={date} onChange={(event) => setDate(event.target.value)} /></Field>
        <Field label={kind === 'retire' ? 'Reason' : 'Notes'} required={kind === 'retire'}>
          <textarea className={textareaClass} value={text} onChange={(event) => setText(event.target.value)} />
        </Field>
      </div>
    </FormDrawer>
  )
}
