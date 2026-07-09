import { parseCsvRecords } from '../../../shared/utils/csv'

export function downloadCsvTemplate(fileName: string, comments: string[], headers: readonly string[], exampleRows: Array<Record<string, string>>) {
  const lines = [
    ...comments,
    headers.join(','),
    ...exampleRows.map((row) => headers.map((header) => `"${row[header] ?? ''}"`).join(',')),
  ]
  const blob = new Blob([lines.join('\n')], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export async function readCsvFile(file: File) {
  const text = await file.text()
  return parseCsvRecords(text, { commentPrefix: '#', skipEmptyLines: true })
}
