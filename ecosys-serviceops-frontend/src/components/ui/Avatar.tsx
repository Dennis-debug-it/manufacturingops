type AvatarTone = 'accent' | 'emerald' | 'neutral'

const toneClassMap: Record<AvatarTone, string> = {
  accent: 'avatar-accent',
  emerald: 'avatar-emerald',
  neutral: 'bg-subtle text-app',
}

function initialsFromName(name?: string | null) {
  const normalized = name?.trim()
  if (!normalized) return '?'

  const parts = normalized.split(/\s+/).filter(Boolean)
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase()
  }

  return `${parts[0][0] ?? ''}${parts[1][0] ?? ''}`.toUpperCase()
}

export function Avatar({
  name,
  size = 'md',
  tone = 'accent',
}: {
  name?: string | null
  size?: 'sm' | 'md' | 'lg'
  tone?: AvatarTone
}) {
  const sizeClass = size === 'sm' ? 'h-8 w-8 text-[11px]' : size === 'lg' ? 'h-12 w-12 text-sm' : 'h-10 w-10 text-xs'

  return (
    <span
      className={`inline-flex shrink-0 items-center justify-center rounded-full border border-app font-semibold uppercase tracking-[0.14em] ${sizeClass} ${toneClassMap[tone]}`}
      aria-label={name || 'Avatar'}
      title={name || undefined}
    >
      {initialsFromName(name)}
    </span>
  )
}

export function AvatarStack({
  names,
  limit = 3,
}: {
  names: Array<string | null | undefined>
  limit?: number
}) {
  const visible = names.filter(Boolean).slice(0, limit)
  const overflow = Math.max(0, names.filter(Boolean).length - visible.length)

  return (
    <div className="flex items-center">
      <div className="flex items-center">
        {visible.map((name, index) => (
          <span key={`${name}-${index}`} className={index === 0 ? '' : '-ml-3'}>
            <Avatar name={name} size="sm" tone={index % 2 === 0 ? 'accent' : 'emerald'} />
          </span>
        ))}
      </div>
      {overflow > 0 ? (
        <span className="ml-2 text-xs font-semibold text-muted">+{overflow}</span>
      ) : null}
    </div>
  )
}
