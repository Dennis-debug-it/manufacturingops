import { parseIdList, type UrlFilterSpec } from '../../shared/utils/urlFilters'
import { stageCategories, type ProjectListItem } from './types'

/**
 * The project register's URL filters. `stage` is absent for the default (open projects), `all` for
 * every stage, or one stage category; `health=behind`, `mine=true` and `ids=` (the projects behind a
 * dashboard figure) narrow it further.
 */
export const projectListFilterSpec: UrlFilterSpec<'stage' | 'health' | 'mine' | 'ids'> = {
  stage: { param: 'stage', values: ['all', 'Open', ...stageCategories] },
  health: { param: 'health', values: ['behind'] },
  mine: { param: 'mine', values: ['true'] },
  ids: { param: 'ids' },
}

/** The register API's `stageCategory` for the URL's `stage` ('' means every stage). */
export function stageCategoryFor(stage: string): string {
  if (stage === 'all') return ''
  return stage || 'Open'
}

/** The URL's `stage` for a stage picked on the page. */
export function stageParamFor(stageCategory: string): string {
  if (stageCategory === '') return 'all'
  return stageCategory === 'Open' ? '' : stageCategory
}

/** The filters the register applies in the browser: behind schedule, and an id list. */
export function filterProjects(rows: ProjectListItem[], filters: { health: string; ids: string }): ProjectListItem[] {
  const ids = new Set(parseIdList(filters.ids))
  return rows.filter((row) => (filters.health !== 'behind' || row.isBehindSchedule) && (ids.size === 0 || ids.has(row.id.toLowerCase())))
}
