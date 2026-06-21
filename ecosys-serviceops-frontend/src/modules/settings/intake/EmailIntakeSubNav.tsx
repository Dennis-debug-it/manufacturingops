import type { EmailIntakeSectionId } from './emailIntakeModels'
import { EMAIL_INTAKE_NAV_ITEMS } from './emailIntakeModels'

export function EmailIntakeSubNav({
  activeSection,
  onChange,
}: {
  activeSection: EmailIntakeSectionId
  onChange: (section: EmailIntakeSectionId) => void
}) {
  return (
    <section data-testid="email-intake-subnav" className="surface-card space-y-4">
      <div className="md:hidden">
        <label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Email Intake Sections</label>
        <select
          className="field-input mt-2"
          value={activeSection}
          onChange={(event) => onChange(event.target.value as EmailIntakeSectionId)}
        >
          {EMAIL_INTAKE_NAV_ITEMS.map((item) => (
            <option key={item.id} value={item.id}>{item.label}</option>
          ))}
        </select>
      </div>

      <div className="hidden gap-2 md:flex md:flex-col">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Workflow Sections</p>
        {EMAIL_INTAKE_NAV_ITEMS.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => onChange(item.id)}
            className={`w-full rounded-2xl px-3 py-2 text-left text-sm transition ${activeSection === item.id ? 'bg-cyan-400/15 text-app border border-sky-400/35' : 'panel-subtle text-muted border border-transparent'}`}
          >
            {item.label}
          </button>
        ))}
      </div>
    </section>
  )
}
