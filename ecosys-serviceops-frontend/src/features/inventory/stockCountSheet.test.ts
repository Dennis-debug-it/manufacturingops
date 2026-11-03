import { describe, expect, it } from 'vitest'
import { countSheetCsv, parseCsv, readCountSheet } from './stockCountSheet'
import type { StockCountDetail, StockCountLine } from './stockCountsApi'

const line = (id: string, itemCode: string, barcode: string | null = null, expectedQuantity: number | null = 5): StockCountLine => ({
  id,
  materialItemId: `m-${id}`,
  itemCode,
  itemName: `Part ${itemCode}`,
  unitOfMeasure: 'each',
  category: null,
  expectedQuantity,
  countedQuantity: null,
  variance: null,
  unitCost: 1,
  varianceValue: null,
  notes: null,
  barcode,
  needsRecount: false,
})

describe('count sheet', () => {
  it('parses quoted cells, commas and line breaks', () => {
    expect(parseCsv('a,"b, c","d ""e"""\r\n1,2,3\n')).toEqual([['a', 'b, c', 'd "e"'], ['1', '2', '3']])
  })

  it('exports a sheet that reads back, and leaves Expected blank on a hidden blind count', () => {
    const count = { lines: [line('1', 'F-1', null, 7)], expectedHidden: true } as unknown as StockCountDetail
    const csv = countSheetCsv(count)
    expect(csv.split('\r\n')[1]).toBe('F-1,,Part F-1,,each,,,')
    expect(readCountSheet(csv, count.lines).updates).toEqual({})
  })

  it('matches rows by code or barcode, skips blanks and reports unknown or bad values', () => {
    const lines = [line('1', 'F-1'), line('2', 'B-2', '600123')]
    const sheet = 'Code,Barcode,Part,Counted,Note\nf-1,,Filter,4,two damaged\n,600123,Belt,3,\nX-9,,Other,1,\nB-2,,Belt,abc,\nF-1,,Filter,,\n'
    const result = readCountSheet(sheet, lines)
    expect(result.updates).toEqual({ '1': { counted: '4', notes: 'two damaged' }, '2': { counted: '3' } })
    expect(result.unknown).toEqual(['X-9'])
    expect(result.invalid).toEqual(['B-2'])
  })

  it('needs a Code and a Counted column', () => {
    expect(() => readCountSheet('Part,Qty\nFilter,1', [line('1', 'F-1')])).toThrow()
  })
})
