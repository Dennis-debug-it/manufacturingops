import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Fuel, MapPin, Plus, RefreshCw, Satellite, Truck, Wrench } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { StatCard } from '../../../shared/components/StatCard'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission, usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, LocationSelect, formatNumber, inputClass, primaryButton, secondaryButton } from '../components/ui'
import { useFactoryLinks } from '../links'
import {
  VEHICLE_TYPES, mapLink, serviceDueSoon, useAddFuel, useFleet, useSaveTelematics, useSaveVehicle, useSyncTelematics, useTelematics, useVehicle, worstDocument,
  type FleetVehicle, type SaveVehiclePayload, type VehicleDetail,
} from './api'

const dateInput = (value: string | null | undefined) => (value ? value.slice(0, 10) : '')
const num = (value: string) => (value.trim() === '' ? null : Number(value))
const text = (value: string) => value.trim() || null

/** The leaf collection fleet: each vehicle's odometer, next service, papers, fuel use and last position. */
export function FleetPage() {
  const [orgNodeId, setOrgNodeId] = useState('')
  const { data: vehicles, isLoading } = useFleet(orgNodeId)
  const canManage = usePermission('assets.manage')
  const canSettings = usePermission('settings.manage')
  const [editing, setEditing] = useState<FleetVehicle | 'new' | null>(null)
  const [open, setOpen] = useState<string | null>(null)
  const rows = vehicles ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title="Leaf collection fleet"
        subtitle="Lorries, pickups and tractors that bring green leaf in: service by the kilometre, papers, fuel use and where each one is."
        primaryAction={canManage ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add vehicle</button> : undefined}
        secondaryActions={<div className="w-56"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>}
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Vehicles" value={isLoading ? '—' : rows.length} icon={<Truck className="h-4 w-4 text-slate-400" />} />
        <StatCard label="Service due soon" value={isLoading ? '—' : rows.filter((x) => serviceDueSoon(x)).length} tone={rows.some((x) => serviceDueSoon(x)) ? 'warning' : 'default'} icon={<Wrench className="h-4 w-4 text-amber-500" />} />
        <StatCard label="Papers ending within 30 days" value={isLoading ? '—' : rows.filter((x) => worstDocument(x)).length} tone={rows.some((x) => (worstDocument(x)?.daysLeft ?? 99) <= 7) ? 'danger' : 'default'} />
        <StatCard label="Using more fuel" value={isLoading ? '—' : rows.filter((x) => x.fuel.usingMore).length} tone={rows.some((x) => x.fuel.usingMore) ? 'warning' : 'default'} icon={<Fuel className="h-4 w-4 text-slate-400" />} />
      </div>

      {isLoading ? <Spinner label="Loading the fleet" /> : rows.length === 0 ? (
        <EmptyState title="No vehicles yet" description="Add each leaf collection vehicle. It becomes an asset, so jobs, PM by the kilometre, daily checks and driver fault reports work for it too." />
      ) : <FleetTable rows={rows} onOpen={setOpen} />}

      {canSettings ? <TrackingCard /> : null}
      {editing ? <VehicleDrawer vehicle={editing === 'new' ? null : editing} onClose={() => setEditing(null)} /> : null}
      {open ? <VehicleDetailDrawer assetId={open} onClose={() => setOpen(null)} onEdit={canManage ? (vehicle) => { setOpen(null); setEditing(vehicle) } : null} /> : null}
    </div>
  )
}

