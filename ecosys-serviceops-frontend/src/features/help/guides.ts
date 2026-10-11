import type { LucideIcon } from 'lucide-react'
import {
  Boxes,
  ClipboardList,
  FileText,
  Inbox,
  Rocket,
  Settings2,
  Smartphone,
  Users,
} from 'lucide-react'

export interface GuideSection {
  title: string
  text?: string
  steps?: string[]
  tip?: string
}

export interface Guide {
  id: string
  title: string
  summary: string
  icon: LucideIcon
  /** The screen this guide is about: shown only to people who can open it, with an "Open …" link. */
  path?: string
  pathLabel?: string
  /** Only for the workspace admin (setup, users, settings). */
  adminOnly?: boolean
  /** Shown in the technician app's help, not in the office guide list. */
  technicianOnly?: boolean
  sections: GuideSection[]
}

const gettingStarted: Guide = {
  id: 'getting-started',
  title: 'Getting started',
  summary: 'Set up a new workspace: company details, branding, email, your team and your first work.',
  icon: Rocket,
  adminOnly: true,
  path: '/onboarding',
  pathLabel: 'Getting Started',
  sections: [
    {
      title: 'First steps',
      steps: [
        'Go to Settings → Company Profile and fill in your legal name, address and contact details. They appear on quotes, invoices and service reports.',
        'Go to Settings → Branding to upload your logo and set your colours.',
        'Go to Settings → Email to set the sender name and where client replies should go.',
        'Go to Users & Access → Invite User to add your team, choosing each person’s role.',
        'Open Getting Started (Settings → Getting Started) for quick buttons: Add Customer, Create Work Order, Create Invoice and Invite Team.',
      ],
    },
    {
      title: 'Starter packs for your trade',
      text: 'Getting Started, PM Templates and Asset Categories show starter packs for Generator, HVAC and Solar contractors. Each adds asset categories with datasheet fields plus a detailed preventive checklist and a breakdown checklist.',
      steps: ['Read what the pack contains on its card.', 'Click Install … pack.', 'Edit or remove anything it added under Settings → PM Templates and Settings → Asset Categories.'],
      tip: 'Every new workspace already has four basic checklists (HVAC, Generator, UPS, Solar). Packs add the detailed versions.',
    },
    {
      title: 'Before your first job',
      steps: [
        'Add your clients and their sites (Clients → New Client).',
        'Register the equipment you maintain under Assets, choosing a category so its datasheet fields appear.',
        'Check Settings → Numbering Rules if you want your own prefixes for work orders, invoices and reports.',
      ],
    },
  ],
}

const usersAccess: Guide = {
  id: 'users-access',
  title: 'Users, roles & access',
  summary: 'Invite people, choose their role, change it later, and control what each role can do.',
  icon: Users,
  adminOnly: true,
  path: '/team/users',
  pathLabel: 'Users & Access',
  sections: [
    {
      title: 'Roles',
      text: 'The role decides which screens someone sees and where they land after signing in.',
      steps: [
        'Admin — full access to everything, including settings and users.',
        'Manager — operations, finance figures, approvals and reports.',
        'Dispatcher — work orders, the scheduling board, live map, clients and maintenance.',
        'Technician — the mobile technician app: their jobs, time & leave, guides.',
        'Materials Controller — stores and logistics: stock, issuing parts, purchase orders, suppliers, transfers and vans. Starts on Inventory.',
        'Finance — quotes, invoices, payments, expenses and finance reports. Starts on Finance.',
        'Viewer — read-only access to operations.',
      ],
    },
    {
      title: 'Invite someone',
      steps: [
        'Open Users & Access and click Invite User.',
        'Enter their full name and email, pick a role, and click Send Invite. They get an email with a link to set their password.',
        'No email address? Tick “This person doesn’t have an email address”, enter a username and click Create User. A temporary password is shown once: give it to them; they choose their own at first sign-in.',
      ],
      tip: 'If an invitation didn’t arrive, open the person and click Resend Invite. Pending invitations use a licensed seat.',
    },
    {
      title: 'Change someone’s role',
      steps: [
        'Open Users & Access and click the person’s name. Their role is shown under their name and in their profile.',
        'Click Change Role, pick the new role and click Save Changes.',
        'They are signed out of open sessions, so the new role applies when they sign in again.',
      ],
      tip: 'Changing the role resets their permissions to that role’s defaults. You can’t change your own role.',
    },
    {
      title: 'Fine-tune permissions',
      steps: [
        'Users & Access → Roles & Permissions: tick what each role can do (for example, let Materials Controllers override stock levels).',
        'On a person’s page you can adjust their own permissions, branch access and teams.',
        'Use Reset Password to send a reset link (or generate a password for people without email), and Deactivate when someone leaves.',
      ],
    },
  ],
}

