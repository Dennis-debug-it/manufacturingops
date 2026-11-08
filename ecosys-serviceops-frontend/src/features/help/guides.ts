import type { LucideIcon } from 'lucide-react'
import {
  BarChart3,
  BookOpen,
  Boxes,
  Building2,
  CheckSquare,
  ClipboardList,
  Clock,
  FileSignature,
  FileText,
  Inbox,
  Package,
  Rocket,
  Settings2,
  Smartphone,
  Users,
  Wallet,
  Wrench,
} from 'lucide-react'
import type { AppRole } from '../../shared/types/auth'
import { EMAIL_INTAKE_ENABLED } from '../../shared/config/features'

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
  /** Only for these roles (the admin always sees every guide). */
  roles?: AppRole[]
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
        'Manager — operations, finance figures, approvals, reports and the sales pipeline.',
        'Dispatcher — work orders, clients and maintenance.',
        'Technician — the mobile technician app: their jobs, time & leave, guides.',
        'Materials Controller — stores and logistics: stock, issuing parts, purchase orders, suppliers, transfers and vans. Starts on Inventory.',
        'Finance — quotes, invoices, payments, expenses, finance reports and the sales pipeline. Starts on Finance.',
        'Viewer — read-only access to operations and the sales pipeline.',
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
        'Users & Access → Roles & Permissions: tick what each role can do (for example, let Materials Controllers override stock levels, or give Dispatchers the sales pipeline under Sales CRM).',
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
        'Reports waiting for review are listed under Work Orders → Report Queue. Filter the list, then Generate, Approve, Send, open the PDF or Download ZIP for several at once.',
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
        'Open the Finish tab, check the Problem reported, then write your Findings (what you found and why it failed) and the Work done (what you did). Both go on the service report and are required to complete the job.',
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
        'Rules are tried top to bottom and the first match wins. Paste an email under the rules and click Test this email to see which rule applies.',
      ],
    },
  ],
}

const workOrders: Guide = {
  id: 'work-orders',
  title: 'Work orders',
  summary: 'Log a job, assign it, follow it through to completion and close it.',
  icon: ClipboardList,
  path: '/work-orders',
  pathLabel: 'Work Orders',
  sections: [
    {
      title: 'Find work orders',
      text: 'All Work Orders lists every job. Switch between the Table, Kanban, Calendar and Map views, and use Filters to narrow by status, priority, client, technician or date. Report Queue lists finished jobs whose service report needs attention.',
    },
    {
      title: 'Create a work order',
      steps: [
        'Click New Work Order.',
        'Optionally pick a Template to fill in the details, then choose the Client, Site and (if it is about one machine) the Asset.',
        'Enter the Title, Type, Priority, Due Date and a Description of the problem.',
        'Click Create Work Order. The SLA clock starts from the priority and the client’s SLA policy.',
      ],
      tip: `${EMAIL_INTAKE_ENABLED ? 'Emails to your intake address can create work orders automatically; see the Email intake guide. ' : ''}On Suite, a won sales deal becomes a work order with one click; see the Sales pipeline guide.`,
    },
    {
      title: 'Move a job along',
      steps: [
        'Open the work order. The tabs are Overview, Work, Activity and Report.',
        'The main button changes with the status: Assign, then Start Work, then Complete (add the Work done notes and click Mark complete), then Close.',
        'Technicians record their arrival, departure, checklist, parts and photos from the technician app; you see it live on the Work tab.',
      ],
    },
    {
      title: 'Jobs that take more than one visit',
      text: 'Each trip to site is recorded as a visit. The crew panel shows whether a technician is On Site or has Left Site, with their first arrival (used for the SLA), latest arrival and latest departure.',
    },
    {
      title: 'Other actions',
      text: 'Under More actions: Put On Hold (On Hold (general), Waiting on Parts or Waiting on Client), Resume, Reassign, Create Follow-up, Reopen, Duplicate and Cancel Work Order.',
    },
  ],
}

const maintenance: Guide = {
  id: 'maintenance',
  title: 'Planned maintenance',
  summary: 'Set up recurring service for assets and keep track of what’s due and done.',
  icon: Wrench,
  path: '/maintenance/calendar',
  pathLabel: 'Maintenance',
  sections: [
    {
      title: 'Where to look',
      text: 'Maintenance has the Calendar (what’s due when), PM Schedules (each recurring plan), PM Compliance (done on time vs late or missed) and Engine Status.',
    },
    {
      title: 'Create a PM schedule',
      steps: [
        'Go to PM Schedules and click New Schedule.',
        'Asset & Coverage: choose the client, site and asset(s).',
        'Template & Schedule: pick the PM template (the checklist) and the frequency: Monthly, Quarterly, Semi-Annual or Annual, and the start date.',
        'Assignment & WO Settings: choose whether work orders are created automatically (Auto-Schedule) and how far ahead.',
        'Click Create Schedule.',
      ],
      tip: 'Need a work order today? Open the schedule and click Generate Work Order Now. Technicians are assigned on the work order that is created.',
    },
    {
      title: 'Checklists',
      text: 'The checklist technicians fill in comes from the PM template. Admins manage templates under Settings → PM Templates, where starter packs add ready-made ones.',
    },
  ],
}

