export interface ImportPreviewRow {
  rowNumber: number
  rawValues: Record<string, string | null>
  isValid: boolean
  errors: string[]
}

export interface ImportPreviewResponse {
  importType: string
  totalRows: number
  rows: ImportPreviewRow[]
}

export interface ImportCommitRowError {
  rowNumber: number
  errors: string[]
}

export interface ImportCommitResponse {
  totalRows: number
  successfulRows: number
  failedRows: number
  skippedRows: number
  rowErrors?: ImportCommitRowError[]
}
