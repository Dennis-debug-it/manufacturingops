import { AlertTriangle } from 'lucide-react'

export function ErrorState({
  title,
  description,
  message,
  onRetry,
}: {
  title?: string
  description?: string
  message?: string
  onRetry?: () => void | Promise<void>
}) {
  const resolvedTitle = title ?? 'Something went wrong'
  const resolvedDescription = description ?? message ?? 'Please try again.'

  return (
    <div className="surface-card bg-[var(--app-badge-danger-bg)]">
      <div className="flex items-start gap-4">
        <div className="rounded-[12px] bg-white/60 p-3 text-[var(--app-badge-danger-text)]">
          <AlertTriangle className="h-5 w-5" />
        </div>
        <div>
          <h3 className="font-heading text-base font-semibold text-app sm:text-lg">{resolvedTitle}</h3>
          <p className="mt-2 text-sm text-muted">{resolvedDescription}</p>
          {onRetry ? (
            <button type="button" className="button-secondary mt-4" onClick={() => void onRetry()}>
              Retry
            </button>
          ) : null}
        </div>
      </div>
    </div>
  )
}
