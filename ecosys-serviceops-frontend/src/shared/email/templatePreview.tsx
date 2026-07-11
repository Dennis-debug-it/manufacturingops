import DOMPurify from 'dompurify'
import type { ReactNode } from 'react'

export interface TemplatePreviewProps {
  subject: string
  body: string
  footer?: ReactNode
}

export function TemplatePreview({ subject, body, footer }: TemplatePreviewProps) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">Preview</div>
      <h3 className="mt-3 text-lg font-semibold text-slate-900">{subject}</h3>
      <div className="prose prose-slate mt-4 max-w-none text-sm" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(body) }} />
      {footer ? <div className="mt-6 border-t border-slate-200 pt-4 text-sm text-slate-600">{footer}</div> : null}
    </div>
  )
}
