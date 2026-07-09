import { api } from '../lib/api'
import type {
  AgingRow,
  ClientStatementResponse,
  FinanceDashboard,
  RevenueReportRow,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const financeReportsService = {
  getDashboard(signal?: AbortSignal) {
    return api.get<FinanceDashboard>('/api/finance/dashboard', { signal })
  },

  async getRevenueReport(params: {
    from?: string
    to?: string
    groupBy?: string
    signal?: AbortSignal
  }): Promise<RevenueReportRow[]> {
    const query: Record<string, string> = {}
    if (params.from) query.from = params.from
    if (params.to) query.to = params.to
    if (params.groupBy) query.groupBy = params.groupBy
    const response = await api.get<unknown>('/api/finance/reports/revenue', { signal: params.signal, query })
    return asArray<RevenueReportRow>(response)
  },

  async getAgingReport(signal?: AbortSignal): Promise<AgingRow[]> {
    const response = await api.get<unknown>('/api/finance/reports/aging', { signal })
    return asArray<AgingRow>(response)
  },

  getClientStatement(params: {
    clientId: string
    from?: string
    to?: string
    signal?: AbortSignal
  }) {
    const query: Record<string, string> = { clientId: params.clientId }
    if (params.from) query.from = params.from
    if (params.to) query.to = params.to
    return api.get<ClientStatementResponse>('/api/finance/reports/client-statement', { signal: params.signal, query })
  },
}
