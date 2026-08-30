import type { ReactNode } from 'react'
import { SectionHeader } from '../components/SectionHeader'
import { Surface } from '../components/Surface'

/**
 * This file used to also export `createModulePage`, a factory that rendered a generic
 * "module shell" — a page header, a search box that filtered nothing, and an EmptyState
 * reading "This route is available and ready for API integration."
 *
 * Its last consumer was the `/clients/:id/sites/new` route (EC-022). Sites are actually
 * created through `SiteFormDrawer`, opened from the Sites tab of `ClientDetailPage`, which
 * posts to `POST /api/clients/{clientId}/sites`. Nothing linked to the placeholder route, so
 * both it and the factory were removed rather than reimplemented as duplicate UI.
 *
 * The file keeps its path because `SummarySection` below is imported by a dozen detail pages.
 */

export interface SummarySectionProps {
  title: string
  description?: string
  children: ReactNode
}

export function SummarySection({ title, description, children }: SummarySectionProps) {
  return (
    <Surface as="section" variant="glass" padding="lg" className="space-y-4">
      <SectionHeader title={title} {...(description ? { description } : {})} />
      {children}
    </Surface>
  )
}