const serviceReports: Guide = {
  id: 'service-reports',
  title: 'Service reports',
  summary: 'Check, edit, approve and send the report for a finished job, and design how it looks.',
  icon: FileText,
  path: '/work-orders',
  pathLabel: 'Work Orders',
  sections: [
    {
      title: 'Review and edit a report',
      steps: [
        'Open the work order and go to the Report tab.',
        'Click Edit report to change the Problem reported, Findings and Work done wording. The technician’s own notes are kept; Use original puts a section back.',
        'Under Photos, untick In report for photos you don’t want the client to see.',
        'Click Download PDF to check how it looks.',
      ],
      tip: 'Editing an approved report sends it back for approval. Once a report is sent or acknowledged it is locked.',
    },
    {
      title: 'Approve and send',
      steps: [
        'Reports waiting for review are listed in the work orders report queue.',
        'Approve the report, then send it: it is emailed to the client’s contact as a PDF.',
        'Settings → Operational Defaults can send approved reports automatically.',
      ],
    },
    {
      title: 'Photos and annotations',
      text: 'Photos marked to include appear in the report, grouped by category. Photos annotated in the office (circles, arrows, text) are saved with the drawing and appear as annotated.',
    },
    {
      title: 'Design the report',
      steps: [
        'Go to Settings → Document Templates and choose Service Report.',
        'Set the logo position and size, colours, fonts, header background, intro text, page numbers, date format and signature lines.',
        'Under Sections, switch sections on or off and change their order. Choose whether photos appear inline or as an appendix, and how many fit on a page.',
        'Click Save, then Download sample PDF to check it.',
      ],
    },
  ],
}

const technicianApp: Guide = {
  id: 'technician-app',
  title: 'Technician app',
  summary: 'What technicians do on their phone, from accepting a job to the client’s signature.',
  icon: Smartphone,
  sections: [
    {
      title: 'Your jobs',
      steps: [
        'Sign in on your phone. Jobs shows My Jobs: Active and Done.',
        'Tap a job to open it. The badge under the title tells you the next step.',
        'Tap Accept Job (or Decline Job from the ⋯ menu).',
      ],
    },
    {
      title: 'On site',
      steps: [
        'When you get there, tap I’ve Arrived.',
        'Tap Start Work.',
        'Work through the checklist on the Overview tab, and log Materials used.',
        'On the Photos tab, choose Before, During or After, take or choose a photo, add a caption and tap Upload evidence.',
        'On the Parts tab, request parts from stores (pick a stock item or describe the part) or return Unused / Faulty parts.',
      ],
    },
    {
      title: 'Leaving site and multi-day jobs',
      steps: [
        'At the end of the day, open the ⋯ menu and tap Record Departure.',
        'Next time you come back, tap I’m back on site on the job. Each day is recorded as its own visit.',
        'Waiting for parts or the client? Use Put On Hold from the ⋯ menu, then Resume.',
      ],
    },
    {
      title: 'Finish the job',
      steps: [
        'Open the Finish tab and write the Work done: what the fault was and what you did. It goes on the service report.',
        'Capture Your signature and the Client signature (with the client’s name and role).',
        'Tap Complete. An administrator reviews and closes the job.',
      ],
    },
    {
      title: 'No signal?',
      text: 'Keep working. Everything you do is saved on the phone and sent when you are back online; the job shows “waiting to sync” until then.',
    },
    {
      title: 'Time, guides and your account',
      steps: [
        'Time & Leave: clock in and out, and request leave.',
        'Guides: search manuals, fixes and how-tos from the knowledge base.',
        'Me: change your password, choose notifications, and see your van’s stock.',
      ],
    },
  ],
}

