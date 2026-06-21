export function LoadingState({ label = 'Loading workspace' }: { label?: string }) {
  return (
    <div className="surface-card min-h-[220px]">
      <div className="skeleton-block h-4 w-32 rounded-full" />
      <div className="skeleton-block mt-4 h-10 w-48 rounded-full" />
      <div className="mt-6 space-y-3">
        <div className="skeleton-block h-14 rounded-2xl" />
        <div className="skeleton-block h-14 rounded-2xl" />
        <div className="skeleton-block h-14 rounded-2xl" />
      </div>
      <p className="mt-6 text-sm text-muted">{label}</p>
    </div>
  )
}