const inventory: Guide = {
  id: 'inventory',
  title: 'Inventory',
  summary: 'Parts, stock levels, issuing to jobs, van stock, tools, stock counts, purchasing, suppliers and reports.',
  icon: Package,
  path: '/inventory',
  pathLabel: 'Inventory',
  sections: [
    {
      title: 'Parts and stock',
      steps: [
        'Inventory opens on Overview: one list of what needs doing (requests to approve or issue, low stock, late deliveries, returns, counts in progress) and the stock at each branch and van.',
        'On Stock levels or Parts catalogue, click New Part, or Bulk Import a spreadsheet with opening quantities and the branch each goes into. Set the reorder level so you are warned before you run out.',
        'Re-importing a spreadsheet updates parts whose code is already in the catalogue: name, category, unit, reorder level and cost are replaced, and a quantity sets the stock at that branch. Leave the quantity blank to keep stock as it is.',
        'A part cannot be deactivated while it still has stock, open requests, purchase orders to receive, or is on an open count.',
        'Buy stock with a purchase order. On a part, Manual receipt is for stock that arrives any other way (a reason is required); Adjust Stock corrects a count. Both ask which branch, and can record batch or serial numbers.',
        'Stock levels shows each part by branch and van, or each location by part; filter by location, category or low stock. A part’s Stock by Location tab shows every branch and van holding it.',
        'Movements lists every receipt, issue, return, adjustment, transfer and van move, filterable by type, branch, dates and search (including serial numbers).',
      ],
    },
    {
      title: 'Transfers between branches',
      steps: [
        'Under Movements, open Transfers and click New Transfer: choose the two branches, the part and the quantity.',
        'Approve it, then Complete it when the stock arrives. Stock only moves on Complete.',
      ],
    },
    {
      title: 'Stock counts',
      steps: [
        'Under Stock counts, click New count and choose a branch or a van (and a category to count part of the store). Tick Only parts held here to skip parts never stocked there, and Blind count to hide book quantities from counters.',
        'Count on screen, with Quick entry (scan or type a code and quantity), or on paper: download or print the count sheet, fill in the Counted column and import it back. Add part covers anything found that the count did not list.',
        'Tick Recount on a line to have it checked again; posting waits until it is. Use the Not counted, Variances and Recount filters to finish, and save as you go.',
        'Post count adjusts each counted part by the difference from what the books said when the count opened. Parts you did not count are left alone. If the differences are worth more than the approval limit in Settings → Inventory, someone who approves materials must post it.',
      ],
    },
    {
      title: 'Requests: one queue for stores',
      steps: [
        'Requests lists every ask for parts in one place: technicians’ parts requests from a work order’s Parts tab, and issue slips raised by stores. Each moves Requested → Approved → Issued → Completed.',
        'Click a request to act on it. Approve can approve less than asked on a technician’s request; Reject asks for a reason the requester sees; Issue stock (or Dispatch) lets you hand over less than asked. Dispatching books the parts as used on the job.',
        'Click New issue slip to hand parts out yourself, against a work order, a project or neither. Slips worth more than the limit in Settings → Inventory wait for approval.',
        'Under Returns, Receive or Reject parts coming back from jobs, or click Record return when a technician hands parts in at the counter.',
        'Parts received as Faulty are held under Faulty parts. Click Deal with it to record them as returned to the supplier (with the RMA number), written off, or back into stock.',
        'Stores is notified of new requests and of parts falling to their reorder level; the person who asked is notified when their request is approved, rejected or dispatched. Each person can change this under their notification preferences.',
        'Parts a technician logs on a job come off their van when it carries enough, otherwise from the branch.',
        'If a technician buys a part at a supplier\'s counter, they tick "I bought this directly from a supplier" when logging it. Stock may go below zero; the purchase waits under Purchasing → Direct purchases. When the supplier\'s invoice arrives, tick the purchases on it and click Reconcile — they are booked back in and the balance is right again. Stores can also record a direct purchase from a handed-in receipt.',
      ],
    },
    {
      title: 'Buying stock',
      steps: [
        'Add your suppliers under Suppliers with New Supplier, including their lead time. Edit or Deactivate a supplier from its page.',
        'Under Alerts, the Reorder view lists parts at or below their reorder level with what is already on order and a suggested quantity. Tick the parts and click Draft Purchase Orders to create one draft per supplier.',
        'Or, under Purchase Orders, click New Purchase Order, pick each part from the list so it is linked to stock, and Create Purchase Order. Purchase orders above your approval limit go to My Approvals first.',
        'On a purchase order, Download PDF prints it in your purchase order design, and Email to supplier sends the PDF and marks a draft Sent. A draft can be edited; changing its total sends it for approval again.',
        'Mark Sent once it is with the supplier. When goods arrive, click Receive on a line, enter the quantity, the branch it goes into and any batch or serial numbers; linked parts go into stock and their cost is averaged in. Lines marked Non-stock only record the delivery.',
      ],
    },
    {
      title: 'Tools and test equipment',
      steps: [
        'Under Tools, click New Tool and give it an asset tag, name and value. If it needs calibration or inspection, set how often and when it is next due.',
        'On a tool, Check Out hands it to a technician (with an expected return date). They see it under Me → My tools and tap I have it to confirm.',
        'Check In takes it back with its condition; a damaged tool can go straight to repair. Mark Lost needs a reason and records who last had it.',
        'Record Calibration keeps the certificate on file and moves the due date on. A tool past its calibration date cannot be checked out without an override, and a failed calibration sends it to repair.',
        'The cards at the top of Tools show overdue calibrations, tools overdue back and tools not yet confirmed; a technician’s profile lists the tools they hold.',
      ],
    },
    {
      title: 'Reports',
      text: 'Inventory Reports has Stock Valuation by branch and van, Usage over a date range (parts used less parts returned), Purchase Orders, and a Movements export. Each exports to CSV.',
    },
    {
      title: 'Settings',
      text: 'Settings → Inventory sets the critical stock threshold, whether stock may go negative, the approval limit for issues, auto-deduct on job completion and the default supplier lead time.',
    },
  ],
}

