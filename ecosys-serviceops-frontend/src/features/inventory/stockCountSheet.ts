import type { StockCountDetail, StockCountLine } from './stockCountsApi'

/** One CSV cell, quoted when it holds a comma, quote or line break. */
function cell(value: string | number | null | undefined) {
  const text = value == null ? '' : String(value)
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

/**
 * The count sheet as CSV: one row per part, with a blank Counted column to fill in on paper or in a
 * spreadsheet and import back. Expected is left blank on a blind count the viewer may not see.
 */
export function countSheetCsv(count: StockCountDetail) {
  const header = ['Code', 'Barcode', 'Part', 'Category', 'Unit', 'Expected', 'Counted', 'Note']
  const rows = count.lines.map((line) => [
    line.itemCode,
    line.barcode,
    line.itemName,
    line.category,
    line.unitOfMeasure,
    count.expectedHidden ? '' : line.expectedQuantity,
    line.countedQuantity,
    line.notes,
  ])
  return [header, ...rows].map((row) => row.map(cell).join(',')).join('\r\n')
}

/** Parses CSV text into rows of cells (RFC 4180: quoted cells may hold commas, quotes and newlines). */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let current = ''
  let quoted = false
  const source = text.replace(/^\uFEFF/, '')
  for (let index = 0; index < source.length; index += 1) {
    const char = source[index]
    if (quoted) {
      if (char === '"' && source[index + 1] === '"') {
        current += '"'
        index += 1
      } else if (char === '"') {
        quoted = false
      } else {
        current += char
      }
    } else if (char === '"') {
      quoted = true
    } else if (char === ',') {
      row.push(current)
      current = ''
    } else if (char === '\n' || char === '\r') {
      if (char === '\r' && source[index + 1] === '\n') index += 1
      row.push(current)
      rows.push(row)
      row = []
      current = ''
    } else {
      current += char
    }
  }
  if (current !== '' || row.length > 0) {
    row.push(current)
    rows.push(row)
  }
  return rows.filter((cells) => cells.some((value) => value.trim() !== ''))
}

export interface CountSheetImport {
  /** Counted quantity (and note, when the sheet has one) by line id. */
  updates: Record<string, { counted: string; notes?: string }>
  /** Codes in the sheet that are not on this count. */
  unknown: string[]
  /** Rows whose Counted value is not a number of 0 or more. */
  invalid: string[]
}

/**
 * Reads a filled-in count sheet. Rows are matched to lines by Code (or Barcode); a blank Counted
 * cell is skipped, so a half-counted sheet only fills in what was counted.
 */
export function readCountSheet(text: string, lines: StockCountLine[]): CountSheetImport {
  const [header, ...rows] = parseCsv(text)
  const result: CountSheetImport = { updates: {}, unknown: [], invalid: [] }
  if (!header) return result
  const column = (name: string) => header.findIndex((value) => value.trim().toLowerCase() === name)
  const codeColumn = column('code')
  const barcodeColumn = column('barcode')
  const countedColumn = column('counted')
  const noteColumn = column('note')
  if ((codeColumn < 0 && barcodeColumn < 0) || countedColumn < 0) {
    throw new Error('The sheet needs a Code (or Barcode) column and a Counted column.')
  }

  const byKey = new Map<string, StockCountLine>()
  for (const line of lines) {
    if (line.itemCode) byKey.set(line.itemCode.trim().toLowerCase(), line)
    if (line.barcode) byKey.set(line.barcode.trim().toLowerCase(), line)
  }

  for (const row of rows) {
    const code = (codeColumn >= 0 ? row[codeColumn] : '')?.trim() || (barcodeColumn >= 0 ? row[barcodeColumn] : '')?.trim() || ''
    const counted = (row[countedColumn] ?? '').trim()
    if (!code || counted === '') continue
    const line = byKey.get(code.toLowerCase()) ?? (barcodeColumn >= 0 ? byKey.get((row[barcodeColumn] ?? '').trim().toLowerCase()) : undefined)
    if (!line) {
      result.unknown.push(code)
      continue
    }
    const value = Number(counted)
    if (!Number.isFinite(value) || value < 0) {
      result.invalid.push(code)
      continue
    }
    const note = noteColumn >= 0 ? (row[noteColumn] ?? '').trim() : ''
    result.updates[line.id] = note ? { counted, notes: note } : { counted }
  }
  return result
}

/** Text made safe to place in the sheet's HTML (the browser does the escaping). */
function escapeHtml(value: string | number | null | undefined) {
  const holder = document.createElement('span')
  holder.textContent = String(value ?? '')
  return holder.innerHTML
}

/** Opens a printable count sheet (print or save as PDF from the browser's dialog). */
export function printCountSheet(count: StockCountDetail) {
  const showExpected = !count.expectedHidden && !count.isBlind
  const rows = count.lines
    .map((line) => `<tr><td>${escapeHtml(line.itemCode)}</td><td>${escapeHtml(line.itemName)}</td><td>${escapeHtml(line.unitOfMeasure)}</td>${showExpected ? `<td class="n">${escapeHtml(line.expectedQuantity)}</td>` : ''}<td class="box">${escapeHtml(line.countedQuantity)}</td><td class="box wide"></td></tr>`)
    .join('')
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(count.countNumber)} count sheet</title>
<style>
body{font:12px system-ui,sans-serif;color:#0f172a;margin:24px}
h1{font-size:18px;margin:0 0 4px}p{margin:0 0 12px;color:#475569}
table{width:100%;border-collapse:collapse}th,td{border:1px solid #cbd5e1;padding:6px 8px;text-align:left}
th{background:#f1f5f9}.n{text-align:right}.box{width:70px}.wide{width:160px}
footer{margin-top:24px;display:flex;gap:48px}footer span{border-top:1px solid #0f172a;padding-top:4px;min-width:200px}
@media print{body{margin:0}}
</style></head><body>
<h1>Stock count ${escapeHtml(count.countNumber)}</h1>
<p>${escapeHtml(count.locationName ?? count.branchName ?? 'All stock')} · ${escapeHtml(count.category ?? 'All parts')} · ${count.lines.length} parts${count.isBlind ? ' · blind count' : ''}</p>
<table><thead><tr><th>Code</th><th>Part</th><th>Unit</th>${showExpected ? '<th class="n">Expected</th>' : ''}<th>Counted</th><th>Note</th></tr></thead><tbody>${rows}</tbody></table>
<footer><span>Counted by</span><span>Date</span><span>Checked by</span></footer>
<script>window.onload=function(){window.print()}</script>
</body></html>`
  const sheet = window.open('', '_blank', 'width=900,height=700')
  if (!sheet) return false
  sheet.document.open()
  sheet.document.write(html)
  sheet.document.close()
  return true
}

/** Saves text as a file in the browser. */
export function downloadText(filename: string, text: string, type = 'text/csv') {
  const url = URL.createObjectURL(new Blob([text], { type: `${type};charset=utf-8` }))
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
