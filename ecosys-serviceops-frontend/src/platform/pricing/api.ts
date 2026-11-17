import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

const BASE = '/api/platform/pricing'

export interface EditionPrice { code: string; name: string; factoryAnnualPrice: number }
export interface DiscountTier { minFactories: number; percent: number }
export interface HardwareItem { code: string; name: string; unit: string; unitCost: number; marginPercent: number; unitPrice?: number }

export interface PriceBook {
  editions: EditionPrice[]
  currency: string
  vatPercent: number
  visitorsFactoryAnnualPrice: number
  securityFactoryAnnualPrice: number
  securitySuiteFactoryAnnualPrice: number
  includedOfficeUsersPerFactory: number
  includedFieldUsersPerFactory: number
  extraUserMonthlyPrice: number
  discountTiers: DiscountTier[]
  implementationFeeMin: number
  implementationFeeMax: number
  implementationFeeDefault: number
  onPremisesMultiplier: number
  onPremisesSetupFee: number
  pilotMonths: number
  pilotFee: number
  pilotCreditedToYearOne: boolean
  hardware: HardwareItem[]
  quoteValidDays: number
  quoteTerms: string | null
  updatedAt: string
}

export interface HardwareRequest { code: string; quantity: number }

export interface QuoteTerms {
  edition: string | null
  visitors: boolean
  security: boolean
  factories: number
  extraOfficeUsers: number
  extraFieldUsers: number
  implementationFeePerFactory: number | null
  onPremises: boolean
  includePilot: boolean
  discountPercentOverride: number | null
  hardware: HardwareRequest[]
}

export type LineKind = 'Subscription' | 'Discount' | 'OneOff' | 'Hardware'
export interface PriceLine { kind: LineKind; description: string; quantity: number; unit: string; unitPrice: number; amount: number }

export interface PricingResult {
  currency: string
  lines: PriceLine[]
  annualSubscription: number
  monthlyEquivalent: number
  perFactoryPerMonth: number
  discountPercent: number
  discountAmount: number
  oneOffTotal: number
  yearOneTotal: number
  vatPercent: number
  yearOneVat: number
  yearOneTotalWithVat: number
  includedOfficeUsers: number
  includedFieldUsers: number
  pilot: { months: number; fee: number; creditedToYearOne: boolean; vat: number } | null
  warnings: string[]
}

export type QuoteStatus = 'Draft' | 'Sent' | 'Accepted' | 'Declined'
export const QUOTE_STATUSES: QuoteStatus[] = ['Draft', 'Sent', 'Accepted', 'Declined']

export interface QuoteRow {
  id: string
  quoteNumber: string
  customerName: string
  contactName: string | null
  tenantId: string | null
  tenantName: string | null
  edition: string | null
  visitors: boolean
  security: boolean
  factories: number
  includePilot: boolean
  status: QuoteStatus
  validUntil: string
  expired: boolean
  annualSubscription: number
  yearOneTotal: number
  preparedByName: string | null
  createdAt: string
}

export interface SaveQuote {
  customerName: string
  contactName: string | null
  contactEmail: string | null
  tenantId: string | null
  notes: string | null
  validUntil: string | null
  terms: QuoteTerms
}

export interface QuoteDetail { quote: QuoteRow; request: SaveQuote; result: PricingResult | null }

export type PricingBasis = 'PerSeat' | 'PerFactory'
export interface TenantFactoryPricing {
  tenantId: string
  pricingBasis: PricingBasis
  edition: string | null
  editionName: string | null
  visitors: boolean
  security: boolean
  licensedFactories: number | null
  factoriesInWorkspace: number
  factoryAnnualPriceOverride: number | null
  extraOfficeUsers: number
  extraFieldUsers: number
  officeSeats: number
  fieldSeats: number
  officeSeatsUsed: number
  fieldSeatsUsed: number
  billingCycle: string
  monthlyValue: number
  perFactoryPrice: PricingResult | null
  problem: string | null
}
export interface TenantFactoryTerms {
  pricingBasis: PricingBasis
  licensedFactories: number | null
  factoryAnnualPriceOverride: number | null
  extraOfficeUsers: number
  extraFieldUsers: number
}

export const EDITION_LABEL: Record<string, string> = { essentials: 'Essentials', professional: 'Professional', enterprise: 'Enterprise' }

