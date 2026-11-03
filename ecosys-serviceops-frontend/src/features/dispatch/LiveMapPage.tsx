import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { format } from 'date-fns'
import { RefreshCw, Route as RouteIcon } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { cn } from '../../shared/utils/cn'
import { useTechnicianLocations, type TechnicianLocation, type TechnicianLiveStatus } from './api'
import { statusLabels, technicianPinIcon } from './mapIcons'

const NAIROBI_CENTER: [number, number] = [-1.286389, 36.817223]

const statusDotClasses: Record<TechnicianLiveStatus, string> = {
  available: 'bg-emerald-500',
  'on-job': 'bg-sky-500',
  offline: 'bg-slate-400',
}

function FlyToTechnician({ position }: { position: [number, number] | null }) {
  const map = useMap()

  useEffect(() => {
    if (position) {
      map.flyTo(position, Math.max(map.getZoom(), 14), { duration: 0.75 })
    }
  }, [position, map])

  return null
}

function formatLastUpdated(value: string | null) {
  if (!value) {
    return 'No location reported'
  }
  return format(new Date(value), 'MMM d, HH:mm')
}

export function LiveMapPage() {
  const { data: technicians, isLoading, isFetching, refetch } = useTechnicianLocations()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const located = useMemo(() => (technicians ?? []).filter((t) => t.latitude != null && t.longitude != null), [technicians])
  const selected = located.find((t) => t.id === selectedId) ?? null
  const selectedPosition: [number, number] | null = selected ? [selected.latitude!, selected.longitude!] : null

  return (
    <div className="flex h-full flex-col gap-4">
      <PageHeader
        title="Live Technician Map"
        subtitle="Real-time positions for every active technician, refreshed automatically every 30 seconds."
        secondaryActions={
          <button
            type="button"
            onClick={() => void refetch()}
            className="inline-flex items-center gap-2 rounded-control border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm transition hover:bg-slate-50"
          >
            <RefreshCw className={cn('h-4 w-4', isFetching && 'animate-spin')} />
            Refresh
          </button>
        }
      />

      {isLoading ? (
        <Spinner label="Loading technician locations" />
      ) : (
        <div className="flex min-h-0 flex-1 flex-col gap-4 lg:flex-row">
          <div className="h-[calc(100vh-260px)] min-h-[480px] flex-1 overflow-hidden rounded-app border border-slate-200 shadow-card">
            <MapContainer center={NAIROBI_CENTER} zoom={12} className="h-full w-full">
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                // The site sends no referrer (nginx Referrer-Policy); OpenStreetMap refuses tile
                // requests without one, so the tiles alone say which site they are for.
                referrerPolicy="strict-origin-when-cross-origin"
              />
              <FlyToTechnician position={selectedPosition} />
              {located.map((technician) => (
                <Marker
                  key={technician.id}
                  position={[technician.latitude!, technician.longitude!]}
                  icon={technicianPinIcon(technician.status)}
                  eventHandlers={{ click: () => setSelectedId(technician.id) }}
                >
                  <Popup>
                    <TechnicianPopupContent technician={technician} />
                  </Popup>
                </Marker>
              ))}
            </MapContainer>
          </div>

          <aside className="flex max-h-[28rem] w-full flex-col rounded-app border border-slate-200 bg-white shadow-card lg:h-[calc(100vh-260px)] lg:max-h-none lg:min-h-[480px] lg:w-80">
            <div className="border-b border-slate-100 px-4 py-3">
              <h2 className="text-sm font-semibold text-slate-900">Technicians ({technicians?.length ?? 0})</h2>
              <div className="mt-2 flex flex-wrap gap-3 text-xs text-slate-500">
                {(Object.keys(statusLabels) as TechnicianLiveStatus[]).map((status) => (
                  <span key={status} className="inline-flex items-center gap-1.5">
                    <span className={cn('h-2.5 w-2.5 rounded-full', statusDotClasses[status])} />
                    {statusLabels[status]}
                  </span>
                ))}
              </div>
            </div>

            <div className="min-h-0 flex-1 overflow-y-auto">
              {!technicians || technicians.length === 0 ? (
                <EmptyState title="No technicians found" description="Active technicians will appear here once available." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {technicians.map((technician) => (
                    <li key={technician.id}>
                      <button
                        type="button"
                        onClick={() => setSelectedId(technician.id)}
                        disabled={technician.latitude == null || technician.longitude == null}
                        className={cn(
                          'flex w-full flex-col gap-1 px-4 py-3 text-left transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60',
                          selectedId === technician.id && 'bg-primary-light/10',
                        )}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <span className="text-sm font-medium text-slate-900">{technician.fullName}</span>
                          <span className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-600">
                            <span className={cn('h-2 w-2 rounded-full', statusDotClasses[technician.status])} />
                            {statusLabels[technician.status]}
                          </span>
                        </div>
                        {technician.currentWorkOrderNumber ? (
                          <span className="text-xs text-slate-500">WO {technician.currentWorkOrderNumber}</span>
                        ) : null}
                        <span className="text-xs text-slate-400">{formatLastUpdated(technician.lastLocationAt)}</span>
                      </button>
                      <Link
                        to={`/dispatch/route/${technician.id}`}
                        className="mx-4 mb-2 inline-flex items-center gap-1.5 text-xs font-medium text-accent hover:underline"
                      >
                        <RouteIcon className="h-3 w-3" />
                        View daily route
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </aside>
        </div>
      )}
    </div>
  )
}

function TechnicianPopupContent({ technician }: { technician: TechnicianLocation }) {
  return (
    <div className="space-y-1 text-sm">
      <p className="font-semibold text-slate-900">{technician.fullName}</p>
      <p className="text-slate-600">{statusLabels[technician.status]}</p>
      {technician.currentWorkOrderNumber ? <p className="text-slate-600">WO {technician.currentWorkOrderNumber}</p> : null}
      <p className="text-xs text-slate-400">Updated {formatLastUpdated(technician.lastLocationAt)}</p>
    </div>
  )
}
