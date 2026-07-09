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
  assetType: z.string().max(100).optional(),
  location: z.string().max(200).optional(),
  serialNumber: z.string().max(120).optional(),
  manufacturer: z.string().max(120).optional(),
  model: z.string().max(120).optional(),
  installationDate: z.string().optional(),
  warrantyExpiryDate: z.string().optional(),
  notes: z.string().max(2000).optional(),
})

export type AssetFormValues = z.infer<typeof assetSchema>
