import { useEffect, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ChevronLeft, Download, RefreshCw, Save } from 'lucide-react'
import toast from 'react-hot-toast'
import { Spinner } from '../../shared/components/Spinner'
import { OnOffSwitch } from '../../shared/components/OnOffSwitch'
import { cn } from '../../shared/utils/cn'
import { inputClass, primaryButton, secondaryButton, textareaClass } from '../../features/factory/components/ui'
import {
  DEFAULT_TITLES, DOCUMENT_LAYOUTS, DOCUMENT_TYPES, blankDraft, draftOf, previewDocument, saveBlob, useDocumentTemplate, usePlaceholders, useSaveDocumentTemplate,
  type DocumentTemplateDraft, type DocumentType,
} from './api'

const ACCENTS = ['#0f3d3e', '#1d4ed8', '#047857', '#b45309', '#b91c1c', '#6d28d9', '#334155', '#0e7490']

type TextField = 'subject' | 'headerHtml' | 'bodyHtml' | 'termsHtml' | 'signatureHtml' | 'footerHtml'

/**
 * The document designer: lays out a quotation, invoice or receipt and shows the real PDF beside it, redrawn as you
 * type on a made-up document of the type.
 */
export function DocumentDesigner() {
  const { id: routeId } = useParams()
  const id = routeId && routeId !== 'new' ? routeId : null
  const [params] = useSearchParams()
  const existing = useDocumentTemplate(id)

  if (id && existing.isLoading) return <Spinner label="Loading template" />
  if (id && !existing.data) return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">This template could not be loaded.</div>

  const type = (DOCUMENT_TYPES.find((x) => x === params.get('type')) ?? 'Invoice') as DocumentType
  return <Designer key={id ?? `new-${type}`} id={id} initial={existing.data ? draftOf(existing.data) : blankDraft(type)} />
}

