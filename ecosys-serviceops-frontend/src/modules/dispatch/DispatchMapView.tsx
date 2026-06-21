import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { MapContainer, TileLayer, Marker, Popup, CircleMarker } from 'react-leaflet'
import { useMemo } from 'react'
import type { TechnicianRecord, WorkOrder } from '../../types/api'
import { Link } from 'react-router-dom'

// ─────────────────────────────────────────────────────────────────────────────
// Fix Leaflet's broken default icon paths when bundled with Vite
// ─────────────────────────────────────────────────────────────────────────────
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

export interface DispatchMapViewProps {
  technicians: TechnicianRecord[]
  workOrders: WorkOrder[]
  onTechnicianClick: (tech: TechnicianRecord) => void
  onWorkOrderClick: (wo: WorkOrder) => void
}

type TechStatus = 'Available' | 'On Job' | 'Travelling' | 'Off Shift'

function deriveTechStatus(record: TechnicianRecord): TechStatus {
  if (!record.isActive) return 'Off Shift'
  if (record.activeWorkOrderId) return 'On Job'
  if (record.isTrackingActive) return 'Travelling'
  return 'Available'
}

// ─────────────────────────────────────────────────────────────────────────────
// Colour helpers
// ─────────────────────────────────────────────────────────────────────────────

const STATUS_COLOUR: Record<TechStatus, string> = {
  Available: '#10b981',   // emerald-500
  'On Job': '#f59e0b',    // amber-500
  Travelling: '#0ea5e9',  // sky-500
  'Off Shift': '#94a3b8', // slate-400
}

const PRIORITY_COLOUR: Record<string, string> = {
  critical: '#ef4444', // rose-500
  high: '#f97316',     // orange-500
  medium: '#3b82f6',   // blue-500
  low: '#94a3b8',      // slate-400
}

function priorityColour(priority: string | null | undefined): string {
  return PRIORITY_COLOUR[(priority ?? '').toLowerCase()] ?? PRIORITY_COLOUR.low
}

// ─────────────────────────────────────────────────────────────────────────────
// Custom coloured SVG icons for technicians
// ─────────────────────────────────────────────────────────────────────────────

function makeTechIcon(colour: string, initials: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `
      <div style="
        width:36px;height:36px;border-radius:50%;
        background:${colour};border:3px solid white;
        box-shadow:0 2px 6px rgba(0,0,0,0.35);
        display:flex;align-items:center;justify-content:center;
        font-size:11px;font-weight:700;color:white;
        font-family:system-ui,sans-serif;
      ">${initials}</div>`,
    iconSize: [36, 36],
    iconAnchor: [18, 18],
    popupAnchor: [0, -20],
  })
}

function makeWOIcon(colour: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `
      <div style="
        width:22px;height:22px;border-radius:4px;
        background:${colour};border:2px solid white;
        box-shadow:0 2px 5px rgba(0,0,0,0.3);
        display:flex;align-items:center;justify-content:center;
      ">
        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5"
          stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/>
          <circle cx="12" cy="10" r="3"/>
        </svg>
      </div>`,
    iconSize: [22, 28],
    iconAnchor: [11, 28],
    popupAnchor: [0, -30],
  })
}

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return name.slice(0, 2).toUpperCase()
}

// ─────────────────────────────────────────────────────────────────────────────
// Kenya map bounds / centre
// ─────────────────────────────────────────────────────────────────────────────

const KENYA_CENTER: [number, number] = [-1.286389, 36.817223] // Nairobi
const KENYA_ZOOM = 7

// Fallback cluster positions around Nairobi for WOs without GPS — spread them
// out by index so they don't all land on the same pixel.
function nairobiCluster(idx: number): [number, number] {
  const ring = Math.floor(idx / 8)
  const angle = (idx % 8) * (Math.PI / 4)
  const radius = 0.08 + ring * 0.06
  return [
    KENYA_CENTER[0] + Math.cos(angle) * radius,
    KENYA_CENTER[1] + Math.sin(angle) * radius,
  ]
}

// ─────────────────────────────────────────────────────────────────────────────
// Map component
// ─────────────────────────────────────────────────────────────────────────────

