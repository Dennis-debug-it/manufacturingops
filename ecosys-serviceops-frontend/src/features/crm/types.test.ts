import { describe, expect, it } from 'vitest'
import { daysUntilDeadline, fieldAppliesTo, openStagesOf, opportunityParty, pipelineFor, stageName, type DealField, type SalesPipeline } from './types'

describe('daysUntilDeadline', () => {
  const now = new Date(2026, 9, 20, 15, 30)

  it('counts calendar days, not 24-hour blocks', () => {
    expect(daysUntilDeadline(new Date(2026, 9, 20, 9, 0).toISOString(), now)).toBe(0)
    expect(daysUntilDeadline(new Date(2026, 9, 21, 8, 0).toISOString(), now)).toBe(1)
    expect(daysUntilDeadline(new Date(2026, 9, 27, 23, 59).toISOString(), now)).toBe(7)
  })

  it('goes negative once the deadline has passed', () => {
    expect(daysUntilDeadline(new Date(2026, 9, 17, 12, 0).toISOString(), now)).toBe(-3)
  })
})

describe('opportunityParty', () => {
  it('prefers the client, then the prospect company, then the contact', () => {
    expect(opportunityParty({ clientName: 'Kilele Hotel', companyName: 'Old name', contactName: 'Amina' })).toBe('Kilele Hotel')
    expect(opportunityParty({ clientName: null, companyName: 'Baraka Logistics', contactName: 'Amina' })).toBe('Baraka Logistics')
    expect(opportunityParty({ clientName: null, companyName: null, contactName: 'Amina' })).toBe('Amina')
  })
})

describe('pipeline stages', () => {
  const stage = (key: string, name: string, isClosed = false) => ({ key, name, probability: 0, isClosed, dealCount: 0 })
  const pipeline = (id: string, isDefault: boolean, stages: SalesPipeline['stages']): SalesPipeline =>
    ({ id, name: id, sortOrder: 0, module: null, isDefault, isActive: true, available: true, stages })
  const sales = pipeline('sales', true, [stage('Lead', 'Lead'), stage('SiteSurvey', 'Site visit'), stage('Won', 'Won', true), stage('Lost', 'Lost', true)])
  const amc = pipeline('amc', false, [stage('Scoping', 'Scoping'), stage('Won', 'Won', true), stage('Lost', 'Lost', true)])

  it('falls back to the default pipeline for deals without one', () => {
    expect(pipelineFor([amc, sales], null)?.id).toBe('sales')
    expect(pipelineFor([amc, sales], 'amc')?.id).toBe('amc')
  })

  it('names stages from the deal pipeline, then any pipeline, then the built-in labels', () => {
    expect(stageName('SiteSurvey', [sales, amc], 'sales')).toBe('Site visit')
    expect(stageName('Scoping', [sales, amc])).toBe('Scoping')
    expect(stageName('Negotiation')).toBe('Negotiation')
    expect(stageName('Unknown')).toBe('Unknown')
  })

  it('lists only open stages, or the built-in ones while pipelines load', () => {
    expect(openStagesOf(amc).map((s) => s.key)).toEqual(['Scoping'])
    expect(openStagesOf(undefined).map((s) => s.key)).toEqual(['Lead', 'Qualified', 'SiteSurvey', 'Proposal', 'Negotiation'])
  })
})

describe('fieldAppliesTo', () => {
  const field = (patch: Partial<DealField>): DealField =>
    ({ id: 'f', key: 'roof', label: 'Roof', fieldType: 'Text', options: [], helpText: null, pipelineId: null, isActive: true, sortOrder: 0, valueCount: 0, ...patch })

  it('shows a field in every pipeline unless it is tied to one or switched off', () => {
    expect(fieldAppliesTo(field({}), 'any')).toBe(true)
    expect(fieldAppliesTo(field({ pipelineId: 'amc' }), 'amc')).toBe(true)
    expect(fieldAppliesTo(field({ pipelineId: 'amc' }), 'sales')).toBe(false)
    expect(fieldAppliesTo(field({ isActive: false }), 'sales')).toBe(false)
  })
})
