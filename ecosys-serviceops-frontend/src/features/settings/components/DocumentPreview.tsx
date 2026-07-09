import { useState, type ReactNode } from 'react'
import { useAuth } from '../../../shared/hooks/useAuth'

export interface DocumentPreviewConfig {
  fontFamily: string
  reportPrimaryColor: string | null
  reportSecondaryColor: string | null
  footerText: string | null
  watermarkText: string | null
  logoAlignment: 'Left' | 'Center' | 'Right'
  showCompanyName: boolean
  showCompanyContact: boolean
  showLogo: boolean
  showTaxBreakdown: boolean
  showBankDetails: boolean
  showSignatureBlock: boolean
  showTerms: boolean
  showStampArea: boolean
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

type DocType = 'invoice' | 'quotation' | 'receipt' | 'statement' | 'service-report'

const docTypes: Array<{ id: DocType; label: string }> = [
  { id: 'invoice', label: 'Invoice' },
  { id: 'quotation', label: 'Quotation' },
  { id: 'receipt', label: 'Receipt' },
  { id: 'statement', label: 'Statement' },
  { id: 'service-report', label: 'Service Report' },
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
  timestamps: 'Arrived 09:15 · Completed 11:40 · Duration 2h 25m',
  checklist: 'All checklist items passed (12 of 12).',
  materials: 'Air filter set (MERV 13) × 4 · Drive belt A-42 × 1',
  photos: '3 evidence photos attached.',
}

const fontStack: Record<string, string> = {
  Helvetica: 'Helvetica, Arial, sans-serif',
  Arial: 'Arial, Helvetica, sans-serif',
  'Times New Roman': '"Times New Roman", Times, serif',
}

/** In-browser mock of the rendered PDFs, driven live by the template config, with a document-type switcher. */
export function DocumentPreview({ config }: { config: DocumentPreviewConfig }) {
  const { tenant, user } = useAuth()
  const [docType, setDocType] = useState<DocType>('invoice')
  const primary = config.reportPrimaryColor || '#0F4C81'
  const secondary = config.reportSecondaryColor || '#64748b'
  const compact = config.pageDensity === 'Compact'
  const pad = compact ? 'p-4' : 'p-6'
  const gap = compact ? 'space-y-3' : 'space-y-4'
  const companyName = tenant?.companyName ?? 'Your Company Ltd'
  const currency = config.defaultCurrency || 'KES'

  const bankLines = [
    config.bankName ? `Bank: ${config.bankName}` : null,
    config.accountName ? `Account Name: ${config.accountName}` : null,
    config.accountNumber ? `Account No: ${config.accountNumber}` : null,
    config.paybill ? `Paybill: ${config.paybill}` : null,
  ].filter(Boolean) as string[]

  const logoBlock = config.showLogo ? (
    <div className="flex h-10 w-10 items-center justify-center rounded bg-slate-200 text-[9px] font-semibold text-slate-500">LOGO</div>
  ) : null

  const docTitles: Record<DocType, { title: string; number: string }> = {
    invoice: { title: 'INVOICE', number: 'INV-000123' },
    quotation: { title: 'QUOTATION', number: 'QT-000045' },
    receipt: { title: 'PAYMENT RECEIPT', number: 'REC-000012' },
    statement: { title: 'STATEMENT OF ACCOUNT', number: 'As at 09 Jul 2026' },
    'service-report': { title: 'SERVICE REPORT', number: 'WO-000078' },
  }

  const header = (
    <>
      <div className="flex items-start gap-3 justify-between">
        {config.logoAlignment !== 'Right' ? logoBlock : null}
        {config.logoAlignment === 'Center' ? null : (
          <div className={config.logoAlignment === 'Right' ? '' : 'flex-1'}>
            {config.showCompanyName ? <div className="text-base font-bold" style={{ color: primary }}>{companyName}</div> : null}
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
          {config.showCompanyName ? <div className="text-sm font-bold" style={{ color: primary }}>{companyName}</div> : null}
          {config.showCompanyContact ? <div className="text-[10px] text-slate-500">+254 700 000 000 · info@company.co.ke</div> : null}
        </div>
      ) : null}
      <div className="border-b-2" style={{ borderColor: primary }} />
    </>
  )

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

  const signatureBlock = config.showSignatureBlock || config.showStampArea ? (
    <div className="flex items-end justify-between pt-2 text-[9px] text-slate-500">
      {config.showSignatureBlock ? (
        <div>
          <div className="w-32 border-b border-slate-400 pb-4" />
          <div className="mt-0.5">Authorised Signature · {user?.fullName ?? 'Finance Manager'}</div>
        </div>
      ) : <div />}
      {config.showStampArea ? (
        <div className="flex h-14 w-14 items-center justify-center rounded-full border border-dashed border-slate-300 text-center text-[8px] leading-tight text-slate-400">Company<br />Stamp</div>
      ) : null}
    </div>
  ) : null

  const footer = (
    <div className="border-t border-slate-200 pt-1.5 text-[9px] text-slate-500">
      <div className="flex justify-between">
        <span>{companyName}</span>
        <span>Page 1 of 1</span>
      </div>
      {config.footerText || config.defaultFooterNote ? (
        <div className="mt-0.5" style={{ color: secondary }}>{config.footerText || config.defaultFooterNote}</div>
      ) : null}
    </div>
  )

  const linesTable = (
    <table className="w-full text-[10px]">
      <thead>
        <tr style={{ backgroundColor: primary }}>
          <th className="p-1.5 text-left font-semibold text-white">Description</th>
          <th className="p-1.5 text-right font-semibold text-white">Qty</th>
          <th className="p-1.5 text-right font-semibold text-white">Rate</th>
          <th className="p-1.5 text-right font-semibold text-white">Total</th>
        </tr>
      </thead>
      <tbody>
        {sampleLines.map((line, index) => (
          <tr key={line.description} className={index % 2 === 1 ? 'bg-slate-50' : ''}>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-slate-700`}>{line.description}</td>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right text-slate-700`}>{line.qty}</td>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right text-slate-700`}>{line.rate}</td>
            <td className={`border-b border-slate-100 ${compact ? 'p-1' : 'p-1.5'} text-right font-medium text-slate-900`}>{line.total}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )

  const totals = (finalLabel: string) => (
    <div className="ml-auto w-44 text-[10px]">
      <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">Subtotal</span><span>36,700.00</span></div>
      {config.showTaxBreakdown ? (
        <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">VAT 16%</span><span>5,872.00</span></div>
      ) : null}
      <div className="flex justify-between p-1 font-semibold text-white" style={{ backgroundColor: primary }}>
        <span>{finalLabel}</span><span>{currency} 42,572.00</span>
      </div>
    </div>
  )

  const invoiceBody = (
    <>
      <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
        <div className="rounded border border-slate-200 p-2">
          <div className="font-semibold text-slate-400">BILL TO</div>
          <div className="mt-0.5 font-semibold text-slate-800">Nairobi Hospital</div>
          <div className="text-slate-500">accounts@client.co.ke</div>
        </div>
        <div className="rounded border border-slate-200 p-2 text-slate-600">
          <div className="flex justify-between"><span className="text-slate-400">Invoice Date</span><span>08 Jul 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Due Date</span><span>07 Aug 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Currency</span><span>{currency}</span></div>
        </div>
      </div>
      {linesTable}
      {totals('Balance Due')}
      {bankBlock}
      {termsBlock(config.invoiceTerms || config.defaultTermsText)}
      {signatureBlock}
    </>
  )

  const quotationBody = (
    <>
      <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
        <div className="rounded border border-slate-200 p-2">
          <div className="font-semibold text-slate-400">PREPARED FOR</div>
          <div className="mt-0.5 font-semibold text-slate-800">Nairobi Hospital</div>
          <div className="text-slate-500">accounts@client.co.ke</div>
        </div>
        <div className="rounded border border-slate-200 p-2 text-slate-600">
          <div className="flex justify-between"><span className="text-slate-400">Quote Date</span><span>08 Jul 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Valid Until</span><span className="font-semibold" style={{ color: primary }}>07 Aug 2026</span></div>
          <div className="flex justify-between"><span className="text-slate-400">Currency</span><span>{currency}</span></div>
        </div>
      </div>
      {linesTable}
      {totals('Total')}
      {bankBlock}
      {termsBlock(config.quoteTerms || config.defaultTermsText)}
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
        <div key={section.id} className="rounded border p-2 text-[10px]" style={{ borderColor: `${primary}55` }}>
          <div className="font-semibold text-slate-800">{section.label}</div>
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
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-1">
        {docTypes.map((doc) => (
          <button
            key={doc.id}
            type="button"
            onClick={() => setDocType(doc.id)}
            className={`rounded-full px-2.5 py-1 text-[11px] font-medium transition-colors ${
              docType === doc.id ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            {doc.label}
          </button>
        ))}
      </div>

      <div className="relative overflow-hidden rounded-xl border border-slate-300 bg-white shadow-lg" style={{ fontFamily: fontStack[config.fontFamily] ?? fontStack.Helvetica }}>
        {config.watermarkText ? (
          <div aria-hidden className="pointer-events-none absolute inset-0 flex items-center justify-center">
            <span className="select-none text-5xl font-bold uppercase tracking-widest text-slate-100" style={{ transform: 'rotate(-30deg)' }}>
              {config.watermarkText}
            </span>
          </div>
        ) : null}

        <div className={`relative ${pad} ${gap}`}>
          {header}
          {bodies[docType]}
          {footer}
        </div>
      </div>
    </div>
  )
}
