import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageHeader'
import { settingsSections } from '../../pages/settings/routes'

export function SettingsHubPage() {
  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Tenant configuration"
        description="Manage all configuration areas for your workspace."
      />
      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {settingsSections.map((section) => {
          const Icon = section.icon
          return (
            <article key={section.path} className="surface-card flex flex-col">
              <div className="icon-accent mb-3 w-fit rounded-2xl p-3">
                <Icon className="h-5 w-5" />
              </div>
              <h2 className="text-base font-semibold text-app">{section.label}</h2>
              <p className="mt-2 flex-1 text-sm leading-6 text-muted">{section.description}</p>
              <Link to={section.path} className="button-secondary mt-4">
                Open
              </Link>
            </article>
          )
        })}
      </section>
    </div>
  )
}
