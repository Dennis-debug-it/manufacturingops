import { api } from '../lib/api'
import type { KipQueryInput, KipQueryResponse } from '../types/api'

export const kipService = {
  query(input: KipQueryInput) {
    return api.post<KipQueryResponse>('/api/kip/query', input)
  },
}
