import { cn } from '../utils/cn'

interface OnOffSwitchProps {
  checked: boolean
  label: string
  disabled?: boolean
  onChange: (next: boolean) => void
}

/** A compact On/Off switch, styled like the notification settings switch. */
export function OnOffSwitch({ checked, label, disabled = false, onChange }: OnOffSwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={cn(
        'h-8 min-w-[3.25rem] rounded-lg px-3 text-sm font-medium ring-1 ring-inset transition disabled:opacity-60',
        checked ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-600 ring-slate-300 hover:bg-slate-50',
      )}
    >
      {checked ? 'On' : 'Off'}
    </button>
  )
}
