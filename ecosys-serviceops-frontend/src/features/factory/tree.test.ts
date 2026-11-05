import { describe, expect, it } from 'vitest'
import { buildTree, canHoldAssets, childLevels, flatten, pathOf } from './tree'
import type { OrgNode } from './types'

const node = (id: string, parentId: string | null, level: OrgNode['level'], name: string, extra: Partial<OrgNode> = {}): OrgNode => ({
  id, parentId, level, name, code: id.toUpperCase(), description: null, location: null, displayOrder: 0, isActive: true,
  templateKey: null, externalId: null, externalSystem: null, canAccess: true, childCount: 0, assetCount: 0, ...extra,
})

const drying = node('dry', 'chl', 'Section', 'Drying', { displayOrder: 2 })
const group = node('grp', null, 'Group', 'Kericho Group', { canAccess: false })
const chelal = node('chl', 'grp', 'Factory', 'Chelal')
const nodes = [drying, node('with', 'chl', 'Section', 'Withering', { displayOrder: 1 }), group, chelal, node('mml', 'grp', 'Factory', 'Momul')]

describe('factory tree', () => {
  it('nests nodes and orders siblings by display order then name', () => {
    const order = flatten(buildTree(nodes)).map((x) => `${'-'.repeat(x.depth)}${x.name}`)
    expect(order).toEqual(['Kericho Group', '-Chelal', '--Withering', '--Drying', '-Momul'])
  })

  it('treats a node whose parent is missing as a root', () => {
    const roots = buildTree(nodes.filter((x) => x.id !== 'grp'))
    expect(roots.map((x) => x.name)).toEqual(['Chelal', 'Momul'])
  })

  it('builds a location path without groups', () => {
    expect(pathOf('dry', nodes)).toBe('Chelal › Drying')
    expect(pathOf('grp', nodes)).toBe('')
    expect(pathOf(null, nodes)).toBe('')
  })

  it('offers only the levels allowed under a parent', () => {
    const all = { useGroupLevel: true, useSubSectionLevel: true }
    expect(childLevels(null, all)).toEqual(['Group', 'Factory'])
    expect(childLevels(null, { ...all, useGroupLevel: false })).toEqual(['Factory'])
    expect(childLevels(chelal, all)).toEqual(['Section'])
    expect(childLevels(drying, { ...all, useSubSectionLevel: false })).toEqual([])
  })

  it('places assets only in accessible, active factories and sections', () => {
    expect(canHoldAssets(group)).toBe(false)
    expect(canHoldAssets(drying)).toBe(true)
    expect(canHoldAssets({ ...drying, isActive: false })).toBe(false)
  })
})
