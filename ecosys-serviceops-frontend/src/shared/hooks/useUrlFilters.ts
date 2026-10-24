import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { readUrlFilters, writeUrlFilters, type UrlFilterSpec, type UrlFilterValues } from '../utils/urlFilters'

/**
 * A list page's filters, read from and written back to the URL (see `urlFilters.ts`). Pass a spec
 * defined at module level so its identity is stable.
 */
export function useUrlFilters<K extends string>(spec: UrlFilterSpec<K>) {
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo(() => readUrlFilters(searchParams, spec), [searchParams, spec])
  const update = useCallback(
    (patch: Partial<UrlFilterValues<K>>) => setSearchParams((previous) => writeUrlFilters(previous, patch, spec), { replace: true }),
    [setSearchParams, spec],
  )
  return { filters, update }
}
