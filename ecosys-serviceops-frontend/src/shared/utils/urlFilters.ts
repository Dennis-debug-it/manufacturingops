/**
 * List filters kept in the URL query string, so a dashboard figure (or a bookmark) can open a list
 * already filtered, and changing a filter on the page updates the URL — the work-order list's
 * pattern (`useWorkOrderListState`), made generic. Every filter is a string; '' means "not set".
 */

/** Per filter: its query parameter, and optionally the only values it accepts (others read as ''). */
export type UrlFilterSpec<K extends string> = Record<K, { param: string; values?: readonly string[] }>

export type UrlFilterValues<K extends string> = Record<K, string>

export function readUrlFilters<K extends string>(params: URLSearchParams, spec: UrlFilterSpec<K>): UrlFilterValues<K> {
  const result = {} as UrlFilterValues<K>
  for (const key of Object.keys(spec) as K[]) {
    const { param, values } = spec[key]
    const raw = (params.get(param) ?? '').trim()
    result[key] = values && !values.includes(raw) ? '' : raw
  }
  return result
}

/** The next query string: patched filters set (or removed when ''), everything else left as it was. */
export function writeUrlFilters<K extends string>(
  previous: URLSearchParams,
  patch: Partial<UrlFilterValues<K>>,
  spec: UrlFilterSpec<K>,
): URLSearchParams {
  const next = new URLSearchParams(previous)
  for (const key of Object.keys(patch) as K[]) {
    const value = patch[key]
    const { param } = spec[key]
    if (!value) next.delete(param)
    else next.set(param, value)
  }
  return next
}

/** A comma-separated id list (`?ids=a,b`), as the dashboard links several projects. */
export function parseIdList(value: string): string[] {
  return value.split(',').map((id) => id.trim().toLowerCase()).filter(Boolean)
}
