import { describe, expect, it } from 'vitest'
import { pipelineQuery } from './PipelinePage'

describe('pipelineQuery', () => {
  const none = { view: 'board' as const, search: '', stage: '', owner: '', field: '', value: '', pipeline: undefined }

  it('leaves out empty filters and list-only filters on the board', () => {
    expect(pipelineQuery(none)).toBe('')
    expect(pipelineQuery({ ...none, stage: 'Open', field: 'roof', value: 'Flat', owner: 'u1' })).toBe('owner=u1')
  })

  it('keeps every filter the list view uses', () => {
    expect(pipelineQuery({ view: 'list', search: ' solar ', stage: 'Stalled', owner: 'u1', field: 'roof', value: 'Flat', pipeline: 'p1' }))
      .toBe('pipeline=p1&view=list&q=solar&stage=Stalled&owner=u1&field=roof&value=Flat')
  })
})
