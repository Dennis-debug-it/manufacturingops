import { ChevronRight } from 'lucide-react'
import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Badge } from './Badge'
import { Drawer } from './Drawer'
import { PageTabs } from './Workspace'

export type SettingsTab<T extends string> = {
  id: T
  label: string
  meta?: ReactNode
}

export function SettingsSection({
  title,
  description,
  children,
  className = '',
}: {
  title: string
  description?: string
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`space-y-3 border-b border-[var(--app-divider)] pb-4 last:border-b-0 last:pb-0 ${className}`.trim()}>
      <div className="space-y-1.5">
        <h2 className="font-heading text-[1rem] font-semibold tracking-[-0.02em] text-app">{title}</h2>
        {description ? <p className="max-w-3xl text-sm leading-5 text-muted">{description}</p> : null}
      </div>
      {children}
    </section>
  )
}

export function SettingsRow({
  icon: Icon,
  title,
  description,
  status,
  action,
  href,
  onClick,
  className = '',
}: {
  icon?: ReactNode
  title: string
  description?: string
  status?: ReactNode
  action?: ReactNode
  href?: string
  onClick?: () => void
  className?: string
}) {
  const content = (
    <div className={`group flex items-center gap-3 rounded-[16px] px-3 py-3 transition hover:bg-[var(--app-subtle-hover)] ${className}`.trim()}>
      <div className="icon-accent mt-0.5 rounded-[14px] p-2.5">
        {Icon}
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-sm font-semibold text-app">{title}</p>
          {status ? <span className="shrink-0">{status}</span> : null}
        </div>
        {description ? <p className="mt-1 text-sm leading-5 text-muted">{description}</p> : null}
      </div>
      <div className="flex items-center gap-2">
        {action}
        {href || onClick ? <ChevronRight className="h-4 w-4 shrink-0 text-muted transition group-hover:translate-x-0.5 group-hover:text-app" /> : null}
      </div>
    </div>
  )

  if (href) {
    return (
      <Link to={href} className="block">
        {content}
      </Link>
    )
  }

  if (onClick) {
    return (
      <button type="button" onClick={onClick} className="block w-full text-left">
        {content}
      </button>
    )
  }

  return content
}

export function SectionStatusBadge({
  tone = 'neutral',
  children,
}: {
  tone?: Parameters<typeof Badge>[0]['tone']
  children: ReactNode
}) {
  return <Badge tone={tone}>{children}</Badge>
}

export function FormSection({
  title,
  description,
  children,
  columns = 2,
}: {
  title: string
  description?: string
  children: ReactNode
  columns?: 1 | 2 | 3
}) {
  const gridClass =
    columns === 1 ? 'grid-cols-1' : columns === 3 ? 'grid-cols-1 md:grid-cols-2 xl:grid-cols-3' : 'grid-cols-1 md:grid-cols-2'

  return (
    <section className="space-y-3 border-b border-[var(--app-divider)] pb-4 last:border-b-0 last:pb-0">
      <div className="space-y-1.5">
        <h3 className="font-heading text-[0.98rem] font-semibold text-app">{title}</h3>
        {description ? <p className="text-sm leading-5 text-muted">{description}</p> : null}
      </div>
      <div className={`grid gap-3 ${gridClass}`}>{children}</div>
    </section>
  )
}

export function SettingsTabs<T extends string>({
  tabs,
  activeTab,
  onChange,
}: {
  tabs: Array<SettingsTab<T>>
  activeTab: T
  onChange: (next: T) => void
}) {
  return <PageTabs tabs={tabs} activeTab={activeTab} onChange={onChange} />
}

export function StickySettingsActions({
  visible,
  children,
}: {
  visible: boolean
  children: ReactNode
}) {
  if (!visible) return null

  return (
    <div className="sticky bottom-0 z-10 -mx-4 mt-4 border-t border-[var(--app-divider)] bg-[color:color-mix(in_srgb,var(--app-bg)_92%,transparent)] px-4 py-3 backdrop-blur-md sm:-mx-6 sm:px-6 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
      <div className="flex flex-wrap items-center justify-end gap-2">{children}</div>
    </div>
  )
}

export function SettingsDrawer({
  open,
  title,
  description,
  onClose,
  children,
}: {
  open: boolean
  title: string
  description: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <Drawer open={open} title={title} description={description} onClose={onClose}>
      {children}
    </Drawer>
  )
}

