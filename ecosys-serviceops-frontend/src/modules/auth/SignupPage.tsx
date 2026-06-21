import { ArrowRight, Building2, CircleCheckBig } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { EcosysLogo, PoweredByEcosys } from '../../components/brand'
import { useThemeMode } from '../../context/ThemeContext'
import {
  platformLeadService,
  preferredContactMethodOptions,
  type CreatePlatformLeadInput,
} from '../../services/platformLeadService'

export function SignupPage() {
  const { theme } = useThemeMode()
  const [form, setForm] = useState<CreatePlatformLeadInput>({
    companyName: '',
    contactPersonName: '',
    email: '',
    phone: '',
    country: '',
    industry: '',
    companySize: '',
    message: '',
    preferredContactMethod: 'Email',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)
  const [successMessage, setSuccessMessage] = useState('')
  const brandVariant = theme === 'light' ? 'dark' : 'light'
  const poweredByTone = theme === 'light' ? 'light' : 'dark'
  const title = 'Get Started with Ecosys'
  const subtitle = 'Tell us about your business and we’ll contact you shortly to guide you through the right setup.'

  const fieldErrors = useMemo(() => errors, [errors])

  function validate(next = form) {
    const validationErrors: Record<string, string> = {}
    if (!next.companyName.trim()) validationErrors.companyName = 'Company name is required.'
    if (!next.contactPersonName.trim()) validationErrors.contactPersonName = 'Contact person name is required.'
    if (!next.email.trim()) {
      validationErrors.email = 'Email address is required.'
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(next.email.trim())) {
      validationErrors.email = 'Enter a valid email address.'
    }
    if (!next.phone.trim()) validationErrors.phone = 'Phone number is required.'
    return validationErrors
  }

  function updateField<Key extends keyof CreatePlatformLeadInput>(key: Key, value: CreatePlatformLeadInput[Key]) {
    setForm((current) => ({ ...current, [key]: value }))
    setErrors((current) => {
      if (!current[key as string]) return current
      const next = { ...current }
      delete next[key as string]
      return next
    })
  }

  async function submit() {
    if (submitting) return

    const validationErrors = validate()
    setErrors(validationErrors)
    if (Object.keys(validationErrors).length > 0) {
      return
    }

    setSubmitting(true)
    try {
      const response = await platformLeadService.submitPublicLead(form)
      setSuccessMessage(response.message)
    } catch (error) {
      setErrors({
        form: error instanceof Error ? error.message : 'We could not submit your request right now.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="relative min-h-dvh overflow-y-auto bg-app">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(15,92,84,0.22),transparent_28%),radial-gradient(circle_at_bottom_left,rgba(183,226,109,0.16),transparent_32%)]" />
      <div className="relative mx-auto flex min-h-dvh max-w-7xl items-start px-4 py-10 sm:px-6 sm:py-12 lg:items-center lg:px-8">
        <div className="glass-panel grid w-full gap-8 rounded-[36px] p-5 lg:grid-cols-[0.88fr_1.12fr] lg:p-8">
          <section className="rounded-[30px] border border-[#d5e5de] bg-[linear-gradient(180deg,rgba(255,255,255,0.98),rgba(237,247,243,0.98))] p-6 shadow-[0_24px_60px_rgba(12,47,51,0.08)]">
            <div className="flex items-center gap-3">
              <div className="icon-emerald hidden rounded-[20px] p-3 lg:flex">
                <Building2 className="h-5 w-5" />
              </div>
              <EcosysLogo variant={brandVariant} size="lg" subtitle="Create your workspace" />
            </div>
            <p className="mt-6 font-heading text-4xl font-semibold tracking-tight text-[#0C2F33]">{title}</p>
            <p className="mt-4 max-w-xl text-sm leading-7 text-[#476360]">{subtitle}</p>
            <div className="mt-8 space-y-3 rounded-[26px] border border-[#d8e8e0] bg-white/80 p-5">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#127A78]">What happens next</p>
              <LeadStep>We review your requirements and recommend the right module mix.</LeadStep>
              <LeadStep>Our team contacts you to confirm operational needs and rollout approach.</LeadStep>
              <LeadStep>Your Ecosys setup will be custom made to suit your needs after review.</LeadStep>
            </div>
            <PoweredByEcosys className="mt-8" tone={poweredByTone} />
          </section>

          <section className="p-2 lg:p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-accent">Get Started</p>
            <h1 className="mt-3 text-3xl font-semibold tracking-tight text-app">{title}</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">{subtitle}</p>

            {successMessage ? (
              <div className="mt-8 rounded-[28px] border border-emerald-500/30 bg-emerald-500/10 p-6">
                <div className="flex items-start gap-4">
                  <div className="rounded-2xl bg-emerald-500/15 p-3 text-emerald-200">
                    <CircleCheckBig className="h-6 w-6" />
                  </div>
                  <div>
                    <h3 className="text-xl font-semibold text-app">Request received</h3>
                    <p className="mt-3 whitespace-pre-line text-sm leading-7 text-muted">{successMessage}</p>
                    <p className="mt-5 text-sm text-muted">
                      Need to access an existing workspace? <Link to="/login" className="font-semibold text-accent-strong hover:opacity-80">Login</Link>
                    </p>
                  </div>
                </div>
              </div>
            ) : (
              <form
                className="mt-8 space-y-6"
                onSubmit={(event) => {
                  event.preventDefault()
                  void submit()
                }}
              >
                <div className="grid gap-4 md:grid-cols-2">
                  <Field label="Company Name" error={fieldErrors.companyName}>
                    <input value={form.companyName} onChange={(event) => updateField('companyName', event.target.value)} className="field-input" disabled={submitting} />
                  </Field>
                  <Field label="Contact Person Name" error={fieldErrors.contactPersonName}>
                    <input value={form.contactPersonName} onChange={(event) => updateField('contactPersonName', event.target.value)} className="field-input" disabled={submitting} />
                  </Field>
                  <Field label="Email Address" error={fieldErrors.email}>
                    <input type="email" value={form.email} onChange={(event) => updateField('email', event.target.value)} className="field-input" autoComplete="email" disabled={submitting} />
                  </Field>
                  <Field label="Phone Number" error={fieldErrors.phone}>
                    <input value={form.phone} onChange={(event) => updateField('phone', event.target.value)} className="field-input" autoComplete="tel" disabled={submitting} />
                  </Field>
                  <Field label="Country">
                    <input value={form.country || ''} onChange={(event) => updateField('country', event.target.value)} className="field-input" disabled={submitting} />
                  </Field>
                  <Field label="Industry">
                    <input value={form.industry || ''} onChange={(event) => updateField('industry', event.target.value)} className="field-input" placeholder="Facilities, manufacturing, healthcare..." disabled={submitting} />
                  </Field>
                  <Field label="Company Size / Number of Users">
                    <input value={form.companySize || ''} onChange={(event) => updateField('companySize', event.target.value)} className="field-input" placeholder="Example: 50 staff, 20 users" disabled={submitting} />
                  </Field>
                  <Field label="Preferred Contact Method">
                    <select value={form.preferredContactMethod || 'Email'} onChange={(event) => updateField('preferredContactMethod', event.target.value as CreatePlatformLeadInput['preferredContactMethod'])} className="field-input" disabled={submitting}>
                      {preferredContactMethodOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                    </select>
                  </Field>
                </div>

                <Field label="Message / Business Need">
                  <textarea
                    value={form.message || ''}
                    onChange={(event) => updateField('message', event.target.value)}
                    className="field-input min-h-[160px]"
                    placeholder="Tell us about your operations, pain points, team size, or rollout goals."
                    disabled={submitting}
                  />
                </Field>

                {fieldErrors.form ? <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{fieldErrors.form}</div> : null}

                <button type="submit" className="button-primary w-full justify-center py-3 text-sm" disabled={submitting}>
                  {submitting ? 'Submitting request...' : 'Submit request'}
                  <ArrowRight className="h-4 w-4" />
                </button>
              </form>
            )}

            <p className="mt-6 text-sm text-muted">
              Already have an account? <Link to="/login" className="font-semibold text-emerald-strong hover:opacity-80">Login instead</Link>
            </p>
          </section>
        </div>
      </div>
    </div>
  )
}

function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
      {error ? <span className="text-xs font-medium text-rose-300">{error}</span> : null}
    </label>
  )
}

function LeadStep({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-start gap-3">
      <span className="mt-1 inline-flex h-2.5 w-2.5 rounded-full bg-[#B7E26D]" />
      <p className="text-sm leading-6 text-[#48625f]">{children}</p>
    </div>
  )
}
