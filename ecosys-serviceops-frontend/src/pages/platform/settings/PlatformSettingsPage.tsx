import { useMemo, useState } from 'react'
import { PageScaffold } from '../../../components/ui/Workspace'
import type { PlatformSettingsSection } from '../../../services/platformSettingsService'
import { BrandingSettingsPanel } from './components/BrandingSettingsPanel'
import { EmailNotificationSettingsPanel } from './components/EmailNotificationSettingsPanel'
import { GeneralSettingsPanel } from './components/GeneralSettingsPanel'
import { NumberingSettingsPanel } from './components/NumberingSettingsPanel'
import { SecuritySettingsPanel } from './components/SecuritySettingsPanel'
import { SettingsMiniSidebar } from './components/SettingsMiniSidebar'
import { settingsSections } from './components/settingsSections'
import { SystemPreferencesPanel } from './components/SystemPreferencesPanel'

function renderPanel(activeSection: PlatformSettingsSection) {
  if (activeSection === 'general') return <div data-testid="settings-panel-general"><GeneralSettingsPanel /></div>
  if (activeSection === 'branding') return <div data-testid="settings-panel-branding"><BrandingSettingsPanel /></div>
  if (activeSection === 'email') return <div data-testid="settings-panel-email-notifications"><EmailNotificationSettingsPanel /></div>
  if (activeSection === 'numbering') return <div data-testid="settings-panel-numbering"><NumberingSettingsPanel /></div>
  if (activeSection === 'security') return <div data-testid="settings-panel-security"><SecuritySettingsPanel /></div>
  return <div data-testid="settings-panel-system-preferences"><SystemPreferencesPanel /></div>
}

export function PlatformSettingsPage() {
  const [activeSection, setActiveSection] = useState<PlatformSettingsSection>('email')
  const [mobileOpen, setMobileOpen] = useState(false)
  const active = useMemo(() => settingsSections.find((item) => item.id === activeSection) ?? settingsSections[0], [activeSection])

  return (
    <div data-testid="platform-settings-page">
      <PageScaffold
        eyebrow="Platform Command Centre"
        title="Platform Settings"
        description="Manage core platform setup for branding, communications, security, and system preferences."
      >

        <div className="surface-card flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings Workspace</p>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight text-app">{active.label}</h2>
            <p className="mt-2 text-sm text-muted">{active.description}</p>
          </div>
        </div>

        <div className="surface-card md:hidden">
          <div className="flex gap-2 overflow-x-auto pb-1">
            {settingsSections.map((section) => (
              <button
                key={section.id}
                type="button"
                onClick={() => setActiveSection(section.id)}
                className={`shrink-0 rounded-2xl px-3 py-2 text-sm font-semibold transition ${activeSection === section.id ? 'bg-subtle-strong text-app border border-app' : 'panel-subtle text-muted'}`}
              >
                {section.shortLabel}
              </button>
            ))}
          </div>
        </div>

        <div className="flex min-h-[680px] w-full min-w-0 flex-col overflow-hidden rounded-[32px] border border-app bg-[var(--app-card)] md:flex-row">
          <aside className="w-full border-b border-app md:w-80 md:border-b-0 md:border-r">
            <SettingsMiniSidebar
              sections={settingsSections}
              activeSection={activeSection}
              onSelect={setActiveSection}
              mobileOpen={mobileOpen}
              onOpenMobile={() => setMobileOpen(true)}
              onCloseMobile={() => setMobileOpen(false)}
            />
          </aside>
          <main className="min-w-0 flex-1 p-4 sm:p-6">
            {renderPanel(activeSection)}
          </main>
        </div>
      </PageScaffold>
    </div>
  )
}
