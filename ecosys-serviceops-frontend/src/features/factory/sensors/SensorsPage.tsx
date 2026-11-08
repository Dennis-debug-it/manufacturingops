import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, KeyRound, Plus, RadioTower } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { formatDateTime } from '../../../shared/utils/dates'
import { Field, LocationSelect, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'

export interface SensorGateway {
  id: string
  name: string
  description: string | null
  orgNodeId: string | null
  orgNodeName: string | null
  keyPrefix: string
  isActive: boolean
  sampleSeconds: number
  mqttTrustBroker: boolean
  lastSeenAt: string | null
  acceptedCount: number
  rejectedCount: number
  lastError: string | null
  lastErrorAt: string | null
  createdAt: string
}

interface SensorMeter {
  meterId: string
  tag: string
  meterName: string
  unit: string
  assetId: string
  assetName: string
  assetCode: string
  currentValue: number | null
  lastReadingAt: string | null
  lastSensorReadingAt: string | null
}

interface SaveGateway {
  name: string
  description: string | null
  orgNodeId: string | null
  sampleSeconds: number
  mqttTrustBroker: boolean
  isActive?: boolean
}

const KEY = ['factory', 'sensor-gateways']

function useGateways() {
  return useQuery({ queryKey: KEY, queryFn: () => api.get<SensorGateway[]>('/api/sensor-gateways') })
}

function useGatewayMeters(id: string | null) {
  return useQuery({ queryKey: [...KEY, id, 'meters'], queryFn: () => api.get<SensorMeter[]>(`/api/sensor-gateways/${id}/meters`), enabled: Boolean(id) })
}

function useSaveGateway() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: SaveGateway }) =>
      id
        ? api.put<SensorGateway>(`/api/sensor-gateways/${id}`, payload).then((gateway) => ({ gateway, key: null as string | null }))
        : api.post<{ gateway: SensorGateway; key: string }>('/api/sensor-gateways', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

function useRotateKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<{ gateway: SensorGateway; key: string }>(`/api/sensor-gateways/${id}/rotate-key`, {}),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

/** "Every reading", "1 min", "5 min", "1 h". */
export function sampleLabel(seconds: number): string {
  if (seconds <= 0) return 'Every reading'
  if (seconds % 3600 === 0) return `${seconds / 3600} h`
  if (seconds % 60 === 0) return `${seconds / 60} min`
  return `${seconds} s`
}

/** Settings → Sensors: the systems that send readings in, their keys and how to connect them. */
export function SensorsPage() {
  const { data: gateways, isLoading } = useGateways()
  const [editing, setEditing] = useState<SensorGateway | 'new' | null>(null)
  const [shownKey, setShownKey] = useState<{ gateway: SensorGateway; key: string } | null>(null)
  const [selected, setSelected] = useState<string | null>(null)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Sensors"
        subtitle="PLCs, SCADA, data loggers and vehicle trackers that send meter readings in, over HTTP or MQTT."
        breadcrumbs={[{ label: 'Settings', to: '/settings' }, { label: 'Sensors' }]}
        primaryAction={<button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add gateway</button>}
      />

      {isLoading ? <Spinner label="Loading gateways" /> : (gateways ?? []).length === 0 ? (
        <EmptyState title="No sensor gateways yet" description="Add one for each system that will send readings. Then give each meter it feeds a sensor tag on the asset's Meters tab." />
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
          {gateways!.map((gateway) => (
            <li key={gateway.id} className="space-y-2 px-4 py-3">
              <div className="flex flex-wrap items-center gap-3">
                <RadioTower className="h-5 w-5 text-slate-400" />
                <button type="button" className="font-medium text-slate-900 hover:underline" onClick={() => setSelected(selected === gateway.id ? null : gateway.id)}>{gateway.name}</button>
                <Badge tone={gateway.isActive ? 'success' : 'neutral'} size="sm">{gateway.isActive ? 'Active' : 'Disabled'}</Badge>
                <span className="font-mono text-xs text-slate-500">{gateway.keyPrefix}…</span>
                <span className="text-xs text-slate-500">{gateway.orgNodeName ?? 'All locations'} · keeps {sampleLabel(gateway.sampleSeconds).toLowerCase()}</span>
                <span className="ml-auto text-xs text-slate-500">
                  {gateway.lastSeenAt ? `Last heard ${formatDateTime(gateway.lastSeenAt)}` : 'Not heard from yet'} · {gateway.acceptedCount.toLocaleString()} stored · {gateway.rejectedCount.toLocaleString()} refused
                </span>
                <button type="button" className={secondaryButton} onClick={() => setEditing(gateway)}>Edit</button>
              </div>
              {gateway.lastError ? <p className="text-xs text-amber-700">Last problem ({gateway.lastErrorAt ? formatDateTime(gateway.lastErrorAt) : ''}): {gateway.lastError}</p> : null}
              {selected === gateway.id ? <GatewayDetail gateway={gateway} onNewKey={(key) => setShownKey({ gateway, key })} /> : null}
            </li>
          ))}
        </ul>
      )}

      {editing ? <GatewayDrawer gateway={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onCreated={(created) => setShownKey(created)} /> : null}
      {shownKey ? <KeyDrawer gateway={shownKey.gateway} sensorKey={shownKey.key} onClose={() => setShownKey(null)} /> : null}
    </div>
  )
}

function GatewayDetail({ gateway, onNewKey }: { gateway: SensorGateway; onNewKey: (key: string) => void }) {
  const { data: meters, isLoading } = useGatewayMeters(gateway.id)
  const rotate = useRotateKey()
  const links = useFactoryLinks()
  const rotateKey = async () => {
    if (!window.confirm(`Make a new key for ${gateway.name}? The current key stops working at once.`)) return
    try {
      onNewKey((await rotate.mutateAsync(gateway.id)).key)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-3 rounded-lg bg-slate-50 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-900">Meters it can feed</h3>
        <button type="button" className={secondaryButton} disabled={rotate.isPending} onClick={() => void rotateKey()}><KeyRound className="h-4 w-4" />New key</button>
      </div>
      {isLoading ? <Spinner label="Loading meters" /> : (meters ?? []).length === 0 ? (
        <p className="text-sm text-slate-500">No meter has a sensor tag{gateway.orgNodeName ? ` at ${gateway.orgNodeName}` : ''} yet. Set one on the asset's Meters tab (Edit → Sensor tag).</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Tag</th><th>Meter</th><th className="text-right">Now</th><th>Last from a sensor</th></tr></thead>
          <tbody className="divide-y divide-slate-200">
            {meters!.map((meter) => (
              <tr key={meter.meterId}>
                <td className="py-1.5 font-mono text-xs">{meter.tag}</td>
                <td><Link to={links.asset(meter.assetId, 'meters')} className="hover:underline">{meter.assetName}</Link> · {meter.meterName}</td>
                <td className="text-right tabular-nums">{formatNumber(meter.currentValue, meter.unit)}</td>
                <td className="text-slate-600">{meter.lastSensorReadingAt ? formatDateTime(meter.lastSensorReadingAt) : 'Never'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function GatewayDrawer({ gateway, onClose, onCreated }: { gateway: SensorGateway | null; onClose: () => void; onCreated: (created: { gateway: SensorGateway; key: string }) => void }) {
  const save = useSaveGateway()
  const [form, setForm] = useState({
    name: gateway?.name ?? '',
    description: gateway?.description ?? '',
    orgNodeId: gateway?.orgNodeId ?? '',
    sampleSeconds: String(gateway?.sampleSeconds ?? 300),
    mqttTrustBroker: gateway?.mqttTrustBroker ?? false,
    isActive: gateway?.isActive ?? true,
  })
  const set = (key: keyof typeof form, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }))
  const submit = async () => {
    try {
      const result = await save.mutateAsync({
        id: gateway?.id,
        payload: {
          name: form.name,
          description: form.description.trim() || null,
          orgNodeId: form.orgNodeId || null,
          sampleSeconds: Number(form.sampleSeconds) || 0,
          mqttTrustBroker: form.mqttTrustBroker,
          ...(gateway ? { isActive: form.isActive } : {}),
        },
      })
      toast.success(gateway ? 'Gateway saved' : 'Gateway added')
      onClose()
      if (result.key) onCreated({ gateway: result.gateway, key: result.key })
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open title={gateway ? `Edit ${gateway.name}` : 'Add sensor gateway'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!form.name.trim() || save.isPending} onClick={() => void submit()}>{gateway ? 'Save' : 'Add and show key'}</button>
      </div>
    }>
      <div className="grid gap-4">
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => set('name', e.target.value)} placeholder="e.g. Chelal boiler PLC" /></Field>
        <Field label="Description"><textarea className={textareaClass} rows={2} value={form.description} onChange={(e) => set('description', e.target.value)} /></Field>
        <Field label="Location" hint="Limit it to meters at this location and below. Blank allows every location.">
          <LocationSelect value={form.orgNodeId} onChange={(value) => set('orgNodeId', value)} emptyLabel="All locations" includeGroups />
        </Field>
        <Field label="Keep a steady reading at most every" hint="A reading that changes a meter's condition is always kept.">
          <select className={inputClass} value={form.sampleSeconds} onChange={(e) => set('sampleSeconds', e.target.value)}>
            {[0, 60, 300, 900, 3600].map((seconds) => <option key={seconds} value={seconds}>{sampleLabel(seconds)}</option>)}
          </select>
        </Field>
        <label className="inline-flex items-start gap-2 text-sm text-slate-700">
          <input type="checkbox" className="mt-1" checked={form.mqttTrustBroker} onChange={(e) => set('mqttTrustBroker', e.target.checked)} />
          <span>MQTT: trust the broker's own access control, so messages need not carry the key. Only tick this if the broker lets nobody else publish to this gateway's topic.</span>
        </label>
        {gateway ? (
          <label className="inline-flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />Active (a disabled gateway's readings are refused)</label>
        ) : null}
      </div>
    </FormDrawer>
  )
}

function KeyDrawer({ gateway, sensorKey, onClose }: { gateway: SensorGateway; sensorKey: string; onClose: () => void }) {
  const origin = typeof window === 'undefined' ? '' : window.location.origin
  const curl = `curl -X POST ${origin}/api/sensors/readings \\\n  -H "X-Sensor-Key: ${sensorKey}" -H "Content-Type: application/json" \\\n  -d '{"readings":[{"tag":"BLR1.PRESSURE","value":8.4}]}'`
  const topic = `ecosys/sensors/${gateway.id}/<tag>`
  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
      toast.success('Copied')
    } catch {
      toast.error('Could not copy; select it and copy by hand')
    }
  }
  return (
    <FormDrawer open title={`Key for ${gateway.name}`} onClose={onClose} footer={<div className="flex justify-end"><button type="button" className={primaryButton} onClick={onClose}>I have saved the key</button></div>}>
      <div className="space-y-4 text-sm">
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-amber-800">This is the only time the key is shown. Store it in the gateway's configuration now.</p>
        <div className="flex items-center gap-2">
          <code className="flex-1 break-all rounded bg-slate-100 px-2 py-1.5 font-mono text-xs">{sensorKey}</code>
          <button type="button" className={secondaryButton} onClick={() => void copy(sensorKey)}><Copy className="h-4 w-4" />Copy</button>
        </div>
        <div>
          <h3 className="mb-1 font-semibold text-slate-900">HTTP</h3>
          <p className="mb-2 text-slate-600">POST up to 500 readings at a time. A reading without "at" is taken as now.</p>
          <pre className="overflow-x-auto rounded bg-slate-900 p-3 text-xs text-slate-100">{curl}</pre>
        </div>
        <div>
          <h3 className="mb-1 font-semibold text-slate-900">MQTT</h3>
          <p className="text-slate-600">Publish to <code className="rounded bg-slate-100 px-1 font-mono text-xs">{topic}</code> a number, or JSON such as
            {' '}<code className="rounded bg-slate-100 px-1 font-mono text-xs">{'{"value": 8.4, "at": "2026-11-08T06:00:00Z", "key": "…"}'}</code>.
            {gateway.mqttTrustBroker ? ' The key may be left out, as this gateway trusts the broker.' : ' Include the key in each message.'}</p>
        </div>
      </div>
    </FormDrawer>
  )
}
