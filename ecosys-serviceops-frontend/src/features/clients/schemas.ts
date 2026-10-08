import { z } from 'zod'

export const clientSchema = z.object({
  clientName: z.string().min(1, 'Client name is required').max(200),
  clientType: z.string().max(100).optional(),
  email: z.string().email('Enter a valid email').optional().or(z.literal('')),
  phone: z.string().max(30).optional(),
  location: z.string().max(200).optional(),
  contactPerson: z.string().max(120).optional(),
  contactPhone: z.string().max(30).optional(),
  notes: z.string().max(2000).optional(),
})

export type ClientFormValues = z.infer<typeof clientSchema>

export const siteSchema = z.object({
  siteName: z.string().min(1, 'Site name is required').max(200),
  siteType: z.enum(['Branch', 'HQ', 'Data Centre', 'Warehouse', 'Factory', 'Other']).optional(),
  streetAddress: z.string().max(200).optional(),
  townCity: z.string().max(100).optional(),
  county: z.string().max(100).optional(),
  country: z.string().max(100).optional(),
  contactPerson: z.string().max(120).optional(),
  contactPhone: z.string().max(30).optional(),
  contactEmail: z.string().email('Enter a valid email').optional().or(z.literal('')),
  operatingHours: z.string().max(120).optional(),
  accessNotes: z.string().max(1000).optional(),
})

export type SiteFormValues = z.infer<typeof siteSchema>

export const assetSchema = z.object({
  clientId: z.string().min(1, 'Client is required'),
  siteId: z.string().optional(),
  assetName: z.string().min(1, 'Asset name is required').max(200),
  assetCode: z.string().max(100).optional(),
  assetType: z.string().max(100).optional(),
  location: z.string().max(200).optional(),
  serialNumber: z.string().max(120).optional(),
  manufacturer: z.string().max(120).optional(),
  model: z.string().max(120).optional(),
  installationDate: z.string().optional(),
  pmFrequencyValue: z.string().optional(),
  pmFrequencyUnit: z.string().optional(),
  lastPmDate: z.string().optional(),
  autoSchedulePm: z.boolean(),
  hasWarranty: z.boolean(),
  warrantyStartDate: z.string().optional(),
  warrantyPeriodMonths: z.string().optional(),
  warrantyExpiryDate: z.string().optional(),
  notes: z.string().max(2000).optional(),
}).superRefine((values, ctx) => {
  const frequency = values.pmFrequencyValue?.trim()
  if (frequency) {
    if (!/^\d+$/.test(frequency) || Number(frequency) <= 0) {
      ctx.addIssue({ code: 'custom', path: ['pmFrequencyValue'], message: 'Enter a whole number greater than zero' })
    }
    if (!values.pmFrequencyUnit) {
      ctx.addIssue({ code: 'custom', path: ['pmFrequencyUnit'], message: 'Choose a unit' })
    }
  } else if (values.pmFrequencyUnit) {
    ctx.addIssue({ code: 'custom', path: ['pmFrequencyValue'], message: 'Enter how often maintenance is due' })
  }
  if (values.autoSchedulePm && !frequency) {
    ctx.addIssue({ code: 'custom', path: ['pmFrequencyValue'], message: 'Set a frequency to auto-schedule maintenance' })
  }
  if (values.lastPmDate && values.lastPmDate > new Date().toISOString().slice(0, 10)) {
    ctx.addIssue({ code: 'custom', path: ['lastPmDate'], message: 'Last maintenance date cannot be in the future' })
  }

  if (!values.hasWarranty) {
    return
  }
  const period = values.warrantyPeriodMonths?.trim()
  if (period && (!/^\d+$/.test(period) || Number(period) <= 0 || Number(period) > 600)) {
    ctx.addIssue({ code: 'custom', path: ['warrantyPeriodMonths'], message: 'Enter 1 to 600 months' })
  }
  if (!period && !values.warrantyExpiryDate) {
    ctx.addIssue({ code: 'custom', path: ['warrantyPeriodMonths'], message: 'Enter the warranty period or an expiry date' })
  }
  if (period && !values.warrantyExpiryDate && !values.warrantyStartDate && !values.installationDate) {
    ctx.addIssue({ code: 'custom', path: ['warrantyStartDate'], message: 'Enter a warranty start or installation date' })
  }
  const start = values.warrantyStartDate || values.installationDate
  if (start && values.warrantyExpiryDate && values.warrantyExpiryDate < start) {
    ctx.addIssue({ code: 'custom', path: ['warrantyExpiryDate'], message: 'Expiry cannot be before the warranty start' })
  }
})

export type AssetFormValues = z.infer<typeof assetSchema>
