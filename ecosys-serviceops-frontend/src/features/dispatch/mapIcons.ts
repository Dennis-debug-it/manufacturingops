import L from 'leaflet'
import type { TechnicianLiveStatus } from './api'

const statusColors: Record<TechnicianLiveStatus, string> = {
  available: '#27ae60',
  'on-job': '#2e86ab',
  offline: '#85929e',
}

export const statusLabels: Record<TechnicianLiveStatus, string> = {
  available: 'Available',
  'on-job': 'On Job',
  offline: 'Offline',
}

const iconCache = new Map<string, L.DivIcon>()

export function technicianPinIcon(status: TechnicianLiveStatus): L.DivIcon {
  const cached = iconCache.get(status)
  if (cached) {
    return cached
  }

  const color = statusColors[status]
  const icon = L.divIcon({
    className: 'ecosys-technician-pin',
    html: `<span style="display:block;width:18px;height:18px;border-radius:9999px;background:${color};border:3px solid white;box-shadow:0 1px 4px rgba(15,23,42,0.4);"></span>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
    popupAnchor: [0, -9],
  })
  iconCache.set(status, icon)
  return icon
}

const stopIconCache = new Map<number, L.DivIcon>()

export function routeStopIcon(order: number): L.DivIcon {
  const cached = stopIconCache.get(order)
  if (cached) {
    return cached
  }

  const icon = L.divIcon({
    className: 'ecosys-route-stop-pin',
    html: `<span style="display:flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:9999px;background:#15806A;color:white;font-size:12px;font-weight:600;border:2px solid white;box-shadow:0 1px 4px rgba(15,23,42,0.4);">${order}</span>`,
    iconSize: [26, 26],
    iconAnchor: [13, 13],
    popupAnchor: [0, -13],
  })
  stopIconCache.set(order, icon)
  return icon
}