function FleetTable({ rows, onOpen }: { rows: FleetVehicle[]; onOpen: (assetId: string) => void }) {
  const links = useFactoryLinks()
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
      <table className="w-full min-w-[980px] text-sm">
        <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-3 py-2">Vehicle</th><th className="px-3 py-2">Route · driver</th><th className="px-3 py-2 text-right">Odometer</th>
            <th className="px-3 py-2">Next service</th><th className="px-3 py-2">Papers</th><th className="px-3 py-2 text-right">km/l</th>
            <th className="px-3 py-2">Last seen</th><th className="px-3 py-2 text-right">Open jobs</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((row) => {
            const paper = worstDocument(row)
            return (
              <tr key={row.assetId} className="cursor-pointer hover:bg-slate-50" onClick={() => onOpen(row.assetId)}>
                <td className="px-3 py-2">
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-semibold text-slate-900">{row.registrationNumber}</span>
                    {row.condition !== 'Normal' ? <Badge tone={row.condition === 'Alarm' ? 'danger' : 'warning'} size="sm">{row.condition}</Badge> : null}
                  </div>
                  <div className="text-xs text-slate-500">{[row.vehicleType, row.make, row.model, row.payloadKg ? `${formatNumber(row.payloadKg)} kg` : null].filter(Boolean).join(' · ')}</div>
                </td>
                <td className="px-3 py-2 text-slate-600">{row.route ?? '—'}<div className="text-xs text-slate-500">{row.driverName ?? ''}</div></td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {formatNumber(row.odometerKm, 'km')}
                  {row.engineHours !== null ? <div className="text-xs text-slate-500">{formatNumber(row.engineHours, 'h')}</div> : null}
                </td>
                <td className={cn('px-3 py-2', serviceDueSoon(row) ? 'font-medium text-amber-700' : 'text-slate-600')}>
                  {row.kmToService !== null ? (row.kmToService <= 0 ? `Overdue by ${formatNumber(-row.kmToService, 'km')}` : `In ${formatNumber(row.kmToService, 'km')}`)
                    : row.serviceDueOn ? formatDate(row.serviceDueOn) : <span className="text-slate-400">No plan</span>}
                  {row.servicePlanName ? <div className="text-xs font-normal text-slate-500">{row.servicePlanName}</div> : null}
                </td>
                <td className="px-3 py-2">
                  {paper ? <Badge tone={paper.daysLeft <= 7 ? 'danger' : 'warning'} size="sm">{paper.label} {paper.daysLeft <= 0 ? 'ends today' : `in ${paper.daysLeft} d`}</Badge>
                    : row.documents.length === 0 ? <span className="text-xs text-slate-400">Not recorded</span> : <span className="text-xs text-emerald-700">Current</span>}
                </td>
                <td className={cn('px-3 py-2 text-right tabular-nums', row.fuel.usingMore && 'font-medium text-amber-700')}>
                  {row.fuel.latestKmPerLitre ?? '—'}
                  {row.fuel.averageKmPerLitre !== null ? <div className="text-xs font-normal text-slate-500">avg {row.fuel.averageKmPerLitre}</div> : null}
                </td>
                <td className="px-3 py-2 text-slate-600">
                  {row.lastPositionAt && row.lastLatitude !== null && row.lastLongitude !== null ? (
                    <a href={mapLink(row.lastLatitude, row.lastLongitude)} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-[var(--app-primary)] hover:underline" onClick={(e) => e.stopPropagation()}>
                      <MapPin className="h-3.5 w-3.5" />{formatDateTime(row.lastPositionAt)}
                    </a>
                  ) : <span className="text-xs text-slate-400">{row.trackerDeviceId ? 'No position yet' : 'No tracker'}</span>}
                </td>
                <td className="px-3 py-2 text-right">
                  <Link to={`${links.asset(row.assetId)}`} className="tabular-nums text-[var(--app-primary)] hover:underline" onClick={(e) => e.stopPropagation()}>{row.openJobs}</Link>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function VehicleDrawer({ vehicle, onClose }: { vehicle: FleetVehicle | null; onClose: () => void }) {
  const save = useSaveVehicle()
  const detail = useVehicle(vehicle?.assetId ?? null)
  return vehicle && !detail.data ? <FormDrawer open title="Edit vehicle" onClose={onClose}><Spinner label="Loading" /></FormDrawer>
    : <VehicleForm vehicle={vehicle} detail={detail.data ?? null} save={save} onClose={onClose} />
}

function VehicleForm({ vehicle, detail, save, onClose }: { vehicle: FleetVehicle | null; detail: VehicleDetail | null; save: ReturnType<typeof useSaveVehicle>; onClose: () => void }) {
  const [form, setForm] = useState({
    registrationNumber: vehicle?.registrationNumber ?? '',
    vehicleType: vehicle?.vehicleType ?? 'Lorry',
    orgNodeId: '',
    make: vehicle?.make ?? '',
    model: vehicle?.model ?? '',
    year: vehicle?.year ? String(vehicle.year) : '',
    payloadKg: vehicle?.payloadKg ? String(vehicle.payloadKg) : '',
    fuelType: vehicle?.fuelType ?? 'Diesel',
    route: vehicle?.route ?? '',
    driverName: vehicle?.driverName ?? '',
    driverPhone: vehicle?.driverPhone ?? '',
    insuranceExpiresOn: dateInput(detail?.insuranceExpiresOn),
    inspectionExpiresOn: dateInput(detail?.inspectionExpiresOn),
    speedGovernorExpiresOn: dateInput(detail?.speedGovernorExpiresOn),
    trackerDeviceId: vehicle?.trackerDeviceId ?? '',
    odometerKm: '',
    trackEngineHours: false,
  })
  const set = (key: keyof typeof form, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }))
  const submit = async () => {
    const payload: SaveVehiclePayload = {
      registrationNumber: form.registrationNumber, vehicleType: form.vehicleType, orgNodeId: form.orgNodeId || null, make: text(form.make), model: text(form.model),
      year: num(form.year), payloadKg: num(form.payloadKg), fuelType: text(form.fuelType), route: text(form.route), driverName: text(form.driverName),
      driverPhone: text(form.driverPhone), insuranceExpiresOn: form.insuranceExpiresOn || null, inspectionExpiresOn: form.inspectionExpiresOn || null,
      speedGovernorExpiresOn: form.speedGovernorExpiresOn || null, trackerDeviceId: text(form.trackerDeviceId),
      ...(vehicle ? {} : { odometerKm: num(form.odometerKm), trackEngineHours: form.trackEngineHours }),
    }
    try {
      await save.mutateAsync({ assetId: vehicle?.assetId, payload })
      toast.success(vehicle ? 'Vehicle saved' : 'Vehicle added')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open title={vehicle ? `Edit ${vehicle.registrationNumber}` : 'Add vehicle'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!form.registrationNumber.trim() || (!vehicle && !form.orgNodeId) || save.isPending} onClick={() => void submit()}>{vehicle ? 'Save' : 'Add vehicle'}</button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Registration" required><input className={inputClass} value={form.registrationNumber} onChange={(e) => set('registrationNumber', e.target.value)} placeholder="KCA 123A" /></Field>
        <Field label="Type">
          <select className={inputClass} value={form.vehicleType} onChange={(e) => set('vehicleType', e.target.value)}>{VEHICLE_TYPES.map((type) => <option key={type}>{type}</option>)}</select>
        </Field>
        {vehicle ? null : <Field label="Factory" required className="sm:col-span-2"><LocationSelect value={form.orgNodeId} onChange={(value) => set('orgNodeId', value)} /></Field>}
        <Field label="Make"><input className={inputClass} value={form.make} onChange={(e) => set('make', e.target.value)} placeholder="Isuzu" /></Field>
        <Field label="Model"><input className={inputClass} value={form.model} onChange={(e) => set('model', e.target.value)} placeholder="FVR" /></Field>
        <Field label="Year"><input type="number" className={inputClass} value={form.year} onChange={(e) => set('year', e.target.value)} /></Field>
        <Field label="Leaf payload (kg)"><input type="number" className={inputClass} value={form.payloadKg} onChange={(e) => set('payloadKg', e.target.value)} /></Field>
        <Field label="Route or buying centres" className="sm:col-span-2"><input className={inputClass} value={form.route} onChange={(e) => set('route', e.target.value)} placeholder="Kapkatet, Chebown, Kiptere" /></Field>
        <Field label="Driver"><input className={inputClass} value={form.driverName} onChange={(e) => set('driverName', e.target.value)} /></Field>
        <Field label="Driver's phone"><input className={inputClass} value={form.driverPhone} onChange={(e) => set('driverPhone', e.target.value)} /></Field>
        <Field label="Insurance ends"><input type="date" className={inputClass} value={form.insuranceExpiresOn} onChange={(e) => set('insuranceExpiresOn', e.target.value)} /></Field>
        <Field label="Inspection ends"><input type="date" className={inputClass} value={form.inspectionExpiresOn} onChange={(e) => set('inspectionExpiresOn', e.target.value)} /></Field>
        <Field label="Speed governor certificate ends"><input type="date" className={inputClass} value={form.speedGovernorExpiresOn} onChange={(e) => set('speedGovernorExpiresOn', e.target.value)} /></Field>
        <Field label="Tracker device id" hint="The IMEI or the tracking platform's unique id."><input className={inputClass} value={form.trackerDeviceId} onChange={(e) => set('trackerDeviceId', e.target.value)} /></Field>
        {vehicle ? null : (
          <>
            <Field label="Odometer now (km)"><input type="number" className={inputClass} value={form.odometerKm} onChange={(e) => set('odometerKm', e.target.value)} /></Field>
            <label className="inline-flex items-center gap-2 self-end pb-2 text-sm text-slate-700"><input type="checkbox" checked={form.trackEngineHours} onChange={(e) => set('trackEngineHours', e.target.checked)} />Also track engine hours</label>
          </>
        )}
      </div>
    </FormDrawer>
  )
}

function VehicleDetailDrawer({ assetId, onClose, onEdit }: { assetId: string; onClose: () => void; onEdit: ((vehicle: FleetVehicle) => void) | null }) {
  const { data, isLoading } = useVehicle(assetId)
  const canFuel = useAnyPermission('assets.manage', 'work-orders.complete')
  const links = useFactoryLinks()
  return (
    <FormDrawer open title={data ? `${data.vehicle.registrationNumber} · ${data.vehicle.assetName}` : 'Vehicle'} onClose={onClose}>
      {isLoading || !data ? <Spinner label="Loading vehicle" /> : (
        <div className="space-y-5 text-sm">
          <div className="flex flex-wrap gap-2">
            <Link to={links.asset(assetId)} className={secondaryButton}>Asset, jobs and PM</Link>
            <Link to={links.asset(assetId, 'meters')} className={secondaryButton}>Meters</Link>
            {onEdit ? <button type="button" className={secondaryButton} onClick={() => onEdit(data.vehicle)}>Edit</button> : null}
          </div>
          <dl className="grid grid-cols-2 gap-3">
            <div><dt className="text-xs text-slate-500">Odometer</dt><dd className="font-medium">{formatNumber(data.vehicle.odometerKm, 'km')}</dd></div>
            <div><dt className="text-xs text-slate-500">Next service</dt><dd className="font-medium">{data.vehicle.kmToService !== null ? `in ${formatNumber(data.vehicle.kmToService, 'km')}` : data.vehicle.serviceDueOn ? formatDate(data.vehicle.serviceDueOn) : 'No plan'}</dd></div>
            {data.vehicle.documents.map((doc) => (
              <div key={doc.type}><dt className="text-xs text-slate-500">{doc.label}</dt><dd className={cn('font-medium', doc.daysLeft <= 30 && 'text-amber-700')}>{formatDate(doc.expiresOn)}</dd></div>
            ))}
          </dl>
          {canFuel ? <FuelForm assetId={assetId} /> : null}
          <div>
            <h3 className="mb-2 font-semibold text-slate-900">Fill-ups</h3>
            {data.fuelLogs.length === 0 ? <p className="text-slate-500">None logged yet.</p> : (
              <table className="w-full">
                <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">When</th><th className="text-right">Litres</th><th className="text-right">Odometer</th><th>Where</th></tr></thead>
                <tbody className="divide-y divide-slate-100">
                  {data.fuelLogs.map((log) => (
                    <tr key={log.id}>
                      <td className="py-1.5">{formatDateTime(log.filledAt)}{log.fullTank ? '' : <span className="ml-1 text-xs text-slate-500">part</span>}</td>
                      <td className="text-right tabular-nums">{formatNumber(log.litres)}</td>
                      <td className="text-right tabular-nums">{formatNumber(log.odometerKm, 'km')}</td>
                      <td className="text-slate-600">{log.station ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </FormDrawer>
  )
}

function FuelForm({ assetId }: { assetId: string }) {
  const add = useAddFuel(assetId)
  const [form, setForm] = useState({ litres: '', odometerKm: '', cost: '', station: '', fullTank: true })
  const submit = async () => {
    try {
      await add.mutateAsync({ filledAt: null, litres: Number(form.litres), cost: num(form.cost), odometerKm: num(form.odometerKm), fullTank: form.fullTank, station: text(form.station) })
      toast.success('Fill-up logged')
      setForm({ litres: '', odometerKm: '', cost: '', station: '', fullTank: true })
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="grid gap-3 rounded-lg bg-slate-50 p-3 sm:grid-cols-2">
      <h3 className="font-semibold text-slate-900 sm:col-span-2">Log a fill-up</h3>
      <Field label="Litres" required><input type="number" inputMode="decimal" className={inputClass} value={form.litres} onChange={(e) => setForm({ ...form, litres: e.target.value })} /></Field>
      <Field label="Odometer (km)"><input type="number" inputMode="decimal" className={inputClass} value={form.odometerKm} onChange={(e) => setForm({ ...form, odometerKm: e.target.value })} /></Field>
      <Field label="Cost"><input type="number" inputMode="decimal" className={inputClass} value={form.cost} onChange={(e) => setForm({ ...form, cost: e.target.value })} /></Field>
      <Field label="Station"><input className={inputClass} value={form.station} onChange={(e) => setForm({ ...form, station: e.target.value })} /></Field>
      <label className="inline-flex items-center gap-2 text-slate-700"><input type="checkbox" checked={form.fullTank} onChange={(e) => setForm({ ...form, fullTank: e.target.checked })} />Filled to the top</label>
      <button type="button" className={primaryButton} disabled={!form.litres || add.isPending} onClick={() => void submit()}><Fuel className="h-4 w-4" />Log</button>
    </div>
  )
}

function TrackingCard() {
  const { data, isLoading } = useTelematics(true)
  const save = useSaveTelematics()
  const sync = useSyncTelematics()
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState({ baseUrl: '', username: '', password: '', isEnabled: true })
  const startEdit = () => {
    setForm({ baseUrl: data?.baseUrl ?? '', username: data?.username ?? '', password: '', isEnabled: data?.isEnabled ?? true })
    setEditing(true)
  }
  const submit = async () => {
    try {
      await save.mutateAsync({ provider: 'Traccar', baseUrl: form.baseUrl, username: text(form.username), password: form.password || null, isEnabled: form.isEnabled })
      toast.success('Tracking link saved')
      setEditing(false)
    } catch {
      // The API client shows the error.
    }
  }
  const syncNow = async () => {
    try {
      const result = await sync.mutateAsync()
      toast.success(`Updated ${result.applied.vehicles} vehicle${result.applied.vehicles === 1 ? '' : 's'} from ${result.fetched} position${result.fetched === 1 ? '' : 's'}`)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-center gap-2">
        <Satellite className="h-5 w-5 text-slate-400" />
        <h2 className="font-semibold text-slate-900">Vehicle tracking</h2>
        {data ? <Badge tone={data.isEnabled ? 'success' : 'neutral'} size="sm">{data.isEnabled ? 'Linked' : 'Off'}</Badge> : null}
        <div className="ml-auto flex gap-2">
          {data ? <button type="button" className={secondaryButton} disabled={sync.isPending} onClick={() => void syncNow()}><RefreshCw className="h-4 w-4" />Update now</button> : null}
          <button type="button" className={secondaryButton} onClick={startEdit}>{data ? 'Change' : 'Link Traccar'}</button>
        </div>
      </div>
      {isLoading ? null : data ? (
        <p className="text-sm text-slate-600">
          {data.provider} at {data.baseUrl}, checked every 10 minutes. {data.lastSyncAt ? `Last update ${formatDateTime(data.lastSyncAt)}: ${data.lastSyncVehicles} vehicle${data.lastSyncVehicles === 1 ? '' : 's'}.` : 'Not updated yet.'}
          {data.lastSyncError ? <span className="block text-amber-700">{data.lastSyncError}</span> : null}
        </p>
      ) : (
        <p className="text-sm text-slate-600">Link a Traccar server to pull positions, odometer and engine hours. Any other tracking platform can push them instead:
          add a gateway under <Link to="/settings/sensors" className="text-[var(--app-primary)] hover:underline">Settings → Sensors</Link> and POST to <code className="rounded bg-slate-100 px-1 text-xs">/api/fleet/positions</code> with its key,
          e.g. <code className="rounded bg-slate-100 px-1 text-xs">{'{"positions":[{"deviceId":"359632…","latitude":-0.37,"longitude":35.28,"odometerKm":152340}]}'}</code>.</p>
      )}
      {editing ? (
        <div className="grid gap-3 rounded-lg bg-slate-50 p-3 sm:grid-cols-2">
          <Field label="Traccar address" required className="sm:col-span-2"><input className={inputClass} value={form.baseUrl} onChange={(e) => setForm({ ...form, baseUrl: e.target.value })} placeholder="https://tracking.example.co.ke" /></Field>
          <Field label="User"><input className={inputClass} value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} /></Field>
          <Field label="Password" hint={data?.hasPassword ? 'Blank keeps the saved one.' : undefined}><input type="password" className={inputClass} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} autoComplete="new-password" /></Field>
          <label className="inline-flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={form.isEnabled} onChange={(e) => setForm({ ...form, isEnabled: e.target.checked })} />Check every 10 minutes</label>
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButton} onClick={() => setEditing(false)}>Cancel</button>
            <button type="button" className={primaryButton} disabled={!form.baseUrl.trim() || save.isPending} onClick={() => void submit()}>Save</button>
          </div>
        </div>
      ) : null}
    </section>
  )
}