export function SmartTextarea({
  label,
  description,
  value,
  onChange,
  minRows = 3,
  maxRows = 8,
  maxLength,
  error,
  disabled,
  readOnly,
  placeholder,
  className = '',
  actions,
  testId,
}: {
  label: string
  description?: string
  value: string
  onChange: (value: string) => void
  minRows?: number
  maxRows?: number
  maxLength?: number
  error?: string
  disabled?: boolean
  readOnly?: boolean
  placeholder?: string
  className?: string
  actions?: ReactNode
  testId?: string
}) {
  const id = useId()
  const ref = useRef<HTMLTextAreaElement>(null)
  const count = value.length

  useEffect(() => {
    const element = ref.current
    if (!element) return

    element.style.height = 'auto'
    const lineHeight = Number.parseFloat(window.getComputedStyle(element).lineHeight || '24')
    const nextHeight = Math.min(Math.max(element.scrollHeight, minRows * lineHeight), maxRows * lineHeight)
    element.style.height = `${nextHeight}px`
    element.style.overflowY = element.scrollHeight > nextHeight ? 'auto' : 'hidden'
  }, [maxRows, minRows, value])

  return (
    <label className={`block space-y-2 ${className}`.trim()} htmlFor={id}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <span className="text-sm font-semibold text-app">{label}</span>
          {description ? <p className="mt-1 text-xs leading-5 text-muted">{description}</p> : null}
        </div>
        {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
      </div>
      <textarea
        id={id}
        ref={ref}
        data-testid={testId}
        className={`field-input resize-none ${error ? 'border-[var(--app-danger)]' : ''}`.trim()}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        readOnly={readOnly}
        maxLength={maxLength}
        rows={minRows}
        aria-invalid={Boolean(error)}
      />
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          {error ? <p className="text-xs text-[var(--app-danger)]">{error}</p> : null}
        </div>
        <div className="flex shrink-0 items-center gap-3 text-xs text-muted">
          {typeof maxLength === 'number' ? <span>{count} / {maxLength}</span> : null}
          <span>{minRows}-{maxRows} rows</span>
        </div>
      </div>
    </label>
  )
}

export function SettingsSidebar({
  title,
  description,
  icon,
  sections,
  activeId,
  onSelect,
  mobileOpen,
  onOpenMobile,
  onCloseMobile,
}: {
  title: string
  description: string
  icon?: ReactNode
  sections: Array<{
    id: string
    label: string
    description?: string
    icon?: ReactNode
    badge?: ReactNode
  }>
  activeId: string
  onSelect: (id: string) => void
  mobileOpen: boolean
  onOpenMobile: () => void
  onCloseMobile: () => void
}) {
  useEffect(() => {
    if (!mobileOpen) return

    const { body } = document
    const previousOverflow = body.style.overflow
    const previousTouchAction = body.style.touchAction
    body.style.overflow = 'hidden'
    body.style.touchAction = 'none'

    return () => {
      body.style.overflow = previousOverflow
      body.style.touchAction = previousTouchAction
    }
  }, [mobileOpen])

  const sidebarContent = (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-start gap-3 border-b border-[var(--app-divider)] px-4 py-4">
        <div className="icon-accent rounded-[14px] p-2.5">{icon}</div>
        <div className="min-w-0">
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings</p>
          <h2 className="mt-1.5 text-lg font-semibold tracking-tight text-app">{title}</h2>
          <p className="mt-1 text-sm leading-5 text-muted">{description}</p>
        </div>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
        <nav className="space-y-1">
          {sections.map((section) => (
            <button
              key={section.id}
              type="button"
              onClick={() => onSelect(section.id)}
              className={`settings-nav-link w-full text-left ${section.id === activeId ? 'settings-nav-link-active' : ''}`}
            >
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-semibold">{section.label}</p>
                  {section.badge ? <span className="shrink-0">{section.badge}</span> : null}
                </div>
                {section.description ? <p className="mt-1 text-xs leading-5 text-muted">{section.description}</p> : null}
              </div>
            </button>
          ))}
        </nav>
      </div>
    </div>
  )

  return (
    <>
      <div className="flex items-center justify-between gap-2 md:hidden">
        <button type="button" className="button-secondary w-full sm:w-auto" onClick={onOpenMobile}>
          <ChevronRight className="h-4 w-4 rotate-90" />
          Sections
        </button>
      </div>

      <div className="hidden h-full min-h-0 w-full md:block">{sidebarContent}</div>

      {mobileOpen ? (
        <div className="fixed inset-0 z-50 bg-black/50 p-4 md:hidden" onClick={onCloseMobile}>
          <div
            className="glass-panel flex h-[calc(100dvh-2rem)] max-h-[calc(100dvh-2rem)] w-full max-w-xs flex-col overflow-hidden rounded-[24px]"
            onClick={(event) => event.stopPropagation()}
          >
            {sidebarContent}
          </div>
        </div>
      ) : null}
    </>
  )
}