const maintenanceTemplates: Guide = {
  id: 'pm-templates',
  title: 'Checklists & starter packs',
  summary: 'Preventive maintenance checklists and the ready-made trade packs.',
  icon: ClipboardList,
  path: '/settings/pm-templates',
  pathLabel: 'PM Templates',
  adminOnly: true,
  sections: [
    {
      title: 'PM templates',
      steps: [
        'Go to Settings → PM Templates.',
        'Click New Template, name it and add sections and questions: Yes / No, Pass / Fail, Condition Rating, Number, Meter Reading, Short or Long Text, Date, Dropdown, Photo, Signature or Checkbox.',
        'Duplicate a template to make a variant; deactivate ones you no longer use.',
      ],
    },
    {
      title: 'Starter packs',
      text: 'Install a Generator, HVAC or Solar pack to add detailed preventive and breakdown checklists plus matching asset categories in one click. Installed copies are yours to edit.',
    },
  ],
}

const assets: Guide = {
  id: 'assets',
  title: 'Assets & asset categories',
  summary: 'Register client equipment with datasheet details, and define what each kind of equipment records.',
  icon: Boxes,
  path: '/assets',
  pathLabel: 'Assets',
  sections: [
    {
      title: 'Add an asset',
      steps: [
        'Go to Assets and click New Asset.',
        'Choose the client and site.',
        'Pick a Category: its datasheet fields appear (for a diesel generator: make, model, serial number, kVA, fuel type…). Fields marked * are required.',
        'Fill in the name, code (left blank, one is generated), location, warranty and PM frequency, then click Create Asset.',
      ],
      tip: 'Filter the register by category with the Category drop-down. The asset’s page shows its datasheet.',
    },
    {
      title: 'Asset categories (admins)',
      steps: [
        'Go to Settings → Asset Categories.',
        'Click Add category, or pick an existing one.',
        'Click Add field to add a datasheet field: label, type (text, number, date, dropdown, long text), unit, and whether it’s required.',
        'Use Switch off to retire a field without losing values already recorded.',
      ],
    },
  ],
}

const emailIntake: Guide = {
  id: 'email',
  title: 'Email & email intake',
  summary: 'Who your emails come from, turning client emails into work orders, rules and auto-replies.',
  icon: Inbox,
  adminOnly: true,
  path: '/settings/email-intake',
  pathLabel: 'Email Intake',
  sections: [
    {
      title: 'Your workspace email',
      steps: [
        'Settings → Email: set the sender name and the addresses replies should go to (general, finance, operations).',
        'To send from your own mail server, tick “Send from our own mail server (advanced)”, enter the SMTP details, save, and send a test email.',
        'Change the wording of any email under Settings → Email Templates.',
      ],
    },
    {
      title: 'Turn emails into work orders',
      steps: [
        'Settings → Email Intake: tick “Turn emails into work orders”.',
        'Enter the service email address and the IMAP details of that mailbox (server, username, password or app password), then click Test connection and Save Changes.',
        'The mailbox is read every minute. An email from a known client contact becomes a work order; a reply quoting a work order number is added to that job.',
        'Choose whether emails from unknown senders are logged under a default client, optionally only from certain domains.',
        'Recent emails shows what became of each one.',
      ],
      tip: 'Auto-replies (request received, added to job, not recognised) can be switched on or off on the same page.',
    },
    {
      title: 'Intake rules',
      steps: [
        'On the Email Intake page, add a ready-made rule (urgent = Critical, generator faults, AC not cooling, water leaks, ignore newsletters…) or click New rule.',
        'Set conditions on the subject, body or sender, and choose what happens: set the priority, work type, team or title, accept a sender for a client, or ignore the email.',
        'Rules are tried top to bottom and the first match wins. Use Try a sample email to check which rule applies.',
      ],
    },
  ],
}

const settings: Guide = {
  id: 'settings',
  title: 'Settings overview',
  summary: 'Where to find each workspace setting.',
  icon: Settings2,
  adminOnly: true,
  path: '/settings',
  pathLabel: 'Settings',
  sections: [
    {
      title: 'What’s where',
      steps: [
        'Company Profile, Branding and Document Templates: how you appear on documents.',
        'Email, Email Intake, Email Templates and Delivery Logs: email in and out.',
        'Numbering Rules, Operational Defaults, PM Templates, Work Order Templates, Asset Categories, SLA Policies, Status Workflows and Escalation Rules: how work runs.',
        'Finance Defaults, Payments, Credit Control and Approval Matrix: money.',
        'Portal, Notifications, Notification Templates, Security and Audit Logs: clients, alerts and control.',
      ],
    },
  ],
}

export const guides: Guide[] = [
  gettingStarted,
  technicianApp,
  serviceReports,
  assets,
  maintenanceTemplates,
  emailIntake,
  usersAccess,
  settings,
]

export const technicianGuide = technicianApp
