export interface CsvParseOptions {
  commentPrefix?: string
  skipEmptyLines?: boolean
}

export function downloadCsv(fileName: string, rows: Array<Record<string, string | number | boolean | null | undefined>>) {
  if (rows.length === 0) {
    return
  }

  const headers = Array.from(new Set(rows.flatMap((row) => Object.keys(row))))
  const escapeValue = (value: string | number | boolean | null | undefined) => `"${String(value ?? '').replace(/"/g, '""')}"`
  const csv = [
    headers.join(','),
    ...rows.map((row) => headers.map((header) => escapeValue(row[header])).join(',')),
  ].join('\n')

  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

function parseCsvLine(line: string) {
  const values: string[] = []
  let current = ''
  let inQuotes = false

  for (let index = 0; index < line.length; index += 1) {
    const char = line[index]
    if (char === '"') {
      if (inQuotes && index + 1 < line.length && line[index + 1] === '"') {
        current += '"'
        index += 1
      } else {
        inQuotes = !inQuotes
      }
      continue
    }

    if (char === ',' && !inQuotes) {
      values.push(current.trim())
      current = ''
      continue
    }

    current += char
  }

  values.push(current.trim())
  return values
}

export function parseCsvRecords(text: string, options?: CsvParseOptions) {
  const commentPrefix = options?.commentPrefix ?? '#'
  const lines = text
    .replace(/\r/g, '')
    .split('\n')
    .filter((line) => {
      if (options?.skipEmptyLines !== false && !line.trim()) {
        return false
      }
      return !line.trimStart().startsWith(commentPrefix)
    })

  if (lines.length === 0) {
    return { headers: [] as string[], rows: [] as Array<Record<string, string>> }
  }

  const headerLine = lines[0]
  if (!headerLine) {
    return { headers: [] as string[], rows: [] as Array<Record<string, string>> }
  }

  const headers = parseCsvLine(headerLine)
  const rows = lines.slice(1).map((line) => {
    const values = parseCsvLine(line)
    const record: Record<string, string> = {}
    headers.forEach((header, index) => {
      if (header) {
        record[header] = values[index] ?? ''
      }
    })
    return record
  })

  return { headers, rows }
}