export function scopeLabel(x: { edition: string | null; visitors: boolean; security: boolean }) {
  return [x.edition ? `Maintenance ${EDITION_LABEL[x.edition] ?? x.edition}` : null, x.visitors ? 'Visitors' : null, x.security ? 'Security' : null].filter(Boolean).join(' + ')
}

export function money(value: number, currency = 'KES') {
  return `${currency} ${Math.round(value).toLocaleString('en-KE')}`
}

const KEY = ['platform', 'pricing'] as const

export const usePriceBook = () => useQuery({ queryKey: [...KEY, 'book'], queryFn: () => api.get<PriceBook>(`${BASE}/price-book`) })

export function useSavePriceBook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (book: Omit<PriceBook, 'updatedAt'>) => api.put<PriceBook>(`${BASE}/price-book`, book),
    onSuccess: (book) => { queryClient.setQueryData([...KEY, 'book'], book); void queryClient.invalidateQueries({ queryKey: KEY }) },
  })
}

export const useQuotes = (status: string) => useQuery({ queryKey: [...KEY, 'quotes', status], queryFn: () => api.get<QuoteRow[]>(`${BASE}/quotes`, { status: status || undefined }) })
export const useQuote = (id: string | null) => useQuery({ queryKey: [...KEY, 'quote', id], queryFn: () => api.get<QuoteDetail>(`${BASE}/quotes/${id}`), enabled: Boolean(id) })

/** Live totals while a quote is built; kept quiet so a half-typed form does not raise error toasts. */
export const usePreview = (terms: QuoteTerms | null) => useQuery({
  queryKey: [...KEY, 'preview', terms],
  queryFn: () => api.post<PricingResult>(`${BASE}/preview`, terms, { suppressGlobalErrorToast: true }),
  enabled: terms !== null,
  placeholderData: (previous) => previous,
  retry: false,
})

function useQuoteMutation<TArgs>(fn: (args: TArgs) => Promise<QuoteDetail>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: (detail) => { queryClient.setQueryData([...KEY, 'quote', detail.quote.id], detail); void queryClient.invalidateQueries({ queryKey: [...KEY, 'quotes'] }) },
  })
}

export const useSaveQuote = () => useQuoteMutation(({ id, quote }: { id?: string | undefined; quote: SaveQuote }) =>
  id ? api.put<QuoteDetail>(`${BASE}/quotes/${id}`, quote) : api.post<QuoteDetail>(`${BASE}/quotes`, quote))
export const useQuoteStatus = () => useQuoteMutation(({ id, status }: { id: string; status: QuoteStatus }) => api.post<QuoteDetail>(`${BASE}/quotes/${id}/status`, { status }))
export const useCopyQuote = () => useQuoteMutation((id: string) => api.post<QuoteDetail>(`${BASE}/quotes/${id}/copy`))

export function useDeleteQuote() {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: (id: string) => api.delete(`${BASE}/quotes/${id}`), onSuccess: () => void queryClient.invalidateQueries({ queryKey: [...KEY, 'quotes'] }) })
}

export function useApplyQuote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<TenantFactoryPricing>(`${BASE}/quotes/${id}/apply`),
    onSuccess: (terms) => {
      queryClient.setQueryData([...KEY, 'tenant', terms.tenantId], terms)
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] })
    },
  })
}

export async function downloadQuotePdf(id: string, quoteNumber: string) {
  const response = await apiClient.get(`${BASE}/quotes/${id}/pdf`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${quoteNumber}.pdf`
  link.click()
  URL.revokeObjectURL(url)
}

export const useTenantPricing = (tenantId: string) =>
  useQuery({ queryKey: [...KEY, 'tenant', tenantId], queryFn: () => api.get<TenantFactoryPricing>(`${BASE}/tenants/${tenantId}`, undefined, { suppressGlobalErrorToast: true }) })

export function useSaveTenantPricing(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (terms: TenantFactoryTerms) => api.put<TenantFactoryPricing>(`${BASE}/tenants/${tenantId}`, terms),
    onSuccess: (result) => {
      queryClient.setQueryData([...KEY, 'tenant', tenantId], result)
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId] })
    },
  })
}

/** Blank terms for a new quote, from the price book's usual fee. */
export function newTerms(book: PriceBook | undefined): QuoteTerms {
  return {
    edition: 'professional', visitors: false, security: false, factories: 1, extraOfficeUsers: 0, extraFieldUsers: 0,
    implementationFeePerFactory: book?.implementationFeeDefault ?? null, onPremises: false, includePilot: false, discountPercentOverride: null, hardware: [],
  }
}
