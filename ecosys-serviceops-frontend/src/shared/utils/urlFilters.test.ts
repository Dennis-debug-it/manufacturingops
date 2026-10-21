import { describe, expect, it } from 'vitest'
import { parseIdList, readUrlFilters, writeUrlFilters, type UrlFilterSpec } from './urlFilters'

const spec: UrlFilterSpec<'status' | 'age'> = {
  status: { param: 'status' },
  age: { param: 'age', values: ['overdue', '1-30'] },
}

describe('URL list filters', () => {
  it('reads each filter from its parameter, ignoring values it does not accept', () => {
    expect(readUrlFilters(new URLSearchParams('status=Draft&age=overdue'), spec)).toEqual({ status: 'Draft', age: 'overdue' })
    expect(readUrlFilters(new URLSearchParams('age=forever'), spec)).toEqual({ status: '', age: '' })
  })

  it('writes changes back, removing cleared filters and keeping other parameters', () => {
    const next = writeUrlFilters(new URLSearchParams('status=Draft&tab=x'), { status: '', age: '1-30' }, spec)
    expect(next.toString()).toBe('tab=x&age=1-30')
  })

  it('parses id lists', () => {
    expect(parseIdList(' A,b,,c ')).toEqual(['a', 'b', 'c'])
    expect(parseIdList('')).toEqual([])
  })
})
