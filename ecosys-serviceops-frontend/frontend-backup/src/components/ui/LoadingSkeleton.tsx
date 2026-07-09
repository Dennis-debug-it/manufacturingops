export function LoadingSkeleton({ lines = 4 }: { lines?: number }) {
  return (
    <div className="section-panel space-y-3">
      {Array.from({ length: lines }).map((_, index) => (
        <div key={index} className="skeleton-block h-4 rounded-[10px]" />
      ))}
    </div>
  )
}
