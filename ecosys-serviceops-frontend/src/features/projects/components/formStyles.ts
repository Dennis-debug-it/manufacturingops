export const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)] disabled:bg-slate-50'
export const textAreaClass = 'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]'
export const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
export const primaryButtonClass = 'inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'
export const secondaryButtonClass = 'inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'
export const smallButtonClass = 'inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'

/** `<input type="date">` value for a nullable ISO date. */
export const dateInputValue = (value: string | null | undefined) => (value ? value.slice(0, 10) : '')

/** A nullable ISO date (yyyy-MM-dd) from a date input, which gives '' when cleared. */
export const dateOrNull = (value: string) => (value ? value : null)

export const textOrNull = (value: string) => (value.trim() ? value.trim() : null)
