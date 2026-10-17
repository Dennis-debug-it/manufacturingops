import { useState, type ReactNode } from 'react'
import { useAuth } from '../../../shared/hooks/useAuth'
import { getActiveCurrency } from '../../../shared/utils/currency'

export interface DocumentPreviewConfig {
  fontFamily: string
  reportPrimaryColor: string | null
  reportSecondaryColor: string | null
  footerText: string | null
  watermarkText: string | null
  watermarkColor?: string | null
  watermarkOpacity?: number
  watermarkPosition?: string
  logoAlignment: 'Left' | 'Center' | 'Right'
  logoWidth?: number
  companyNameFontSize?: number
  headerBackgroundColor?: string | null
  bodyFontSize?: number
  lineSpacing?: number
  introText?: string | null
  showCompanyName: boolean
  showCompanyContact: boolean
  showLogo: boolean
  showTaxBreakdown: boolean
  showBankDetails: boolean
  showSignatureBlock: boolean
  showTerms: boolean
  showStampArea: boolean
  showPageNumbers?: boolean
  pageNumberPosition?: string
  showPoweredByEcosys?: boolean
  currencyDisplay?: string
  signatureFields?: string[]
  pageDensity: string
  invoiceTerms: string | null
  quoteTerms?: string | null
  serviceReportDisclaimer?: string | null
  sections?: Array<{ id: string; label: string; enabled: boolean; order: number }>
  defaultTermsText: string | null
  defaultFooterNote: string | null
  bankName: string | null
  accountName: string | null
  accountNumber: string | null
  paybill: string | null
  paymentInstructions: string | null
  defaultCurrency: string
}

export type DocType = 'invoice' | 'quotation' | 'receipt' | 'statement' | 'service-report' | 'purchase-order'

const docTypes: Array<{ id: DocType; label: string }> = [
  { id: 'invoice', label: 'Invoice' },
  { id: 'quotation', label: 'Quotation' },
  { id: 'receipt', label: 'Receipt' },
  { id: 'statement', label: 'Statement' },
  { id: 'service-report', label: 'Service Report' },
  { id: 'purchase-order', label: 'Purchase Order' },
]

const sampleLines = [
  { description: 'Quarterly AHU preventive maintenance', qty: '1', rate: '18,500.00', total: '18,500.00' },
  { description: 'Air filter set (MERV 13)', qty: '4', rate: '2,150.00', total: '8,600.00' },
  { description: 'Call-out labour — senior technician', qty: '3', rate: '3,200.00', total: '9,600.00' },
]

const sampleReportSections: Record<string, string> = {
  'service-details': 'Client: Nairobi Hospital · Site: Main Campus, Nairobi',
  problem: 'AHU-03 tripping on high static pressure alarm; airflow to Ward B reduced.',
  findings: 'Filters heavily loaded; belt tension low; pressure sensor within calibration.',
  'work-done': 'Replaced filter set, re-tensioned belt, verified static pressure at 320 Pa.',
  checklist: 'All checklist items passed (12 of 12).',
  materials: 'Air filter set (MERV 13) × 4 · Drive belt A-42 × 1',
  photos: '3 evidence photos attached.',
}

const fontStack: Record<string, string> = {
  Helvetica: 'Helvetica, Arial, sans-serif',
  Arial: 'Arial, Helvetica, sans-serif',
  'Times New Roman': '"Times New Roman", Times, serif',
}

const currencySymbols: Record<string, string> = {
  KES: 'KSh', USD: '$', EUR: '€', GBP: '£', UGX: 'USh', TZS: 'TSh', NGN: '₦', ZAR: 'R',
}

interface DocumentPreviewProps {
  config: DocumentPreviewConfig
  /** When provided, the preview renders this type and hides its own switcher (the page owns selection). */
  docType?: DocType
  onDocTypeChange?: (docType: DocType) => void
}

