import { describe, expect, it } from 'vitest'
import { readUrlFilters } from '../../shared/utils/urlFilters'
import { contractListFilterSpec, filterContracts } from '../contracts/listFilters'
import { invoiceListFilterSpec, invoiceListParams } from '../finance/invoiceListFilters'
import { filterReceipts, receiptListFilterSpec } from '../finance/receiptListFilters'
import { matchesRequestStage, requestStageFromParam, purchaseOrderChoice, purchaseOrderFiltersFor, purchaseOrderListFilterSpec } from '../inventory/listFilters'
import type { ContractListItem } from '../contracts/types'

/** Each dashboard link (RoleDashboardQueries.cs) read back by the list page it opens. */
const read = <K extends string>(link: string, spec: Parameters<typeof readUrlFilters<K>>[1]) =>
  readUrlFilters(new URL(link, 'https://app.test').searchParams, spec)

describe('dashboard links open filtered lists', () => {
  it('invoices: age, currency and status reach the API', () => {
    expect(invoiceListParams(read('/finance/invoices?age=overdue&currency=kes', invoiceListFilterSpec))).toEqual({ age: 'overdue', currency: 'KES' })
    expect(invoiceListParams(read('/finance/invoices?age=90-plus', invoiceListFilterSpec))).toEqual({ age: '90-plus' })
    expect(invoiceListParams(read('/finance/invoices?status=Draft', invoiceListFilterSpec))).toEqual({ status: 'Draft' })
    expect(invoiceListParams(read('/finance/invoices?age=someday&status=Lost', invoiceListFilterSpec))).toEqual({})
  })

  it('purchase orders: state and status are one choice', () => {
    expect(purchaseOrderChoice(read('/inventory/purchase-orders?state=late', purchaseOrderListFilterSpec))).toBe('late')
    expect(purchaseOrderChoice(read('/inventory/purchase-orders', purchaseOrderListFilterSpec))).toBe('All')
    expect(purchaseOrderFiltersFor('Sent')).toEqual({ state: '', status: 'Sent' })
    expect(purchaseOrderFiltersFor('open')).toEqual({ state: 'open', status: '' })
  })

  it('inventory requests: by stage', () => {
    const rows = [{ id: 'a', stage: 'Requested' }, { id: 'b', stage: 'Approved' }, { id: 'c', stage: 'Issued' }] as const
    const stage = requestStageFromParam(new URL('/inventory/requests?stage=Requested', 'https://app.test').searchParams.get('stage'))
    expect(rows.filter((row) => matchesRequestStage(row, stage)).map((x) => x.id)).toEqual(['a'])
    expect(rows.filter((row) => matchesRequestStage(row, requestStageFromParam(null))).map((x) => x.id)).toEqual(['a', 'b'])
    expect(requestStageFromParam('pending_approval')).toBe('open')
  })

  it('contracts: live ones ending within the window', () => {
    const contract = (id: string, daysToExpiry: number, status = 'Active') => ({ id, daysToExpiry, status }) as ContractListItem
    const rows = [contract('soon', 10), contract('later', 60), contract('ended', -2), contract('expired', 5, 'Expired')]
    expect(filterContracts(rows, read('/contracts?expiring=30', contractListFilterSpec).expiring).map((x) => x.id)).toEqual(['soon'])
    expect(filterContracts(rows, '90').map((x) => x.id)).toEqual(['soon', 'later'])
  })

  it('receipts: confirmed this month', () => {
    const rows = [
      { id: 'old', status: 'Confirmed', paymentDate: '2026-09-30T00:00:00Z' },
      { id: 'new', status: 'Confirmed', paymentDate: '2026-10-02T00:00:00Z' },
      { id: 'reversed', status: 'Reversed', paymentDate: '2026-10-03T00:00:00Z' },
    ]
    const filters = read('/finance/receipts?status=Confirmed&from=2026-10-01', receiptListFilterSpec)
    expect(filterReceipts(rows, filters).map((x) => x.id)).toEqual(['new'])
    expect(filterReceipts(rows, { status: '', from: 'garbage' })).toHaveLength(3)
  })
})
