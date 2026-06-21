export function LoadingSkeleton({ lines = 4 }: { lines?: number }) {
  return (
    <div className="surface-card space-y-3">
      {Array.from({ length: lines }).map((_, index) => (
        <div key={index} className="skeleton-block h-4 rounded-xl" />
      ))}
    </div>
  )
}
