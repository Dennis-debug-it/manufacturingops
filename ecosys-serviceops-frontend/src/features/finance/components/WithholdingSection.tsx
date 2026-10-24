import { useState } from 'react'
import toast from 'react-hot-toast'
import { format } from 'date-fns'
import { Undo2 } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { formatDate } from '../../../shared/utils/dates'
import { formatCurrency } from '../../../shared/utils/format'
import { useInvoiceWithholding, useRecordWithholding, useReverseWithholding } from '../../projects/siteApi'
import type { WithholdingCertificate } from '../../projects/types'

type TaxType = WithholdingCertificate['taxType']

const taxTypeLabels: Record<TaxType, string> = { VAT: 'VAT', IncomeTax: 'Income tax' }
/** KRA withholding VAT is 2%; income tax rates vary by service, so no default there. */
const defaultRates: Record<TaxType, string> = { VAT: '2', IncomeTax: '' }

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export interface WithholdingSectionProps {
  invoiceId: string
  currency: string
  balance: number
  /** Draft, Void and proforma invoices can't take a certificate. */
  canRecordOnInvoice: boolean
  /** Same check the page uses for recording payments. */
  canManage: boolean
}

/** Tax a client withheld and covered with a KRA certificate. It settles part of the invoice like a payment. */
export function WithholdingSection({ invoiceId, currency, balance, canRecordOnInvoice, canManage }: WithholdingSectionProps) {
  const { data: certificates = [], isLoading } = useInvoiceWithholding(invoiceId)
  const recordWithholding = useRecordWithholding(invoiceId)
  const reverseWithholding = useReverseWithholding()

  const [showRecord, setShowRecord] = useState(false)
  const [taxType, setTaxType] = useState<TaxType>('VAT')
  const [ratePercent, setRatePercent] = useState(defaultRates.VAT)
  const [amount, setAmount] = useState('')
  const [certificateNumber, setCertificateNumber] = useState('')
  const [certificateDate, setCertificateDate] = useState(() => format(new Date(), 'yyyy-MM-dd'))
  const [notes, setNotes] = useState('')
  const [toReverse, setToReverse] = useState<WithholdingCertificate | null>(null)
  const [reverseReason, setReverseReason] = useState('')

  const resetForm = () => {
    setTaxType('VAT')
    setRatePercent(defaultRates.VAT)
    setAmount('')
    setCertificateNumber('')
    setCertificateDate(format(new Date(), 'yyyy-MM-dd'))
    setNotes('')
  }

  const submit = async () => {
    const value = Number(amount)
    if (!Number.isFinite(value) || value <= 0) {
      toast.error('Enter a valid amount')
      return
    }
    if (value > balance) {
      toast.error(`Amount can't be more than the outstanding balance (${formatCurrency(balance, currency)})`)
      return
    }
    const rate = ratePercent.trim() === '' ? null : Number(ratePercent)
    if (rate !== null && (!Number.isFinite(rate) || rate <= 0 || rate > 100)) {
      toast.error('Enter a rate between 0 and 100')
      return
    }
    if (!certificateNumber.trim()) {
      toast.error('Enter the certificate number')
      return
    }
    if (!certificateDate) {
      toast.error('Enter the certificate date')
      return
    }
    try {
      await recordWithholding.mutateAsync({
        taxType,
        ratePercent: rate,
        amount: value,
        certificateNumber: certificateNumber.trim(),
        certificateDate,
        notes: notes.trim() || null,
      })
      toast.success('Withholding certificate recorded')
      setShowRecord(false)
      resetForm()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const closeReverse = () => {
    setToReverse(null)
    setReverseReason('')
  }

  const columns: DataTableColumn<WithholdingCertificate>[] = [
    { key: 'taxType', header: 'Type', cell: (row) => taxTypeLabels[row.taxType] ?? row.taxType },
    { key: 'ratePercent', header: 'Rate', cell: (row) => (row.ratePercent == null ? '-' : `${row.ratePercent}%`) },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount, row.currency || currency) },
    { key: 'certificateNumber', header: 'Certificate', cell: (row) => row.certificateNumber },
    { key: 'certificateDate', header: 'Date', cell: (row) => formatDate(row.certificateDate) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) =>
        canManage && row.status === 'Confirmed' ? (
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => setToReverse(row)}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-rose-200 bg-white px-3 text-xs font-medium text-rose-600 transition hover:bg-rose-50"
            >
              <Undo2 className="h-3.5 w-3.5" />
              Reverse
            </button>
          </div>
        ) : null,
    },
  ]

  return (
    <>
      <SummarySection title="Tax withheld" description="Withholding certificates from the client. Each one settles part of the invoice like a payment.">
        {canManage && canRecordOnInvoice && balance > 0 ? (
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => setShowRecord(true)}
              className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Record withholding
            </button>
          </div>
        ) : null}
        <DataTable
          columns={columns}
          data={certificates}
          loading={isLoading}
          emptyTitle="No tax withheld"
          emptyDescription="Withholding certificates recorded against this invoice will appear here."
        />
      </SummarySection>

      <ConfirmDialog
        open={Boolean(toReverse)}
        title="Reverse withholding certificate"
        message={`Reverse certificate ${toReverse?.certificateNumber ?? ''} of ${formatCurrency(toReverse?.amount ?? 0, toReverse?.currency || currency)}? The invoice balance will be recalculated.`}
        confirmLabel="Reverse"
        danger
        confirmDisabled={!reverseReason.trim() || reverseWithholding.isPending}
        onCancel={closeReverse}
        onConfirm={async () => {
          if (!toReverse) return
          try {
            await reverseWithholding.mutateAsync({ id: toReverse.id, reason: reverseReason.trim() })
            toast.success('Withholding certificate reversed')
            closeReverse()
          } catch {
            // interceptor surfaces the error toast
          }
        }}
      >
        <div className="mt-4">
          <label className={labelClass} htmlFor="withholding-reverse-reason">Reason</label>
          <textarea
            id="withholding-reverse-reason"
            value={reverseReason}
            onChange={(event) => setReverseReason(event.target.value)}
            rows={2}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>
      </ConfirmDialog>

      <FormDrawer
        open={showRecord}
        title="Record withholding"
        onClose={() => setShowRecord(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowRecord(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button type="button" onClick={submit} disabled={recordWithholding.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {recordWithholding.isPending ? 'Saving...' : 'Record withholding'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass} htmlFor="withholding-tax-type">Tax type</label>
              <select
                id="withholding-tax-type"
                value={taxType}
                onChange={(event) => {
                  const next = event.target.value as TaxType
                  setTaxType(next)
                  setRatePercent(defaultRates[next])
                }}
                className={inputClass}
              >
                {(Object.keys(taxTypeLabels) as TaxType[]).map((option) => (
                  <option key={option} value={option}>
                    {taxTypeLabels[option]}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="withholding-rate">Rate %</label>
              <input id="withholding-rate" type="number" step="0.01" min="0" max="100" value={ratePercent} onChange={(event) => setRatePercent(event.target.value)} className={inputClass} />
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="withholding-amount">Amount</label>
            <input id="withholding-amount" type="number" step="0.01" min="0" value={amount} onChange={(event) => setAmount(event.target.value)} className={inputClass} />
            <p className="mt-1 text-xs text-slate-500">Outstanding balance: {formatCurrency(balance, currency)}</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass} htmlFor="withholding-certificate-number">Certificate number</label>
              <input id="withholding-certificate-number" value={certificateNumber} onChange={(event) => setCertificateNumber(event.target.value)} className={inputClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="withholding-certificate-date">Certificate date</label>
              <input id="withholding-certificate-date" type="date" value={certificateDate} onChange={(event) => setCertificateDate(event.target.value)} className={inputClass} />
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="withholding-notes">Notes</label>
            <textarea id="withholding-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </>
  )
}