export function DispatchMapView({
  technicians,
  workOrders,
  onTechnicianClick,
  onWorkOrderClick,
}: DispatchMapViewProps) {
  // Only show unassigned WOs on the map (assigned ones are on a tech)
  const unassignedWOs = useMemo(
    () =>
      workOrders.filter((wo) => {
        const closed = ['completed', 'closed', 'cancelled']
        if (closed.includes((wo.status ?? '').toLowerCase())) return false
        return (
          !wo.assignedTechnicianId &&
          !wo.leadTechnicianId &&
          (!wo.technicianAssignments || wo.technicianAssignments.length === 0)
        )
      }),
    [workOrders],
  )

  // Techs with GPS
  const techsWithGPS = useMemo(
    () =>
      technicians.filter(
        (t) =>
          t.lastKnownLatitude != null &&
          t.lastKnownLongitude != null,
      ),
    [technicians],
  )

  const gpsCount = techsWithGPS.length

  return (
    <div className="surface-card rounded-[24px] overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-app px-4 py-3">
        <div className="flex items-center gap-3">
          <h2 className="text-sm font-bold text-app">Live Map</h2>
          <span className="text-[11px] text-muted">
            {gpsCount}/{technicians.length} technicians tracked · {unassignedWOs.length} unassigned pins
          </span>
        </div>
        {/* Legend chips */}
        <div className="hidden sm:flex items-center gap-3 text-[10px] text-muted">
          {[
            { colour: STATUS_COLOUR.Available, label: 'Available' },
            { colour: STATUS_COLOUR['On Job'], label: 'On Job' },
            { colour: STATUS_COLOUR.Travelling, label: 'Travelling' },
            { colour: STATUS_COLOUR['Off Shift'], label: 'Off Shift' },
          ].map(({ colour, label }) => (
            <span key={label} className="flex items-center gap-1">
              <span
                className="inline-block size-2.5 rounded-full border border-white/50 shadow-sm"
                style={{ background: colour }}
              />
              {label}
            </span>
          ))}
          <span className="flex items-center gap-1">
            <span
              className="inline-block size-2.5 rounded"
              style={{ background: priorityColour('high') }}
            />
            WO Pin
          </span>
        </div>
      </div>

      {/* Map */}
      <div style={{ height: 420 }}>
        <MapContainer
          center={KENYA_CENTER}
          zoom={KENYA_ZOOM}
          style={{ height: '100%', width: '100%' }}
          scrollWheelZoom={true}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />

          {/* ── Technician markers ── */}
          {technicians.map((tech) => {
            const status = deriveTechStatus(tech)
            const colour = STATUS_COLOUR[status]
            const initials = getInitials(tech.fullName)
            const hasGPS =
              tech.lastKnownLatitude != null && tech.lastKnownLongitude != null

            if (!hasGPS) return null

            const pos: [number, number] = [
              tech.lastKnownLatitude!,
              tech.lastKnownLongitude!,
            ]

            return (
              <Marker
                key={tech.id}
                position={pos}
                icon={makeTechIcon(colour, initials)}
                eventHandlers={{ click: () => onTechnicianClick(tech) }}
              >
                <Popup>
                  <div style={{ minWidth: 160, fontFamily: 'system-ui, sans-serif' }}>
                    <p style={{ fontWeight: 700, fontSize: 13, marginBottom: 2 }}>
                      {tech.fullName}
                    </p>
                    <p style={{ fontSize: 11, color: colour, fontWeight: 600, marginBottom: 4 }}>
                      {status}
                    </p>
                    {tech.activeWorkOrderId ? (
                      <p style={{ fontSize: 11, color: '#64748b' }}>
                        Job: {tech.activeWorkOrderId.slice(0, 8)}…
                      </p>
                    ) : null}
                    {tech.lastLocationAt ? (
                      <p style={{ fontSize: 10, color: '#94a3b8', marginTop: 4 }}>
                        Updated {new Date(tech.lastLocationAt).toLocaleTimeString()}
                      </p>
                    ) : null}
                    <button
                      onClick={() => onTechnicianClick(tech)}
                      style={{
                        marginTop: 6,
                        fontSize: 11,
                        padding: '3px 10px',
                        background: colour,
                        color: 'white',
                        border: 'none',
                        borderRadius: 12,
                        cursor: 'pointer',
                        fontWeight: 600,
                      }}
                    >
                      View details
                    </button>
                  </div>
                </Popup>
              </Marker>
            )
          })}

          {/* ── Technicians WITHOUT GPS — show pulsing ring at Nairobi cluster ── */}
          {technicians
            .filter(
              (t) =>
                t.lastKnownLatitude == null || t.lastKnownLongitude == null,
            )
            .map((tech, idx) => {
              const status = deriveTechStatus(tech)
              const colour = STATUS_COLOUR[status]
              const pos = nairobiCluster(idx)

              return (
                <CircleMarker
                  key={`no-gps-${tech.id}`}
                  center={pos}
                  radius={6}
                  pathOptions={{
                    fillColor: colour,
                    fillOpacity: 0.5,
                    color: colour,
                    weight: 1,
                    dashArray: '4 3',
                  }}
                  eventHandlers={{ click: () => onTechnicianClick(tech) }}
                >
                  <Popup>
                    <div style={{ fontFamily: 'system-ui, sans-serif' }}>
                      <p style={{ fontWeight: 700, fontSize: 13, marginBottom: 2 }}>
                        {tech.fullName}
                      </p>
                      <p style={{ fontSize: 11, color: '#94a3b8' }}>
                        No GPS signal
                      </p>
                      <p style={{ fontSize: 10, color: colour, fontWeight: 600 }}>
                        {status}
                      </p>
                    </div>
                  </Popup>
                </CircleMarker>
              )
            })}

          {/* ── Unassigned work order pins ── */}
          {unassignedWOs.map((wo, idx) => {
            const colour = priorityColour(wo.priority)
            // WOs don't carry lat/lng directly; cluster near Nairobi with offset
            const pos = nairobiCluster(idx + 20) // +20 offset so they don't overlap tech rings

            return (
              <Marker
                key={`wo-${wo.id}`}
                position={pos}
                icon={makeWOIcon(colour)}
                eventHandlers={{ click: () => onWorkOrderClick(wo) }}
              >
                <Popup>
                  <div style={{ minWidth: 170, fontFamily: 'system-ui, sans-serif' }}>
                    <p
                      style={{
                        fontFamily: 'monospace',
                        fontWeight: 700,
                        fontSize: 12,
                        marginBottom: 2,
                        color: colour,
                      }}
                    >
                      {wo.workOrderNumber}
                    </p>
                    <p style={{ fontSize: 12, fontWeight: 600, marginBottom: 2 }}>
                      {wo.title}
                    </p>
                    {wo.clientName ? (
                      <p style={{ fontSize: 11, color: '#64748b' }}>{wo.clientName}</p>
                    ) : null}
                    {wo.siteName ? (
                      <p style={{ fontSize: 11, color: '#94a3b8' }}>{wo.siteName}</p>
                    ) : null}
                    <div style={{ marginTop: 6, display: 'flex', gap: 6 }}>
                      <button
                        onClick={() => onWorkOrderClick(wo)}
                        style={{
                          fontSize: 11,
                          padding: '3px 10px',
                          background: colour,
                          color: 'white',
                          border: 'none',
                          borderRadius: 12,
                          cursor: 'pointer',
                          fontWeight: 600,
                        }}
                      >
                        Assign
                      </button>
                      <Link
                        to={`/work-orders/${wo.id}`}
                        style={{
                          fontSize: 11,
                          padding: '3px 10px',
                          background: '#f1f5f9',
                          color: '#475569',
                          borderRadius: 12,
                          textDecoration: 'none',
                          fontWeight: 600,
                        }}
                      >
                        Open
                      </Link>
                    </div>
                  </div>
                </Popup>
              </Marker>
            )
          })}
        </MapContainer>
      </div>

      {/* Footer notice if no GPS data at all */}
      {gpsCount === 0 ? (
        <div className="border-t border-app px-4 py-2 flex items-center gap-2">
          <span className="size-1.5 rounded-full bg-amber-400 inline-block" />
          <p className="text-[11px] text-muted">
            No live GPS data yet — technician positions will appear here once tracking is active.
            Unassigned job pins are approximate (clustered near Nairobi).
          </p>
        </div>
      ) : (
        <div className="border-t border-app px-4 py-2 flex items-center gap-2">
          <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse inline-block" />
          <p className="text-[11px] text-muted">
            {gpsCount} technician{gpsCount !== 1 ? 's' : ''} tracked live ·{' '}
            {technicians.length - gpsCount} without GPS signal ·
            Unassigned WO pins are approximate pending site geocoding.
          </p>
        </div>
      )}
    </div>
  )
}