const finance: Guide = {
  id: 'finance',
  title: 'Finance',
  summary: 'Quotes, invoices, payments, credit notes, expenses, bills and finance reports.',
  icon: Wallet,
  path: '/finance',
  pathLabel: 'Finance',
  sections: [
    {
      title: 'Where to find things',
      text: 'Finance is grouped into Documents (Documents, Quotes, Invoices, Credit Notes, Receipts), Pricing (Rate Cards, Preferential Rates, Exchange Rates), Control (Approvals, Credit Control, Aging) and Reports (Reports, Expenses, Payables).',
    },
    {
      title: 'Quote to invoice',
      steps: [
        'Under Quotes, click New Quote, add the lines and click Create Quote.',
        'Click Send to email it. When the client agrees (or accepts it in the client portal) click Accept.',
        'Click Convert to Invoice, or Build Proforma if you need payment first.',
      ],
    },
    {
      title: 'Invoices and payments',
      steps: [
        'Under Invoices, click New Invoice, add the lines and click Create Invoice.',
        'When the client pays, open the invoice and click Record Payment (Cash, MPesa, BankTransfer, Cheque or Card).',
        'For one payment that covers several invoices, go to Receipts → Receive Payment and choose Apply to invoices, or keep it On account.',
      ],
    },
    {
      title: 'Credit notes',
      text: 'Under Credit Notes, click New Credit Note, then Create Draft, then Issue when it is right.',
    },
    {
      title: 'Expenses and bills',
      text: 'Staff claim expenses under Expenses with New Expense Claim and Submit Claim; claims go for approval. Supplier bills are entered under Payables with New Bill and Create Bill.',
    },
    {
      title: 'Chasing payment',
      text: 'Credit Control and Aging show who owes what and for how long. Open a client’s Statement and click Email Statement to send it.',
    },
    {
      title: 'Finance reports',
      text: 'Reports has Revenue Summary, Collections, By Client, Outstanding Receivables, Payables Aging and Contract P&L.',
    },
  ],
}

const contracts: Guide = {
  id: 'contracts',
  title: 'Contracts',
  summary: 'Service contracts: what’s covered, SLA terms, billing, approval and renewal.',
  icon: FileSignature,
  path: '/contracts',
  pathLabel: 'Contracts',
  sections: [
    {
      title: 'Create a contract',
      steps: [
        'Click New Contract and choose the client.',
        'Set what is covered: by asset category or specific assets, and the billing model.',
        'Click Create Contract. It starts as a Draft.',
      ],
    },
    {
      title: 'Approve and activate',
      steps: [
        'Click Submit for Approval; an approver clicks Approve.',
        'Click Activate to start it. Near the end date, Start Renewal creates the next term.',
        'You can also Suspend, Resume, Terminate or Cancel a contract.',
      ],
    },
    {
      title: 'Contract details',
      text: 'The tabs are Overview, Covered Sites/Assets, SLA Terms, Preferential Rates, PM Terms, Billing and P&L (what the contract earns against what the work costs).',
    },
  ],
}

