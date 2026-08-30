import { useCallback, useEffect, useMemo, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { MapContainer, TileLayer, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import 'leaflet.markercluster/dist/MarkerCluster.css'
import 'leaflet.markercluster/dist/MarkerCluster.Default.css'
import L from 'leaflet'
import 'leaflet.markercluster'
import { EmptyState } from '../../../shared/components/EmptyState'
import type { WorkOrderRow } from '../listModel'

const NAIROBI_CENTER: [number, number] = [-1.286389, 36.817223]

const statusColors: Record<string, string> = {
  New: '#e74c3c',
  Assigned: '#2e86ab',
  'In Progress': '#f39c12',
  'On Hold': '#8e44ad',
  Completed: '#27ae60',
  Closed: '#64748b',
}

const iconCache = new Map<string, L.DivIcon>()
function statusPinIcon(status: string) {
  const cached = iconCache.get(status)
  if (cached) {
    return cached
  }
  const color = statusColors[status] ?? '#64748b'
  const icon = L.divIcon({
    className: 'ecosys-wo-pin',
    html: `<span style="display:block;width:18px;height:18px;border-radius:9999px;background:${color};border:3px solid white;box-shadow:0 1px 4px rgba(15,23,42,0.4);"></span>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
    popupAnchor: [0, -9],
  })
  iconCache.set(status, icon)
  return icon
}

function ClusterLayer({ rows, onOpen }: { rows: WorkOrderRow[]; onOpen: (id: string) => void }) {
  const map = useMap()
  const groupRef = useRef<L.MarkerClusterGroup | null>(null)

  useEffect(() => {
    const group = L.markerClusterGroup()
    groupRef.current = group
    map.addLayer(group)
    return () => {
      map.removeLayer(group)
      groupRef.current = null
    }
  }, [map])

  useEffect(() => {
    const group = groupRef.current
    if (!group) {
      return
    }
    group.clearLayers()

    rows.forEach((row) => {
      if (row.latitude == null || row.longitude == null) {
        return
      }
      const marker = L.marker([row.latitude, row.longitude], { icon: statusPinIcon(row.status) })

      const container = document.createElement('div')
      container.className = 'space-y-1 text-sm'

      const number = document.createElement('p')
      number.className = 'font-semibold text-slate-900'
      number.textContent = row.woNo
      container.appendChild(number)

      const client = document.createElement('p')
      client.className = 'text-slate-600'
      client.textContent = row.client
      container.appendChild(client)

      const site = document.createElement('p')
      site.className = 'text-xs text-slate-500'
      site.textContent = row.site
      container.appendChild(site)

      const status = document.createElement('p')
      status.className = 'text-xs text-slate-500'
      status.textContent = row.status
      container.appendChild(status)

      const button = document.createElement('button')
      button.type = 'button'
      button.textContent = 'Open'
      button.className = 'mt-2 inline-flex h-8 items-center rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white'
      button.addEventListener('click', () => onOpen(row.id))
      container.appendChild(button)

      marker.bindPopup(container)
      group.addLayer(marker)
    })
  }, [onOpen, rows])

  return null
}

export interface MapViewProps {
  rows: WorkOrderRow[]
}

export function MapView({ rows }: MapViewProps) {
  const navigate = useNavigate()
  const located = useMemo(() => rows.filter((row) => row.latitude != null && row.longitude != null), [rows])
  const openWorkOrder = useCallback((id: string) => navigate(`/work-orders/${id}`), [navigate])

  if (rows.length === 0) {
    return <EmptyState title="No work orders" description="Once work orders match the current filters, they'll appear on the map." />
  }

  return (
    <div className="space-y-3">
      {located.length < rows.length ? (
        <p className="text-xs text-slate-500">
          {rows.length - located.length} work order(s) without a site location aren't shown on the map.
        </p>
      ) : null}
      <div className="h-[calc(100vh-320px)] min-h-[420px] overflow-hidden rounded-2xl border border-slate-200 shadow-sm">
        <MapContainer center={NAIROBI_CENTER} zoom={11} className="h-full w-full">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <ClusterLayer rows={located} onOpen={openWorkOrder} />
        </MapContainer>
      </div>
      <div className="flex flex-wrap gap-3 text-xs text-slate-500">
        {Object.entries(statusColors).map(([status, color]) => (
          <span key={status} className="inline-flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
            {status}
          </span>
        ))}
      </div>
    </div>
  )
}
