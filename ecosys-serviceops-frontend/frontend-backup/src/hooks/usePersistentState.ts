import { useEffect, useMemo, useState } from 'react'

function readStoredValue<T>(key: string, fallback: T, validate?: (value: unknown) => value is T): T {
  if (typeof window === 'undefined') return fallback
  try {
    const raw = window.localStorage.getItem(key)
    if (!raw) return fallback
    const parsed = JSON.parse(raw) as unknown
    if (validate && !validate(parsed)) {
      return fallback
    }
    return parsed as T
  } catch {
    return fallback
  }
}

export function usePersistentState<T>(
  key: string,
  fallback: T,
  options?: { validate?: (value: unknown) => value is T },
) {
  const initialValue = useMemo(() => readStoredValue(key, fallback, options?.validate), [fallback, key, options?.validate])
  const [value, setValue] = useState<T>(initialValue)

  useEffect(() => {
    try {
      window.localStorage.setItem(key, JSON.stringify(value))
    } catch {
      // Ignore storage failures and keep the workspace functional.
    }
  }, [key, value])

  const reset = () => setValue(fallback)

  return { value, setValue, reset }
}