const timeAttendance: Guide = {
  id: 'time-attendance',
  title: 'Time and leave',
  summary: 'Clock in and out, apply for leave, and (for admins) approve timesheets and leave.',
  icon: Clock,
  roles: ['manager', 'dispatcher', 'materials_controller'],
  sections: [
    {
      title: 'Your own time (My Time & Leave)',
      steps: [
        'Open My Time & Leave from the menu. Click Clock In when you start and Clock Out when you finish.',
        'Use Take Break and End Break for breaks, and Travelling while you drive between sites.',
        'To take time off, click Apply for leave, choose the type and dates, and click Submit request.',
      ],
      tip: 'Technicians do the same from the technician app.',
    },
    {
      title: 'Managing the team (admin)',
      steps: [
        'Go to Users & Access → Time & Attendance.',
        'Live shows who is clocked in, on a break or travelling right now.',
        'Timesheets lists everyone’s hours to check and approve.',
        'Leave lists leave requests to Approve or Reject; New Leave Request and Submit Request add one for someone.',
        'Reports shows hours and leave over a period; Export downloads it.',
      ],
    },
  ],
}

const knowledge: Guide = {
  id: 'knowledge',
  title: 'Knowledge base',
  summary: 'Write how-to articles and procedures for your team and clients.',
  icon: BookOpen,
  path: '/knowledge',
  pathLabel: 'Knowledge',
  sections: [
    {
      title: 'Write an article',
      steps: [
        'Click New Category to group articles (for example Generators or Safety), then New Article.',
        'Write the body in Markdown: headings, lists, links and images.',
        'Choose the Visibility: Internal (all staff), Technicians only, Admins only, or Public (visible in the client portal).',
        'Click Save to keep a draft, then Publish when it’s ready.',
      ],
      tip: 'Technicians find published articles under Guides in the technician app, even on site.',
    },
  ],
}

const reports: Guide = {
  id: 'reports',
  title: 'Reports and dashboards',
  summary: 'How the business is doing: work orders, SLAs, maintenance, money, technicians and client satisfaction.',
  icon: BarChart3,
  path: '/reports',
  pathLabel: 'Reports',
  sections: [
    {
      title: 'Reports',
      text: 'Reports has tabs for Work Order, SLA, PM Compliance, Finance, Technician Performance and CSAT (client satisfaction). Pick a date range and filters; Export CSV downloads the figures.',
    },
    {
      title: 'Executive view',
      text: 'The Executive View link on the Dashboard shows the headline numbers for operations and finance on one page.',
    },
  ],
}

const approvals: Guide = {
  id: 'approvals',
  title: 'Approvals',
  summary: 'Approve purchase orders and expense claims routed to you.',
  icon: CheckSquare,
  path: '/approvals',
  pathLabel: 'My Approvals',
  roles: ['manager', 'finance', 'materials_controller'],
  sections: [
    {
      title: 'Approve or reject',
      steps: [
        'My Approvals lists the purchase orders and expense claims waiting for you. Who approves what depends on the amount, set in Settings → Approval Matrix.',
        'Open an item, check it, and click Approve (then Confirm Approval) or Reject with a reason.',
      ],
    },
  ],
}

const clients: Guide = {
  id: 'clients',
  title: 'Clients and sites',
  summary: 'Add clients, their sites and contacts, and see everything you do for them.',
  icon: Building2,
  path: '/clients',
  pathLabel: 'Clients',
  sections: [
    {
      title: 'Add a client',
      steps: [
        'Click New Client, fill in the name, contact and billing details, and click Create Client.',
        'Tick Give this client access to the client portal to invite their contact straight away.',
        'Have many clients already? Use Import to load them from a spreadsheet.',
      ],
    },
    {
      title: 'Sites',
      text: 'A site is a location you work at. On the client’s Sites tab click New Site, enter the address (and map location so jobs show on the Live Map) and click Create Site.',
    },
    {
      title: 'Everything about a client',
      text: 'The client page has tabs for Overview, Sites, Assets, Contracts, Work Orders, Documents and Client Portal.',
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
  workOrders,
  serviceReports,
  maintenance,
  clients,
  assets,
  inventory,
  contracts,
  finance,
  approvals,
  timeAttendance,
  knowledge,
  reports,
  technicianApp,
  maintenanceTemplates,
  ...(EMAIL_INTAKE_ENABLED ? [emailIntake] : []),
  usersAccess,
  settings,
]

export const technicianGuide = technicianApp