/** In-browser mock of the rendered PDFs, driven live by the template config. */
export function DocumentPreview({ config, docType: controlledDocType, onDocTypeChange }: DocumentPreviewProps) {
  const { tenant, user } = useAuth()
  const [internalDocType, setInternalDocType] = useState<DocType>('invoice')
  const docType = controlledDocType ?? internalDocType
  const setDocType = (next: DocType) => (onDocTypeChange ? onDocTypeChange(next) : setInternalDocType(next))
  const showSwitcher = controlledDocType === undefined

  const primary = config.reportPrimaryColor || '#0F4C81'
  const secondary = config.reportSecondaryColor || '#64748b'
  const compact = config.pageDensity === 'Compact'
  const pad = compact ? 'p-4' : 'p-6'
  const gap = compact ? 'space-y-3' : 'space-y-4'
  const companyName = tenant?.companyName ?? 'Your Company Ltd'
  const companyNameSize = config.companyNameFontSize ?? 18
  const bodyFontSize = config.bodyFontSize ?? 10
  const headerBg = config.headerBackgroundColor || undefined
  // The tenant's own Document Templates setting, which is the one place this platform stores a
  // configured currency. Blank until an admin fills it in — then the workspace currency stands in.
  const cur = (config.defaultCurrency || getActiveCurrency()).toUpperCase()
  const currency = config.currencyDisplay === 'Symbol' ? currencySymbols[cur] ?? cur : cur

  const bankLines = [
    config.bankName ? `Bank: ${config.bankName}` : null,
    config.accountName ? `Account Name: ${config.accountName}` : null,
    config.accountNumber ? `Account No: ${config.accountNumber}` : null,
    config.paybill ? `Paybill: ${config.paybill}` : null,
  ].filter(Boolean) as string[]

  const logoBlock = config.showLogo ? (
    <div
      className="flex items-center justify-center rounded bg-slate-200 text-[9px] font-semibold text-slate-500"
      style={{ width: (config.logoWidth ?? 40) / 1.6, height: (config.logoWidth ?? 40) / 1.6 }}
    >
      LOGO
    </div>
  ) : null

  const docTitles: Record<DocType, { title: string; number: string }> = {
    invoice: { title: 'INVOICE', number: 'INV-000123' },
    quotation: { title: 'QUOTATION', number: 'QT-000045' },
    receipt: { title: 'PAYMENT RECEIPT', number: 'REC-000012' },
    statement: { title: 'STATEMENT OF ACCOUNT', number: 'As at 09 Jul 2026' },
    'service-report': { title: 'SERVICE REPORT', number: 'WO-000078' },
    'purchase-order': { title: 'PURCHASE ORDER', number: 'PO-000078' },
  }

  const header = (
    <div className="space-y-2" style={headerBg ? { backgroundColor: headerBg, margin: '-4px', padding: '8px', borderRadius: 6 } : undefined}>
      <div className="flex items-start gap-3 justify-between">
        {config.logoAlignment !== 'Right' ? logoBlock : null}
        {config.logoAlignment === 'Center' ? null : (
          <div className={config.logoAlignment === 'Right' ? '' : 'flex-1'}>
            {config.showCompanyName ? <div className="font-bold" style={{ color: primary, fontSize: companyNameSize }}>{companyName}</div> : null}
            {config.showCompanyContact ? <div className="text-[10px] text-slate-500">+254 700 000 000 · info@company.co.ke · Nairobi, Kenya</div> : null}
          </div>
        )}
        <div className={config.logoAlignment === 'Center' ? 'text-center' : 'text-right'}>
          <div className="text-lg font-bold" style={{ color: primary }}>{docTitles[docType].title}</div>
          <div className="text-[10px] text-slate-500">{docTitles[docType].number}</div>
        </div>
        {config.logoAlignment === 'Right' ? logoBlock : null}
      </div>
      {config.logoAlignment === 'Center' && (config.showCompanyName || config.showCompanyContact) ? (
        <div className="text-center">
          {config.showCompanyName ? <div className="font-bold" style={{ color: primary, fontSize: companyNameSize * 0.8 }}>{companyName}</div> : null}
          {config.showCompanyContact ? <div className="text-[10px] text-slate-500">+254 700 000 000 · info@company.co.ke</div> : null}
        </div>
      ) : null}
      <div className="border-b-2" style={{ borderColor: primary }} />
    </div>
  )

  const introBlock = config.introText ? (
    <div className="text-[10px] text-slate-600">{config.introText}</div>
  ) : null

  const bankBlock = config.showBankDetails && (bankLines.length > 0 || config.paymentInstructions) ? (
    <div className="rounded border border-slate-200 p-2 text-[10px]">
      <div className="font-semibold" style={{ color: primary }}>Payment Details</div>
      {bankLines.map((line) => <div key={line} className="text-slate-600">{line}</div>)}
      {config.paymentInstructions ? <div className="mt-0.5 text-slate-500">{config.paymentInstructions}</div> : null}
    </div>
  ) : null

  const termsBlock = (terms: string | null) =>
    config.showTerms && terms ? (
      <div className="text-[9px]">
        <div className="font-semibold text-slate-600">Terms & Conditions</div>
        <div className="text-slate-500">{terms}</div>
      </div>
    ) : null

  const signatureLabels = config.signatureFields && config.signatureFields.length > 0
    ? config.signatureFields
    : ['Authorised Signature']

  const signatureBlock = config.showSignatureBlock || config.showStampArea ? (
    <div className="flex items-end justify-between gap-4 pt-2 text-[9px] text-slate-500">
      {config.showSignatureBlock ? (
        <div className="flex flex-1 flex-wrap gap-4">
          {signatureLabels.map((label, i) => (
            <div key={`${label}-${i}`}>
              <div className="w-28 border-b border-slate-400 pb-4" />
              <div className="mt-0.5">{label}{i === 0 ? ` · ${user?.fullName ?? 'Finance Manager'}` : ''}</div>
            </div>
          ))}
        </div>
      ) : <div />}
      {config.showStampArea ? (
        <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full border border-dashed border-slate-300 text-center text-[8px] leading-tight text-slate-400">Company<br />Stamp</div>
      ) : null}
    </div>
  ) : null

  const pageNumAlign = config.pageNumberPosition === 'Left' ? 'text-left' : config.pageNumberPosition === 'Center' ? 'text-center' : 'text-right'

  const footer = (
    <div className="border-t border-slate-200 pt-1.5 text-[9px] text-slate-500">
      <div className="flex items-center justify-between gap-2">
        <span className="flex-1">{companyName}</span>
        {config.showPageNumbers !== false ? <span className={`flex-1 ${pageNumAlign}`}>Page 1 of 1</span> : null}
      </div>
      {config.footerText || config.defaultFooterNote ? (
        <div className="mt-0.5 italic" style={{ color: secondary }}>{config.footerText || config.defaultFooterNote}</div>
      ) : null}
      {config.showPoweredByEcosys !== false ? (
        <div className="mt-0.5 text-slate-300">Powered by Ecosys</div>
      ) : null}
    </div>
  )

  const linesTable = (short?: boolean) => (
    <table className="w-full text-[10px]">
      <thead>
        <tr style={{ backgroundColor: primary }}>
          <th className="p-1.5 text-left font-semibold text-white">Description</th>
          <th className="p-1.5 text-right font-semibold text-white">Qty</th>
          <th className="p-1.5 text-right font-semibold text-white">Rate</th>
          {!short ? <th className="p-1.5 text-right font-semibold text-white">Tax %</th> : null}
          <th className="p-1.5 text-right font-semibold text-white">Total</th>
        </tr>
      </thead>
      <tbody>
        {sampleLines.map((line, index) => (
          <tr key={line.description} className={index % 2 === 1 ? 'bg-slate-50' : ''}>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-slate-700`}>{line.description}</td>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right text-slate-700`}>{line.qty}</td>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right text-slate-700`}>{line.rate}</td>
            {!short ? <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right text-slate-700`}>16%</td> : null}
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right font-medium text-slate-900`}>{line.total}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )

  const totals = (finalLabel: string) => (
    <div className="ml-auto w-44 text-[10px]">
      <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">Subtotal</span><span>{currency} 36,700.00</span></div>
      {config.showTaxBreakdown ? (
        <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">VAT 16%</span><span>{currency} 5,872.00</span></div>
      ) : null}
      <div className="flex justify-between p-1 font-semibold text-white" style={{ backgroundColor: primary }}>
        <span>{finalLabel}</span><span>{currency} 42,572.00</span>
      </div>
    </div>
  )

  const partyStrip = (leftLabel: string, rightRows: Array<[string, string, boolean?]>) => (
    <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
      <div className="rounded border border-slate-200 p-2">
        <div className="font-semibold text-slate-400">{leftLabel}</div>
        <div className="mt-0.5 font-semibold text-slate-800">Nairobi Hospital</div>
        <div className="text-slate-500">accounts@client.co.ke</div>
      </div>
      <div className="rounded border border-slate-200 p-2 text-slate-600">
        {rightRows.map(([label, value, highlight]) => (
          <div key={label} className="flex justify-between">
            <span className="text-slate-400">{label}</span>
            <span className={highlight ? 'font-semibold' : ''} style={highlight ? { color: primary } : undefined}>{value}</span>
          </div>
        ))}
      </div>
    </div>
  )

  const invoiceBody = (
    <>
      {partyStrip('BILL TO', [['Invoice Date', '08 Jul 2026'], ['Due Date', '07 Aug 2026'], ['Currency', cur]])}
      {linesTable()}
      {totals('Balance Due')}
      {bankBlock}
      {termsBlock(config.invoiceTerms || config.defaultTermsText)}
      {signatureBlock}
    </>
  )

  const quotationBody = (
    <>
      {partyStrip('PREPARED FOR', [['Quote Date', '08 Jul 2026'], ['Valid Until', '07 Aug 2026', true], ['Currency', cur]])}
      {linesTable()}
      {totals('Total')}
      {bankBlock}
      {termsBlock(config.quoteTerms || config.defaultTermsText)}
      {signatureBlock}
    </>
  )

  const purchaseOrderBody = (
    <>
      <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
        <div className="rounded border border-slate-200 p-2">
          <div className="font-semibold text-slate-400">SUPPLIER</div>
          <div className="mt-0.5 font-semibold text-slate-800">Coastline HVAC Supplies Ltd</div>
          <div className="text-slate-500">sales@coastlinehvac.co.ke</div>
        </div>
        <div className="rounded border border-slate-200 p-2 text-slate-600">
          <div className="flex justify-between"><span className="text-slate-400">Order Date</span><span>09 Jul 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Expected</span><span className="font-semibold" style={{ color: primary }}>23 Jul 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Currency</span><span>{cur}</span></div>
        </div>
      </div>
      {linesTable(true)}
      {totals('Total')}
      {termsBlock(config.defaultTermsText)}
      {signatureBlock}
    </>
  )

  const receiptBody = (
    <>
      <div className="rounded border border-slate-200 p-3 text-[10px]">
        {[
          ['Receipt No.', 'REC-000012'],
          ['Invoice Ref.', 'INV-000123'],
          ['Client', 'Nairobi Hospital'],
          ['Payment Date', '09 Jul 2026'],
          ['Method', 'MPesa'],
          ['Reference', 'QX12ABC34'],
        ].map(([label, value]) => (
          <div key={label} className="flex justify-between border-b border-slate-100 py-1 last:border-b-0">
            <span className="font-semibold text-slate-400">{label}</span>
            <span className="text-slate-700">{value}</span>
          </div>
        ))}
      </div>
      <div className="py-2 text-center">
        <div className="text-[10px] text-slate-500">Amount Received</div>
        <div className="text-2xl font-bold" style={{ color: primary }}>{currency} 42,572.00</div>
        <div className="mt-1 text-[10px] italic text-slate-400">Thank you for your payment!</div>
      </div>
    </>
  )

  const statementBody = (
    <>
      <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
        <div className="rounded border border-slate-200 p-2">
          <div className="font-semibold text-slate-400">CLIENT</div>
          <div className="mt-0.5 font-semibold text-slate-800">Nairobi Hospital</div>
          <div className="text-slate-500">accounts@client.co.ke</div>
        </div>
        <div className="rounded border border-slate-200 p-2 text-slate-600">
          <div className="flex justify-between"><span className="text-slate-400">Period</span><span>09 Jan – 09 Jul 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Total Invoiced</span><span>{currency} 128,400.00</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Total Paid</span><span>{currency} 85,828.00</span></div>
        </div>
      </div>
      <div className="flex items-center justify-between rounded border p-2 text-[11px] font-bold" style={{ borderColor: primary, color: primary, backgroundColor: `${primary}14` }}>
        <span>BALANCE DUE</span>
        <span>{currency} 42,572.00</span>
      </div>
      <table className="w-full text-[10px]">
        <thead>
          <tr style={{ backgroundColor: primary }}>
            <th className="p-1.5 text-left font-semibold text-white">Date</th>
            <th className="p-1.5 text-left font-semibold text-white">Invoice</th>
            <th className="p-1.5 text-right font-semibold text-white">Amount</th>
            <th className="p-1.5 text-right font-semibold text-white">Balance</th>
          </tr>
        </thead>
        <tbody>
          {[
            ['08 Jun 26', 'INV-000119', '85,828.00', '0.00'],
            ['08 Jul 26', 'INV-000123', '42,572.00', '42,572.00'],
          ].map(([date, number, amount, balance], index) => (
            <tr key={number} className={index % 2 === 1 ? 'bg-slate-50' : ''}>
              <td className="border-b border-slate-100 p-1.5 text-slate-700">{date}</td>
              <td className="border-b border-slate-100 p-1.5 font-medium text-slate-800">{number}</td>
              <td className="border-b border-slate-100 p-1.5 text-right text-slate-700">{amount}</td>
              <td className="border-b border-slate-100 p-1.5 text-right font-medium text-slate-900">{balance}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  )

  const enabledSections = (config.sections ?? [])
    .filter((section) => section.enabled)
    .sort((a, b) => a.order - b.order)

  const serviceReportBody = (
    <>
      <div className="flex items-baseline justify-between text-[10px]">
        <div className="font-bold text-slate-800">WO-000078 | AHU-03 high static pressure</div>
        <div className="text-slate-400">Generated: 09 Jul 2026</div>
      </div>
      {(enabledSections.length > 0 ? enabledSections : [{ id: 'service-details', label: 'Service Details', enabled: true, order: 0 }]).map((section) => (
        <div key={section.id} className="text-[10px]">
          <div className="border-b pb-0.5 font-semibold text-slate-800" style={{ borderColor: secondary }}>{section.label}</div>
          <div className="mt-0.5 text-slate-600">{sampleReportSections[section.id] ?? 'Sample content for this section.'}</div>
        </div>
      ))}
      {signatureBlock}
      {config.serviceReportDisclaimer ? (
        <div className="text-[9px] italic text-slate-400">{config.serviceReportDisclaimer}</div>
      ) : null}
    </>
  )

  const bodies: Record<DocType, ReactNode> = {
    invoice: invoiceBody,
    quotation: quotationBody,
    receipt: receiptBody,
    statement: statementBody,
    'service-report': serviceReportBody,
    'purchase-order': purchaseOrderBody,
  }

  // Watermark tint: blend the chosen colour toward white per the opacity setting.
  const watermarkColor = (() => {
    const base = config.watermarkColor || '#9CA3AF'
    const opacity = Math.min(60, Math.max(5, config.watermarkOpacity ?? 12)) / 100
    const hex = base.replace('#', '')
    if (hex.length !== 6) return '#E5E7EB'
    const mix = (c: number) => Math.round(c * opacity + 255 * (1 - opacity))
    const r = mix(parseInt(hex.slice(0, 2), 16))
    const g = mix(parseInt(hex.slice(2, 4), 16))
    const b = mix(parseInt(hex.slice(4, 6), 16))
    return `rgb(${r}, ${g}, ${b})`
  })()

  const watermarkTransform = config.watermarkPosition === 'Top' || config.watermarkPosition === 'Bottom' ? 'none' : 'rotate(-30deg)'
  const watermarkAlign = config.watermarkPosition === 'Top' ? 'items-start pt-8' : config.watermarkPosition === 'Bottom' ? 'items-end pb-8' : 'items-center'

  return (
    <div className="space-y-2">
      {showSwitcher ? (
        <div className="flex flex-wrap gap-1">
          {docTypes.map((doc) => (
            <button
              key={doc.id}
              type="button"
              onClick={() => setDocType(doc.id)}
              className={`rounded-full px-2.5 py-1 text-[11px] font-medium transition-colors ${
                docType === doc.id ? 'bg-[var(--app-primary)] text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              {doc.label}
            </button>
          ))}
        </div>
      ) : null}

      <div
        className="relative overflow-hidden rounded-xl border border-slate-300 bg-white shadow-lg"
        style={{ fontFamily: fontStack[config.fontFamily] ?? fontStack.Helvetica, fontSize: bodyFontSize }}
      >
        {config.watermarkText ? (
          <div aria-hidden className={`pointer-events-none absolute inset-0 flex justify-center ${watermarkAlign}`}>
            <span className="select-none text-5xl font-bold uppercase tracking-widest" style={{ transform: watermarkTransform, color: watermarkColor }}>
              {config.watermarkText}
            </span>
          </div>
        ) : null}

        <div className={`relative ${pad} ${gap}`}>
          {header}
          {introBlock}
          {bodies[docType]}
          {footer}
        </div>
      </div>
    </div>
  )
}
