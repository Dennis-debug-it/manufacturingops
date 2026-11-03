export interface SpinnerProps {
  label?: string
}

export function Spinner({ label = 'Loading' }: SpinnerProps) {
  return (
    <div className="flex min-h-[40vh] items-center justify-center px-6 py-10 text-sm text-slate-500">
      <div className="flex items-center gap-3 rounded-full border border-slate-200 bg-white px-4 py-2 shadow-sm">
        <span className="h-4 w-4 animate-spin rounded-full border-2 border-slate-200 border-t-slate-900" />
        <span>{label}</span>
      </div>
    </div>
  )
}