function Designer({ id, initial }: { id: string | null; initial: DocumentTemplateDraft }) {
  const navigate = useNavigate()
  const save = useSaveDocumentTemplate(id)
  const placeholders = usePlaceholders()
  const [draft, setDraft] = useState(initial)
  const [dirty, setDirty] = useState(false)
  const [focused, setFocused] = useState<TextField>('bodyHtml')
  const preview = usePreview(draft)
  const [downloading, setDownloading] = useState(false)

  const downloadSample = async () => {
    setDownloading(true)
    try {
      saveBlob(await previewDocument(draft, 'pdf'), `${draft.type.toLowerCase()}-sample.pdf`)
    } catch {
      toast.error('The PDF could not be made.')
    } finally {
      setDownloading(false)
    }
  }

  const set = <K extends keyof DocumentTemplateDraft>(key: K, value: DocumentTemplateDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }))
    setDirty(true)
  }

  const changeType = (type: string) => {
    // Keep a custom title; swap the standard one for the new type's.
    const standard = Object.values(DEFAULT_TITLES).includes(draft.subject.trim().toUpperCase()) || !draft.subject.trim()
    setDraft((current) => ({ ...current, type, subject: standard ? DEFAULT_TITLES[type as DocumentType] : current.subject }))
    setDirty(true)
  }

  const insert = (key: string) => {
    const field = focused
    // The placeholder buttons keep focus where it was, so the field being edited is still the active element.
    const active = document.activeElement
    const element = active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement ? (active.name === field ? active : null) : null
    const token = `{{${key}}}`
    const value = draft[field]
    const start = element?.selectionStart ?? value.length
    const end = element?.selectionEnd ?? value.length
    set(field, value.slice(0, start) + token + value.slice(end))
    requestAnimationFrame(() => {
      element?.focus()
      element?.setSelectionRange(start + token.length, start + token.length)
    })
  }

  const onSave = () => {
    if (!draft.name.trim()) {
      toast.error('Give the template a name.')
      return
    }
    save.mutate(draft, {
      onSuccess: (saved) => {
        toast.success('Template saved')
        setDirty(false)
        if (!id) navigate(`/platform/settings/templates/${saved.id}`, { replace: true })
      },
    })
  }

  const text = (field: TextField) => ({
    name: field,
    value: draft[field],
    onFocus: () => setFocused(field),
    onChange: (e: { target: { value: string } }) => set(field, e.target.value),
  })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <Link to="/platform/settings/templates" className="inline-flex items-center gap-1 text-sm text-slate-600 hover:text-slate-900"><ChevronLeft className="h-4 w-4" />Document templates</Link>
          <h1 className="mt-1 text-2xl font-semibold text-slate-900">{id ? draft.name || 'Template' : `New ${draft.type.toLowerCase()} template`}</h1>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {dirty ? <span className="text-sm text-amber-700">Unsaved changes</span> : null}
          <button type="button" className={primaryButton} onClick={onSave} disabled={save.isPending}>
            <Save className="h-4 w-4" />{save.isPending ? 'Saving…' : 'Save template'}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,440px)_minmax(0,1fr)]">
        <div className="space-y-4">
          <Section title="Document">
            <Labelled label="Type">
              <select className={inputClass} value={draft.type} onChange={(e) => changeType(e.target.value)}>
                {DOCUMENT_TYPES.map((x) => <option key={x}>{x}</option>)}
              </select>
            </Labelled>
            <Labelled label="Template name">
              <input className={inputClass} value={draft.name} onChange={(e) => set('name', e.target.value)} placeholder="e.g. Standard invoice" />
            </Labelled>
            <Labelled label="Title on the document">
              <input className={inputClass} {...text('subject')} placeholder={DEFAULT_TITLES[draft.type as DocumentType] ?? 'INVOICE'} />
            </Labelled>
          </Section>

          <Section title="Look">
            <div className="grid grid-cols-3 gap-2" role="radiogroup" aria-label="Layout">
              {DOCUMENT_LAYOUTS.map((x) => (
                <button key={x.id} type="button" role="radio" aria-checked={draft.layout === x.id} title={x.hint} onClick={() => set('layout', x.id)}
                  className={cn('rounded-xl border p-2 text-left text-xs transition', draft.layout === x.id ? 'border-[var(--app-primary)] ring-2 ring-[var(--app-primary)]/20' : 'border-slate-200 hover:border-slate-300')}>
                  <LayoutThumb layout={x.id} accent={draft.accentColor} />
                  <span className="mt-1.5 block font-medium text-slate-800">{x.label}</span>
                </button>
              ))}
            </div>
            <Labelled label="Colour">
              <div className="flex flex-wrap items-center gap-2">
                {ACCENTS.map((colour) => (
                  <button key={colour} type="button" aria-label={`Colour ${colour}`} onClick={() => set('accentColor', colour)}
                    className={cn('h-8 w-8 rounded-full border-2 transition', draft.accentColor.toLowerCase() === colour ? 'border-slate-900 scale-110' : 'border-white shadow-sm ring-1 ring-slate-200')}
                    style={{ background: colour }} />
                ))}
                <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                  <input type="color" value={draft.accentColor} onChange={(e) => set('accentColor', e.target.value)} className="h-8 w-10 cursor-pointer rounded border border-slate-200" aria-label="Custom colour" />
                  {draft.accentColor}
                </label>
              </div>
            </Labelled>
            <div className="grid grid-cols-2 gap-3">
              <Labelled label="Page size">
                <select className={inputClass} value={draft.pageSize} onChange={(e) => set('pageSize', e.target.value)}>
                  <option>A4</option><option>Letter</option>
                </select>
              </Labelled>
              <Labelled label="Orientation">
                <select className={inputClass} value={draft.orientation} onChange={(e) => set('orientation', e.target.value)}>
                  <option>Portrait</option><option>Landscape</option>
                </select>
              </Labelled>
            </div>
            <Toggle label="Show the logo" hint="The logo from Platform Settings → Branding." checked={draft.showLogo} onChange={(v) => set('showLogo', v)} />
            <Toggle label="Show “Powered by Ecosys”" checked={draft.showPoweredByEcosys} onChange={(v) => set('showPoweredByEcosys', v)} />
          </Section>

          <Section title="Wording" description="Plain text; new lines are kept. Click a field, then a placeholder to put it in.">
            <Labelled label="Company details" hint="Under your name: address, PIN, phone, email.">
              <textarea className={textareaClass} rows={3} {...text('headerHtml')} placeholder={'P.O. Box 123-00100, Nairobi\nPIN P051234567X · billing@example.co.ke'} />
            </Labelled>
            <Labelled label="Opening message" hint="Above the lines.">
              <textarea className={textareaClass} rows={2} {...text('bodyHtml')} />
            </Labelled>
            <Labelled label={draft.type === 'Receipt' ? 'Details' : 'Terms and payment details'} hint="Bank, M-Pesa paybill, due terms.">
              <textarea className={textareaClass} rows={4} {...text('termsHtml')} placeholder={'M-Pesa Paybill 400200, account {{document.number}}\nKCB, A/C 1234567890, Westlands branch'} />
            </Labelled>
            <Labelled label="Sign-off">
              <textarea className={textareaClass} rows={2} {...text('signatureHtml')} placeholder="Authorised by: ____________" />
            </Labelled>
            <Labelled label="Page footer">
              <input className={inputClass} {...text('footerHtml')} placeholder="Company name · registration number · website" />
            </Labelled>
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Placeholders</p>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {(placeholders.data ?? []).map((x) => (
                  <button key={x.key} type="button" title={x.meaning} onMouseDown={(e) => e.preventDefault()} onClick={() => insert(x.key)}
                    className="rounded-md border border-slate-200 bg-slate-50 px-2 py-1 font-mono text-[11px] text-slate-700 hover:border-[var(--app-primary)] hover:text-[var(--app-primary)]">
                    {`{{${x.key}}}`}
                  </button>
                ))}
              </div>
            </div>
          </Section>

          <Section title="Use">
            <Toggle label={`Default ${draft.type.toLowerCase()} template`} hint={`Used for every ${draft.type.toLowerCase()} PDF. Only one template per type is the default.`} checked={draft.isDefault} onChange={(v) => set('isDefault', v)} />
            <Toggle label="Active" checked={draft.isActive} onChange={(v) => set('isActive', v)} />
          </Section>
        </div>

        <div className="xl:sticky xl:top-4 xl:self-start">
          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-3 shadow-sm">
            <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm font-medium text-slate-700">Preview <span className="font-normal text-slate-500">· first page of a made-up {draft.type.toLowerCase()}</span></p>
              <div className="flex items-center gap-2">
                {preview.loading ? <span className="inline-flex items-center gap-1 text-xs text-slate-500"><RefreshCw className="h-3.5 w-3.5 animate-spin" />Updating</span> : null}
                <button type="button" className={cn(secondaryButton, 'h-8 px-3 text-xs')} disabled={downloading} onClick={() => void downloadSample()}>
                  <Download className="h-3.5 w-3.5" />{downloading ? 'Making PDF…' : 'Download PDF'}
                </button>
              </div>
            </div>
            {preview.error ? (
              <div className="flex h-[70vh] items-center justify-center rounded-xl border border-dashed border-rose-200 bg-rose-50 p-6 text-center text-sm text-rose-700">{preview.error}</div>
            ) : preview.url ? (
              <div className="max-h-[80vh] overflow-auto rounded-xl border border-slate-200 bg-slate-100 p-3">
                <img src={preview.url} alt={`Preview of the ${draft.type.toLowerCase()} template, first page`} className={cn('mx-auto w-full bg-white shadow-md transition-opacity', preview.loading ? 'opacity-70' : 'opacity-100')} />
              </div>
            ) : (
              <div className="flex h-[70vh] items-center justify-center"><Spinner label="Drawing the preview" /></div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

/** Redraws the first page shortly after the draft stops changing. */
function usePreview(draft: DocumentTemplateDraft) {
  const [url, setUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const key = JSON.stringify(draft)

  useEffect(() => {
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      setLoading(true)
      previewDocument(JSON.parse(key) as DocumentTemplateDraft, 'png', controller.signal)
        .then((blob) => {
          setError(null)
          setUrl((old) => {
            if (old) URL.revokeObjectURL(old)
            return URL.createObjectURL(blob)
          })
        })
        .catch((reason: unknown) => {
          if (controller.signal.aborted) return
          setError(reason instanceof Error && reason.message.includes('colour') ? reason.message : 'The preview could not be drawn. Check the colour is a hex value like #0f3d3e.')
        })
        .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    }, 500)
    return () => { controller.abort(); window.clearTimeout(timer) }
  }, [key])

  useEffect(() => () => { if (url) URL.revokeObjectURL(url) }, [url])
  return { url, loading, error }
}

function LayoutThumb({ layout, accent }: { layout: string; accent: string }) {
  return (
    <svg viewBox="0 0 60 44" className="h-12 w-full rounded border border-slate-100 bg-white" aria-hidden>
      {layout === 'Banner' ? <rect x="0" y="0" width="60" height="11" fill={accent} /> : null}
      <rect x="5" y="4" width="18" height="3" rx="1" fill={layout === 'Banner' ? '#fff' : accent} />
      <rect x="40" y="4" width="15" height="3" rx="1" fill={layout === 'Banner' ? '#fff' : accent} />
      {layout === 'Classic' ? <rect x="5" y="11" width="50" height="0.8" fill={accent} /> : null}
      {layout === 'Minimal' ? <rect x="5" y="11" width="50" height="0.5" fill="#cbd5e1" /> : null}
      <rect x="5" y="16" width="50" height="4" fill={layout === 'Minimal' ? '#fff' : accent} stroke={layout === 'Minimal' ? '#94a3b8' : 'none'} strokeWidth="0.4" />
      {[23, 27, 31].map((y) => <rect key={y} x="5" y={y} width="50" height="0.6" fill="#e2e8f0" />)}
      <rect x="38" y="35" width="17" height="3" rx="1" fill={accent} opacity={layout === 'Minimal' ? 0.6 : 1} />
    </svg>
  )
}

function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <section className="space-y-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <div>
        <h2 className="text-sm font-semibold text-slate-900">{title}</h2>
        {description ? <p className="text-xs text-slate-500">{description}</p> : null}
      </div>
      {children}
    </section>
  )
}

function Labelled({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="block text-xs font-semibold text-slate-600">{label}{hint ? <span className="ml-1 font-normal text-slate-400">{hint}</span> : null}</span>
      {children}
    </label>
  )
}

function Toggle({ label, hint, checked, onChange }: { label: string; hint?: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <div>
        <p className="text-sm text-slate-800">{label}</p>
        {hint ? <p className="text-xs text-slate-500">{hint}</p> : null}
      </div>
      <OnOffSwitch checked={checked} label={label} onChange={onChange} />
    </div>
  )
}
