import { Link } from 'react-router-dom'
import {
  Building2, Layers, Palette, Flag, Mail, Send, ShieldCheck, Megaphone,
  MessageCircleQuestion, Activity, ScrollText,
} from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'

const items: Array<{ to: string; label: string; description: string; icon: typeof Building2 }> = [
  { to: '/platform/settings/identity', label: 'Identity', description: 'Platform name, locale, and legal details.', icon: Building2 },
  { to: '/platform/settings/plans', label: 'Editions & Pricing', description: 'Seat prices per edition and WhatsApp add-on prices.', icon: Layers },
  { to: '/platform/settings/branding', label: 'Branding', description: 'Logo, favicon, and colour palette.', icon: Palette },
  { to: '/platform/settings/features', label: 'Edition Features', description: 'What each edition includes.', icon: Flag },
  { to: '/platform/settings/email-templates', label: 'Email Templates', description: 'Platform-wide transactional email defaults.', icon: Mail },
  { to: '/platform/settings/email-delivery', label: 'Email Delivery', description: 'SMTP, flags, and subject rules.', icon: Send },
  { to: '/platform/settings/security', label: 'Security', description: 'Password policy and session rules.', icon: ShieldCheck },
  { to: '/platform/settings/announcements', label: 'Announcements', description: 'Platform-wide banners and release notes.', icon: Megaphone },
  { to: '/platform/settings/support', label: 'Support', description: 'Support contact and escalation routing.', icon: MessageCircleQuestion },
  { to: '/platform/settings/health', label: 'System Health', description: 'Background jobs and service status.', icon: Activity },
  { to: '/platform/settings/audit', label: 'Audit Log', description: 'Cross-tenant administrative history.', icon: ScrollText },
]

export function PlatformSettingsOverviewPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Platform Settings" subtitle="Configuration that applies across every tenant workspace." />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((item) => (
          <Link key={item.to} to={item.to} className="flex items-start gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm transition hover:border-slate-300 hover:shadow">
            <item.icon className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
            <div>
              <p className="text-sm font-medium text-slate-900">{item.label}</p>
              <p className="mt-1 text-xs text-slate-500">{item.description}</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
