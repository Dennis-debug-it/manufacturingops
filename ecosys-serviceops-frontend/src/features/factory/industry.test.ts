import { describe, expect, it } from 'vitest'
import { factoryIndustryKey } from './industry'
import type { OrgNode } from './types'

const node = (id: string, level: OrgNode['level'], parentId: string | null, templateKey: string | null = null) =>
  ({ id, level, parentId, templateKey, name: id, code: id, description: null, location: null, displayOrder: 0, isActive: true, externalId: null, externalSystem: null, canAccess: true, childCount: 0, assetCount: 0 }) as OrgNode

describe('a factory industry', () => {
  const nodes = [node('g', 'Group', null), node('tea', 'Factory', 'g'), node('mill', 'Factory', 'g', 'sugar'), node('mill-cent', 'Section', 'mill', 'sugar'), node('tea-dry', 'Section', 'tea', 'tea')]

  it('is the factory own industry, or the workspace one', () => {
    expect(factoryIndustryKey(nodes, 'mill', 'tea')).toBe('sugar')
    expect(factoryIndustryKey(nodes, 'tea', 'tea')).toBe('tea')
    expect(factoryIndustryKey(nodes, 'tea', 'dairy')).toBe('dairy')
  })

  it('follows the factory above a section', () => {
    expect(factoryIndustryKey(nodes, 'mill-cent', 'tea')).toBe('sugar')
    expect(factoryIndustryKey(nodes, 'tea-dry', 'dairy')).toBe('dairy')
    expect(factoryIndustryKey(nodes, 'unknown', 'tea')).toBe('tea')
  })
})
