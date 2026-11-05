import type { OrgNode, OrgNodeLevel } from './types'

export interface TreeNode extends OrgNode {
  depth: number
  children: TreeNode[]
}

const LEVEL_RANK: Record<OrgNodeLevel, number> = { Group: 0, Factory: 1, Section: 2, SubSection: 3 }

function compare(a: OrgNode, b: OrgNode) {
  return LEVEL_RANK[a.level] - LEVEL_RANK[b.level] || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)
}

/** Builds the tree; a node whose parent isn't in the list becomes a root (e.g. a scoped user's factory). */
export function buildTree(nodes: OrgNode[]): TreeNode[] {
  const byId = new Map<string, TreeNode>(nodes.map((node) => [node.id, { ...node, depth: 0, children: [] }]))
  const roots: TreeNode[] = []
  for (const node of byId.values()) {
    const parent = node.parentId ? byId.get(node.parentId) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  }
  const sort = (list: TreeNode[], depth: number) => {
    list.sort(compare)
    for (const node of list) {
      node.depth = depth
      sort(node.children, depth + 1)
    }
  }
  sort(roots, 0)
  return roots
}

/** Depth-first order, for indented lists and selects. */
export function flatten(tree: TreeNode[]): TreeNode[] {
  return tree.flatMap((node) => [node, ...flatten(node.children)])
}

/** "Factory › Section" for a node, leaving out groups as labels and scan results do. */
export function pathOf(nodeId: string | null | undefined, nodes: OrgNode[]): string {
  if (!nodeId) return ''
  const byId = new Map(nodes.map((node) => [node.id, node]))
  const names: string[] = []
  for (let current = byId.get(nodeId); current; current = current.parentId ? byId.get(current.parentId) : undefined) {
    if (current.level !== 'Group') names.unshift(current.name)
  }
  return names.join(' › ')
}

/** The levels a child of `parent` may have, given which levels the tenant uses. */
export function childLevels(parent: OrgNode | null, settings: { useGroupLevel: boolean; useSubSectionLevel: boolean }): OrgNodeLevel[] {
  if (!parent) return settings.useGroupLevel ? ['Group', 'Factory'] : ['Factory']
  switch (parent.level) {
    case 'Group': return settings.useGroupLevel ? ['Group', 'Factory'] : ['Factory']
    case 'Factory': return ['Section']
    case 'Section': return settings.useSubSectionLevel ? ['SubSection'] : []
    default: return []
  }
}

/** Whether an asset can be located at this node (not a group, and visible to the user). */
export function canHoldAssets(node: OrgNode) {
  return node.level !== 'Group' && node.isActive && node.canAccess
}
