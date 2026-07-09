export function LoadingState({ label = 'Loading workspace' }: { label?: string }) {
  return (
    <div className="section-panel min-h-[220px]">
      <div className="skeleton-block h-3.5 w-28 rounded-full" />
      <div className="skeleton-block mt-4 h-8 w-40 rounded-full" />
      <div className="mt-6 space-y-3">
        <div className="skeleton-block h-12 rounded-[14px]" />
        <div className="skeleton-block h-12 rounded-[14px]" />
        <div className="skeleton-block h-12 rounded-[14px]" />
      </div>
      <p className="mt-5 text-sm text-muted">{label}</p>
    </div>
  )
}
