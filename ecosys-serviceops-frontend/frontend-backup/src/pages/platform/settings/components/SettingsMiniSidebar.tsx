import { useEffect } from 'react'
import { Menu, Settings2, X } from 'lucide-react'
import type { PlatformSettingsSection } from '../../../../services/platformSettingsService'
import type { SettingsNavSection } from './settingsSections'
import { cleanupBodyInteractivity } from '../../../../utils/appCleanup'

type Props = {
  sections: SettingsNavSection[]
  activeSection: PlatformSettingsSection
  onSelect: (section: PlatformSettingsSection) => void
  mobileOpen: boolean
  onOpenMobile: () => void
  onCloseMobile: () => void
}

function SidebarContent({ sections, activeSection, onSelect }: Pick<Props, 'sections' | 'activeSection' | 'onSelect'>) {
  return (
    <div data-testid="settings-mini-sidebar" className="flex h-full min-h-0 flex-col">
      <div className="flex items-start gap-3 border-b border-app px-4 py-4">
        <div className="icon-accent rounded-2xl p-3">
          <Settings2 className="h-5 w-5" />
        </div>
        <div className="min-w-0">
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings</p>
          <h2 className="mt-2 text-xl font-semibold tracking-tight text-app">Command Centre</h2>
          <p className="mt-2 text-sm leading-6 text-muted">Use these focused sections to keep platform settings organized.</p>
        </div>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
        <nav className="space-y-2">
          {sections.map((section) => {
            const isActive = section.id === activeSection
            return (
              <button
                key={section.id}
                type="button"
                data-testid={`settings-mini-sidebar-${section.id}`}
                onClick={() => onSelect(section.id)}
                className={`settings-nav-link w-full text-left ${isActive ? 'settings-nav-link-active' : ''}`}
              >
                <p className="text-sm font-semibold">{section.label}</p>
                <p className="mt-1 text-xs leading-5 text-muted">{section.description}</p>
              </button>
            )
          })}
        </nav>
      </div>
    </div>
  )
}

export function SettingsMiniSidebar({ sections, activeSection, onSelect, mobileOpen, onOpenMobile, onCloseMobile }: Props) {
  useEffect(() => {
    if (!mobileOpen) {
      return
    }

    const { body } = document
    const previousOverflow = body.style.overflow
    body.style.overflow = 'hidden'

    return () => {
      body.style.overflow = previousOverflow
      cleanupBodyInteractivity()
    }
  }, [mobileOpen])

  return (
    <>
      <div className="flex items-center justify-between gap-2 md:hidden">
        <button type="button" className="button-secondary w-full sm:w-auto" onClick={onOpenMobile}>
          <Menu className="h-4 w-4" />
          Sections
        </button>
      </div>

      <div className="hidden h-full w-full md:block">
        <SidebarContent sections={sections} activeSection={activeSection} onSelect={onSelect} />
      </div>

      {mobileOpen ? (
        <div className="fixed inset-0 z-40 bg-black/50 p-4 md:hidden" onClick={onCloseMobile}>
          <div className="glass-panel flex h-full max-h-full w-full max-w-xs flex-col overflow-hidden rounded-[28px]" onClick={(event) => event.stopPropagation()}>
            <div className="flex shrink-0 items-center justify-between border-b border-app px-4 py-4">
              <div>
                <p className="text-sm font-semibold text-app">Settings Sections</p>
                <p className="mt-1 text-xs text-muted">Choose one section to edit.</p>
              </div>
              <button type="button" className="icon-button" onClick={onCloseMobile} aria-label="Close settings sections">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              <SidebarContent sections={sections} activeSection={activeSection} onSelect={(section) => { onSelect(section); onCloseMobile() }} />
            </div>
          </div>
        </div>
      ) : null}
    </>
  )
}
