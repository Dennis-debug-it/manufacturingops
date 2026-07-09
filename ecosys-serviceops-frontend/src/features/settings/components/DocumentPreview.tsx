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
  defaultTermsText: string | null
  defaultFooterNote: string | null
  bankName: string | null
  accountName: string | null
  accountNumber: string | null
  paybill: string | null
  paymentInstructions: string | null
  defaultCurrency: string
}

const sampleLines = [
  { description: 'Quarterly AHU preventive maintenance', qty: '1', rate: '18,500.00', total: '18,500.00' },
  { description: 'Air filter set (MERV 13)', qty: '4', rate: '2,150.00', total: '8,600.00' },
  { description: 'Call-out labour — senior technician', qty: '3', rate: '3,200.00', total: '9,600.00' },
]

const fontStack: Record<string, string> = {
  Helvetica: 'Helvetica, Arial, sans-serif',
  Arial: 'Arial, Helvetica, sans-serif',
  'Times New Roman': '"Times New Roman", Times, serif',
}

/** In-browser mock of the rendered invoice PDF, driven live by the template config. */
export function DocumentPreview({ config }: { config: DocumentPreviewConfig }) {
  const { tenant, user } = useAuth()
  const primary = config.reportPrimaryColor || '#0F4C81'
  const secondary = config.reportSecondaryColor || '#64748b'
  const compact = config.pageDensity === 'Compact'
  const pad = compact ? 'p-4' : 'p-6'
  const gap = compact ? 'space-y-3' : 'space-y-4'
  const companyName = tenant?.companyName ?? 'Your Company Ltd'
  const terms = config.invoiceTerms || config.defaultTermsText

  const logoBlock = config.showLogo ? (
    <div className="flex h-10 w-10 items-center justify-center rounded bg-slate-200 text-[9px] font-semibold text-slate-500">LOGO</div>
  ) : null

  const headerJustify = config.logoAlignment === 'Center' ? 'justify-center text-center' : config.logoAlignment === 'Right' ? 'justify-between' : 'justify-between'

  const bankLines = [
    config.bankName ? `Bank: ${config.bankName}` : null,
    config.accountName ? `Account Name: ${config.accountName}` : null,
    config.accountNumber ? `Account No: ${config.accountNumber}` : null,
    config.paybill ? `Paybill: ${config.paybill}` : null,
  ].filter(Boolean)

  return (
    <div className="relative overflow-hidden rounded-xl border border-slate-300 bg-white shadow-lg" style={{ fontFamily: fontStack[config.fontFamily] ?? fontStack.Helvetica }}>
      {config.watermarkText ? (
        <div aria-hidden className="pointer-events-none absolute inset-0 flex items-center justify-center">
          <span className="-rotate-30 select-none text-5xl font-bold uppercase tracking-widest text-slate-100" style={{ transform: 'rotate(-30deg)' }}>
            {config.watermarkText}
          </span>
        </div>
      ) : null}

      <div className={`relative ${pad} ${gap}`}>
        {/* Header */}
        <div className={`flex items-start gap-3 ${headerJustify}`}>
          {config.logoAlignment !== 'Right' ? logoBlock : null}
          {config.logoAlignment === 'Center' ? null : (
            <div className={config.logoAlignment === 'Right' ? '' : 'flex-1'}>
              {config.showCompanyName ? <div className="text-base font-bold" style={{ color: primary }}>{companyName}</div> : null}
              {config.showCompanyContact ? <div className="text-[10px] text-slate-500">+254 700 000 000 · info@company.co.ke · Nairobi, Kenya</div> : null}
            </div>
          )}
          <div className={config.logoAlignment === 'Center' ? 'text-center' : 'text-right'}>
            <div className="text-lg font-bold" style={{ color: primary }}>INVOICE</div>
            <div className="text-[10px] text-slate-500">INV-000123</div>
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

        {/* Bill-to strip */}
        <div className={`grid grid-cols-2 ${compact ? 'gap-2' : 'gap-3'} text-[10px]`}>
          <div className="rounded border border-slate-200 p-2">
            <div className="font-semibold text-slate-400">BILL TO</div>
            <div className="mt-0.5 font-semibold text-slate-800">Nairobi Hospital</div>
            <div className="text-slate-500">accounts@client.co.ke</div>
          </div>
          <div className="rounded border border-slate-200 p-2 text-slate-600">
            <div className="flex justify-between"><span className="text-slate-400">Invoice Date</span><span>08 Jul 2026</span></div>
            <div className="flex justify-between"><span className="text-slate-400">Due Date</span><span>07 Aug 2026</span></div>
            <div className="flex justify-between"><span className="text-slate-400">Currency</span><span>{config.defaultCurrency || 'KES'}</span></div>
          </div>
        </div>

        {/* Lines table */}
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

        {/* Totals */}
        <div className="ml-auto w-44 text-[10px]">
          <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">Subtotal</span><span>36,700.00</span></div>
          {config.showTaxBreakdown ? (
            <div className="flex justify-between border-b border-slate-100 p-1"><span className="text-slate-500">VAT 16%</span><span>5,872.00</span></div>
          ) : null}
          <div className="flex justify-between p-1 font-semibold text-white" style={{ backgroundColor: primary }}>
            <span>Balance Due</span><span>{config.defaultCurrency || 'KES'} 42,572.00</span>
          </div>
        </div>

        {/* Bank details */}
        {config.showBankDetails && (bankLines.length > 0 || config.paymentInstructions) ? (
          <div className="rounded border border-slate-200 p-2 text-[10px]">
            <div className="font-semibold" style={{ color: primary }}>Payment Details</div>
            {bankLines.map((line) => <div key={line} className="text-slate-600">{line}</div>)}
            {config.paymentInstructions ? <div className="mt-0.5 text-slate-500">{config.paymentInstructions}</div> : null}
          </div>
        ) : null}

        {/* Terms */}
        {config.showTerms && terms ? (
          <div className="text-[9px]">
            <div className="font-semibold text-slate-600">Terms & Conditions</div>
            <div className="text-slate-500">{terms}</div>
          </div>
        ) : null}

        {/* Signature / stamp */}
        {config.showSignatureBlock || config.showStampArea ? (
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
        ) : null}

        {/* Footer */}
        <div className="border-t border-slate-200 pt-1.5 text-[9px] text-slate-500">
          <div className="flex justify-between">
            <span>{companyName}</span>
            <span>Page 1 of 1</span>
          </div>
          {config.footerText || config.defaultFooterNote ? (
            <div className="mt-0.5" style={{ color: secondary }}>{config.footerText || config.defaultFooterNote}</div>
          ) : null}
        </div>
      </div>
    </div>
  )
}
