import { useEffect, useMemo, useRef, useState } from 'react'
import { Search } from 'lucide-react'
import { cn } from '../utils/cn'

export interface AutocompleteSuggestion {
  id: string
  /** Primary text; selecting fills the input with this value. */
  label: string
  /** Optional muted mono code shown on the right (e.g. an item code). */
  code?: string | undefined
  /** Optional muted hint shown far right (e.g. a price). */
  hint?: string | undefined
}

export interface AutocompleteInputProps {
  value: string
  onChange: (value: string) => void
  /** Full candidate list — the component filters it against the current value. */
  suggestions: AutocompleteSuggestion[]
  /** Fired when the user picks a suggestion (after the value is filled). */
  onSelect?: (suggestion: AutocompleteSuggestion) => void
  placeholder?: string | undefined
  className?: string | undefined
  id?: string | undefined
  'aria-label'?: string | undefined
  maxSuggestions?: number | undefined
}

/**
 * Free-text input with type-ahead suggestions. Typing still allows brand-new
 * values (documents can reference items not yet in a catalogue); picking a
 * suggestion fills the field and reports the choice via `onSelect`.
 */
export function AutocompleteInput({
  value,
  onChange,
  suggestions,
  onSelect,
  placeholder,
  className,
  id,
  'aria-label': ariaLabel,
  maxSuggestions = 8,
}: AutocompleteInputProps) {
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)

  const query = value.trim().toLowerCase()
  const matches = useMemo(() => {
    if (!query) return []
    return suggestions
      .filter((s) => {
        const label = s.label.toLowerCase()
        if (label === query) return false // nothing to suggest once it already matches
        return label.includes(query) || (s.code?.toLowerCase().includes(query) ?? false)
      })
      .slice(0, maxSuggestions)
  }, [suggestions, query, maxSuggestions])

  useEffect(() => {
    setHighlight(0)
  }, [query])

  useEffect(() => {
    if (!open) return
    const onDocClick = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [open])

  const choose = (suggestion: AutocompleteSuggestion) => {
    onChange(suggestion.label)
    onSelect?.(suggestion)
    setOpen(false)
  }

  const showList = open && matches.length > 0

  return (
    <div ref={containerRef} className="relative">
      <input
        id={id}
        aria-label={ariaLabel}
        value={value}
        onChange={(event) => {
          onChange(event.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={(event) => {
          if (!showList) return
          if (event.key === 'ArrowDown') {
            event.preventDefault()
            setHighlight((h) => Math.min(h + 1, matches.length - 1))
          } else if (event.key === 'ArrowUp') {
            event.preventDefault()
            setHighlight((h) => Math.max(h - 1, 0))
          } else if (event.key === 'Enter') {
            const picked = matches[highlight]
            if (picked) {
              event.preventDefault()
              choose(picked)
            }
          } else if (event.key === 'Escape') {
            setOpen(false)
          }
        }}
        placeholder={placeholder}
        autoComplete="off"
        role="combobox"
        aria-expanded={showList}
        aria-autocomplete="list"
        className={cn(
          'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]',
          className,
        )}
      />
      {showList ? (
        <ul
          role="listbox"
          className="absolute left-0 right-0 top-full z-30 mt-1 max-h-64 overflow-y-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg"
        >
          {matches.map((suggestion, index) => (
            <li key={suggestion.id} role="option" aria-selected={index === highlight}>
              <button
                type="button"
                // onMouseDown so selection fires before the input blurs.
                onMouseDown={(event) => {
                  event.preventDefault()
                  choose(suggestion)
                }}
                onMouseEnter={() => setHighlight(index)}
                className={cn(
                  'flex w-full items-center gap-2 px-3 py-2 text-left text-sm',
                  index === highlight ? 'bg-slate-100' : 'hover:bg-slate-50',
                )}
              >
                <Search className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                <span className="min-w-0 flex-1 truncate text-slate-800">{suggestion.label}</span>
                {suggestion.code ? <span className="shrink-0 font-mono text-xs text-slate-400">{suggestion.code}</span> : null}
                {suggestion.hint ? <span className="shrink-0 text-xs text-slate-500">{suggestion.hint}</span> : null}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
