import { useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { MapContainer, Marker, Polyline, Popup, TileLayer } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { Radio, Route as RouteIcon } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { cn } from '../../shared/utils/cn'
import { useTechnician } from '../team/api'
import { WorkOrderDispatchDrawer } from '../work-orders/components/WorkOrderDispatchDrawer'
import { useOptimisedRoute, type RouteStop } from './api'
import { routeStopIcon } from './mapIcons'
import { formatTime } from '../../shared/utils/dates'

const NAIROBI_CENTER: [number, number] = [-1.286389, 36.817223]

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function formatScheduledTime(value: string | null) {
  if (!value) {
    return 'Unscheduled'
  }
  return formatTime(value)
}

export function DailyRouteView() {
  const { technicianId } = useParams<{ technicianId: string }>()
  const [date, setDate] = useState(todayIso())
  // `RouteStopResponse` is a scheduling projection (id, number, client, address, coordinates) with
  // no assignment state on it, so the drawer re-reads the work order to decide what is legal.
  const [dispatchWorkOrderId, setDispatchWorkOrderId] = useState<string | null>(null)

  const { data: technician } = useTechnician(technicianId)
  const { data: stops, isLoading, isFetching, refetch } = useOptimisedRoute(technicianId, date)

  const polylinePositions = useMemo<[number, number][]>(
    () => (stops ?? []).map((stop) => [stop.lat, stop.lng]),
    [stops],
  )

  const mapCenter: [number, number] = polylinePositions[0] ?? NAIROBI_CENTER

  return (
    <div className="flex h-full flex-col gap-4">
      <PageHeader
        title="Daily Route"
        subtitle={technician ? `Optimised visit order for ${technician.fullName}.` : 'Optimised visit order for the selected technician.'}
        breadcrumbs={[{ label: 'Dispatch', to: '/dispatch/map' }, { label: 'Daily Route' }]}
        secondaryActions={
          <div className="flex items-center gap-2">
            <input
              type="date"
              value={date}
              onChange={(event) => setDate(event.target.value)}
              className="h-10 rounded-control border border-slate-200 px-3 text-sm text-slate-700 shadow-sm focus:border-primary focus:outline-none"
            />
            <button
              type="button"
              onClick={() => void refetch()}
              className="inline-flex items-center gap-2 rounded-control bg-primary px-3 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-primary-light disabled:opacity-60"
              disabled={!technicianId}
            >
              <RouteIcon className={cn('h-4 w-4', isFetching && 'animate-pulse')} />
              Optimise Route
            </button>
          </div>
        }
      />

      {isLoading ? (
        <Spinner label="Calculating optimal route" />
      ) : !stops || stops.length === 0 ? (
        <EmptyState
          icon={RouteIcon}
          title="No work orders scheduled"
          description={`No work orders are scheduled for this technician on ${date}.`}
        />
      ) : (
        <div className="flex min-h-0 flex-1 flex-col gap-4 lg:flex-row">
          <div className="h-[320px] flex-1 overflow-hidden rounded-app border border-slate-200 shadow-card lg:h-auto">
            <MapContainer center={mapCenter} zoom={12} className="h-full w-full">
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              <Polyline positions={polylinePositions} pathOptions={{ color: '#15806A', weight: 3, dashArray: '6 6' }} />
              {stops.map((stop, index) => (
                <Marker key={stop.id} position={[stop.lat, stop.lng]} icon={routeStopIcon(index + 1)}>
                  <Popup>
                    <StopPopupContent stop={stop} order={index + 1} />
                  </Popup>
                </Marker>
              ))}
            </MapContainer>
          </div>

          <aside className="flex w-full flex-col rounded-app border border-slate-200 bg-white shadow-card lg:w-96">
            <div className="border-b border-slate-100 px-4 py-3">
              <h2 className="text-sm font-semibold text-slate-900">Stops ({stops.length})</h2>
            </div>
            <ol className="flex-1 divide-y divide-slate-100 overflow-y-auto">
              {stops.map((stop, index) => (
                <li key={stop.id} className="flex gap-3 px-4 py-3">
                  <span className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-white">
                    {index + 1}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-2">
                      <span className="truncate text-sm font-medium text-slate-900">{stop.clientName}</span>
                      <span className="text-xs text-slate-400">{formatScheduledTime(stop.scheduledTime)}</span>
                    </div>
                    <p className="truncate text-xs text-slate-500">WO {stop.woNumber}</p>
                    <p className="truncate text-xs text-slate-500">{stop.siteAddress}</p>
                    {index < stops.length - 1 ? (
                      <p className="mt-1 text-xs font-medium text-accent">
                        ~{stop.estimatedTravelMins} min to next stop
                      </p>
                    ) : null}
                    <button
                      type="button"
                      onClick={() => setDispatchWorkOrderId(stop.id)}
                      className="mt-2 inline-flex h-7 items-center gap-1 rounded-md border border-[var(--app-border)] bg-white px-2 text-[11px] font-medium text-slate-600 transition hover:bg-[var(--app-primary-soft)] hover:text-slate-900"
                    >
                      <Radio className="h-3 w-3" />
                      Dispatch
                    </button>
                  </div>
                </li>
              ))}
            </ol>
          </aside>
        </div>
      )}

      <WorkOrderDispatchDrawer workOrderId={dispatchWorkOrderId} onClose={() => setDispatchWorkOrderId(null)} />
    </div>
  )
}

function StopPopupContent({ stop, order }: { stop: RouteStop; order: number }) {
  return (
    <div className="space-y-1 text-sm">
      <p className="font-semibold text-slate-900">
        Stop {order}: {stop.clientName}
      </p>
      <p className="text-slate-600">WO {stop.woNumber}</p>
      <p className="text-slate-600">{stop.siteAddress}</p>
      <p className="text-xs text-slate-400">Scheduled {formatScheduledTime(stop.scheduledTime)}</p>
    </div>
  )
}
