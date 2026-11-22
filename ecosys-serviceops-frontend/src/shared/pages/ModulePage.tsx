import type { ReactNode } from 'react'
import { SectionHeader } from '../components/SectionHeader'
import { Surface } from '../components/Surface'

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
