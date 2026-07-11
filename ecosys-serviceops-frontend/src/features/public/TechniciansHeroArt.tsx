/**
 * Hand-built, self-contained SVG illustration for the landing hero: two field
 * technicians in hi-vis gear using tablets that show the ServiceOps app, with a
 * facility unit behind them and floating UI cards (work order + SLA gauge).
 *
 * To use a real photograph instead, drop it in `public/` and swap this component
 * for an <img src="/your-photo.jpg" ... /> in LandingPage.tsx.
 */
export function TechniciansHeroArt({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 640 520" className={className} role="img" aria-label="Field technicians using tablets on the ServiceOps platform" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="heroPanel" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#ecfdf5" />
          <stop offset="1" stopColor="#eff6ff" />
        </linearGradient>
        <linearGradient id="tabletScreen" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0f172a" />
          <stop offset="1" stopColor="#1e293b" />
        </linearGradient>
        <filter id="softShadow" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="10" stdDeviation="14" floodColor="#0f172a" floodOpacity="0.12" />
        </filter>
      </defs>

      {/* backdrop panel */}
      <rect x="16" y="24" width="608" height="472" rx="36" fill="url(#heroPanel)" />
      <circle cx="112" cy="120" r="70" fill="#34d399" opacity="0.16" />
      <circle cx="548" cy="404" r="90" fill="#F5C542" opacity="0.16" />

      {/* blueprint grid */}
      <g stroke="#0f766e" strokeOpacity="0.06">
        {[80, 160, 240, 320, 400, 480].map((y) => (
          <line key={y} x1="40" y1={y} x2="600" y2={y} />
        ))}
        {[120, 220, 320, 420, 520].map((x) => (
          <line key={x} x1={x} y1="48" x2={x} y2="472" />
        ))}
      </g>

      {/* facility unit (HVAC / generator) behind the technicians */}
      <g filter="url(#softShadow)">
        <rect x="250" y="188" width="150" height="150" rx="14" fill="#ffffff" />
        <rect x="250" y="188" width="150" height="34" rx="14" fill="#0f766e" />
        <circle cx="290" cy="270" r="26" fill="#e2e8f0" stroke="#94a3b8" strokeWidth="4" />
        <circle cx="360" cy="270" r="26" fill="#e2e8f0" stroke="#94a3b8" strokeWidth="4" />
        <g stroke="#94a3b8" strokeWidth="4" strokeLinecap="round">
          <line x1="290" y1="254" x2="290" y2="286" />
          <line x1="274" y1="270" x2="306" y2="270" />
          <line x1="360" y1="254" x2="360" y2="286" />
          <line x1="344" y1="270" x2="376" y2="270" />
        </g>
        <rect x="266" y="312" width="118" height="12" rx="6" fill="#e2e8f0" />
      </g>

      {/* ground shadow */}
      <ellipse cx="320" cy="452" rx="220" ry="20" fill="#0f172a" opacity="0.06" />

      {/* ── Technician A (left) ── */}
      <g filter="url(#softShadow)">
        {/* legs */}
        <rect x="150" y="360" width="26" height="86" rx="12" fill="#1e293b" />
        <rect x="182" y="360" width="26" height="86" rx="12" fill="#1e293b" />
        {/* body / hi-vis vest */}
        <path d="M138 300 q42 -24 84 0 l6 78 q-48 20 -96 0 z" fill="#10b981" />
        <path d="M172 282 l14 96 M188 282 l-14 96" stroke="#a7f3d0" strokeWidth="6" fill="none" />
        <rect x="150" y="336" width="60" height="10" fill="#e2e8f0" opacity="0.9" />
        {/* arms holding tablet */}
        <path d="M144 312 q-16 30 6 52" stroke="#10b981" strokeWidth="20" fill="none" strokeLinecap="round" />
        <path d="M216 312 q16 30 -6 52" stroke="#10b981" strokeWidth="20" fill="none" strokeLinecap="round" />
        {/* head + hard hat */}
        <circle cx="180" cy="262" r="26" fill="#f8c9a4" />
        <path d="M150 258 a30 30 0 0 1 60 0 z" fill="#F5C542" />
        <rect x="146" y="256" width="68" height="9" rx="4" fill="#eab308" />
        <rect x="176" y="232" width="8" height="14" rx="4" fill="#eab308" />
      </g>
      {/* tablet A */}
      <g filter="url(#softShadow)">
        <rect x="150" y="356" width="86" height="60" rx="8" fill="#0f172a" transform="rotate(-8 193 386)" />
        <rect x="156" y="362" width="74" height="48" rx="4" fill="url(#tabletScreen)" transform="rotate(-8 193 386)" />
        <g transform="rotate(-8 193 386)">
          <rect x="160" y="366" width="66" height="9" rx="3" fill="#10b981" />
          <rect x="160" y="380" width="42" height="6" rx="3" fill="#475569" />
          <rect x="160" y="392" width="54" height="6" rx="3" fill="#475569" />
          <rect x="160" y="404" width="30" height="6" rx="3" fill="#334155" />
        </g>
      </g>

      {/* ── Technician B (right) ── */}
      <g filter="url(#softShadow)">
        {/* legs */}
        <rect x="428" y="368" width="26" height="80" rx="12" fill="#1e293b" />
        <rect x="460" y="368" width="26" height="80" rx="12" fill="#1e293b" />
        {/* body / hi-vis vest (amber) */}
        <path d="M416 308 q42 -24 84 0 l6 74 q-48 20 -96 0 z" fill="#F5C542" />
        <path d="M450 290 l14 92 M466 290 l-14 92" stroke="#fef3c7" strokeWidth="6" fill="none" />
        <rect x="428" y="342" width="60" height="10" fill="#334155" opacity="0.5" />
        {/* arms holding tablet */}
        <path d="M422 320 q-16 28 6 50" stroke="#F5C542" strokeWidth="20" fill="none" strokeLinecap="round" />
        <path d="M494 320 q16 28 -6 50" stroke="#F5C542" strokeWidth="20" fill="none" strokeLinecap="round" />
        {/* head + hard hat */}
        <circle cx="458" cy="270" r="26" fill="#e8b08a" />
        <path d="M428 266 a30 30 0 0 1 60 0 z" fill="#10b981" />
        <rect x="424" y="264" width="68" height="9" rx="4" fill="#059669" />
        <rect x="454" y="240" width="8" height="14" rx="4" fill="#059669" />
      </g>
      {/* tablet B */}
      <g filter="url(#softShadow)">
        <rect x="424" y="362" width="86" height="60" rx="8" fill="#0f172a" transform="rotate(7 467 392)" />
        <rect x="430" y="368" width="74" height="48" rx="4" fill="url(#tabletScreen)" transform="rotate(7 467 392)" />
        <g transform="rotate(7 467 392)">
          <rect x="434" y="372" width="66" height="9" rx="3" fill="#F5C542" />
          <rect x="434" y="386" width="30" height="16" rx="3" fill="#10b981" />
          <rect x="468" y="386" width="32" height="16" rx="3" fill="#1e293b" />
          <rect x="434" y="406" width="66" height="5" rx="2" fill="#334155" />
        </g>
      </g>

      {/* floating card: work order complete */}
      <g filter="url(#softShadow)">
        <rect x="72" y="196" width="150" height="66" rx="14" fill="#ffffff" />
        <circle cx="100" cy="229" r="16" fill="#dcfce7" />
        <path d="M93 229 l5 5 l9 -10" stroke="#16a34a" strokeWidth="4" fill="none" strokeLinecap="round" strokeLinejoin="round" />
        <rect x="126" y="214" width="80" height="9" rx="4" fill="#1e293b" />
        <rect x="126" y="230" width="58" height="7" rx="3" fill="#94a3b8" />
        <rect x="126" y="243" width="68" height="7" rx="3" fill="#cbd5e1" />
      </g>

      {/* floating card: SLA gauge */}
      <g filter="url(#softShadow)">
        <rect x="452" y="150" width="128" height="92" rx="16" fill="#ffffff" />
        <path d="M478 214 a34 34 0 1 1 76 0" fill="none" stroke="#e2e8f0" strokeWidth="10" strokeLinecap="round" />
        <path d="M478 214 a34 34 0 0 1 68 -12" fill="none" stroke="#10b981" strokeWidth="10" strokeLinecap="round" />
        <text x="516" y="206" textAnchor="middle" fontFamily="Inter, system-ui, sans-serif" fontSize="24" fontWeight="800" fill="#0f172a">96%</text>
        <text x="516" y="230" textAnchor="middle" fontFamily="Inter, system-ui, sans-serif" fontSize="11" fontWeight="600" fill="#64748b">SLA MET</text>
      </g>
    </svg>
  )
}
