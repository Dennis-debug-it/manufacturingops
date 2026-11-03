import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Popup } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { useTechnicianLocations } from '../../dispatch/api'
import { technicianPinIcon } from '../../dispatch/mapIcons'
import type { WorkOrder } from '../types'

const siteIcon = L.divIcon({
  className: 'ecosys-site-pin',
  html: '<span style="display:flex;align-items:center;justify-content:center;width:20px;height:20px;border-radius:9999px;background:#0f172a;border:3px solid white;box-shadow:0 1px 4px rgba(15,23,42,0.4);"></span>',
  iconSize: [20, 20],
  iconAnchor: [10, 10],
})

const haversineKm = (a: [number, number], b: [number, number]) => {
  const toRad = (value: number) => (value * Math.PI) / 180
  const earthRadiusKm = 6371
  const dLat = toRad(b[0] - a[0])
  const dLng = toRad(b[1] - a[1])
  const lat1 = toRad(a[0])
  const lat2 = toRad(b[0])
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLng / 2) ** 2
  return earthRadiusKm * 2 * Math.asin(Math.sqrt(h))
}

const AVERAGE_SPEED_KMH = 30

export function TechnicianLocationPanel({ workOrder }: { workOrder: WorkOrder }) {
  const { data: technicians } = useTechnicianLocations()

  const technician = useMemo(
    () => (technicians ?? []).find((t) => t.id === workOrder.assignedTechnicianId),
    [technicians, workOrder.assignedTechnicianId],
  )

  const isActive = workOrder.status === 'In Progress' && Boolean(workOrder.assignedTechnicianId)
  const techPosition: [number, number] | null =
    technician?.latitude != null && technician?.longitude != null ? [technician.latitude, technician.longitude] : null
  const sitePosition: [number, number] | null =
    workOrder.siteLatitude != null && workOrder.siteLongitude != null ? [workOrder.siteLatitude, workOrder.siteLongitude] : null

  if (!isActive || !techPosition) {
    return null
  }

  const distanceKm = sitePosition ? haversineKm(techPosition, sitePosition) : null
  const etaMinutes = distanceKm != null ? Math.max(1, Math.round((distanceKm / AVERAGE_SPEED_KMH) * 60)) : null
  const center = sitePosition ?? techPosition
  const bounds: [number, number][] = sitePosition ? [techPosition, sitePosition] : [techPosition]

  return (
    <div className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
      <div className="flex items-center justify-between px-4 pt-4">
        <h3 className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Technician Location</h3>
      </div>
      <div style={{ height: 200 }} className="mt-3">
        <MapContainer
          center={center}
          zoom={13}
          style={{ height: '100%', width: '100%' }}
          scrollWheelZoom={false}
          {...(bounds.length > 1 ? { bounds } : {})}
        >
          <TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                // The site sends no referrer (nginx Referrer-Policy); OpenStreetMap refuses tile
                // requests without one, so the tiles alone say which site they are for.
                referrerPolicy="strict-origin-when-cross-origin" />
          <Marker position={techPosition} icon={technicianPinIcon(technician?.status ?? 'on-job')}>
            <Popup>{technician?.fullName ?? 'Technician'}</Popup>
          </Marker>
          {sitePosition ? (
            <>
              <Marker position={sitePosition} icon={siteIcon}>
                <Popup>{workOrder.siteName ?? 'Site'}</Popup>
              </Marker>
              <Polyline positions={[techPosition, sitePosition]} pathOptions={{ color: '#0f172a', weight: 2, dashArray: '4 6' }} />
            </>
          ) : null}
        </MapContainer>
      </div>
      <div className="px-4 py-3 text-xs text-slate-600">
        {distanceKm != null ? (
          <span>{distanceKm.toFixed(1)} km away, ~{etaMinutes} min ETA</span>
        ) : (
          <span>Site location not available</span>
        )}
      </div>
    </div>
  )
}
