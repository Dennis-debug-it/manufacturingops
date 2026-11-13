import { useFactorySettings, useIndustryTemplates, useOrgNodes } from './api'
import type { IndustryTemplate, IndustryVocabulary, OrgNode } from './types'

/** Tea's words, used until the settings load (the product launched with tea). */
export const DEFAULT_VOCABULARY: IndustryVocabulary = {
  overhaulSeason: 'low-crop shutdown',
  lowSeason: 'Low-crop season',
  supplyFleet: 'Leaf collection fleet',
  supplyFleetDescription: 'Lorries, pickups and tractors that bring green leaf in',
  rawMaterial: 'green leaf',
}

/** The workspace's industry and its words, for screens that say "low-crop shutdown" or "off-season shutdown". */
export function useIndustry(): { industry: IndustryTemplate | undefined; vocabulary: IndustryVocabulary; productionUnit: string } {
  const { data } = useFactorySettings()
  return {
    industry: data?.industry,
    vocabulary: data?.industry?.vocabulary ?? DEFAULT_VOCABULARY,
    productionUnit: data?.productionUnit ?? 'kg made tea',
  }
}

/**
 * A factory's industry: its own when it has one (a group with mixed factories), otherwise the
 * workspace's. Sections resolve to the factory above them. Mirrors the server's ForFactoryAsync.
 */
export function factoryIndustryKey(nodes: OrgNode[] | undefined, nodeId: string, workspaceKey: string | undefined): string | undefined {
  const byId = new Map((nodes ?? []).map((node) => [node.id, node]))
  for (let node = byId.get(nodeId); node; node = node.parentId ? byId.get(node.parentId) : undefined) {
    if (node.level === 'Factory') return node.templateKey ?? workspaceKey
  }
  return workspaceKey
}

export function useFactoryIndustry(nodeId: string): IndustryTemplate | undefined {
  const { data: nodes } = useOrgNodes()
  const { data: settings } = useFactorySettings()
  const { data: templates } = useIndustryTemplates()
  const key = nodeId ? factoryIndustryKey(nodes, nodeId, settings?.industryTemplate) : settings?.industryTemplate
  return templates?.find((x) => x.key === key) ?? settings?.industry
}
