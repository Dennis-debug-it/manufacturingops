import type { LucideIcon } from 'lucide-react'
import {
  BarChart3,
  Boxes,
  CheckSquare,
  ClipboardList,
  Clock,
  FileText,
  Package,
  Rocket,
  Settings2,
  Smartphone,
  Users,
  Wallet,
  Wrench,
} from 'lucide-react'
import type { AppRole } from '../../shared/types/auth'

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
        'Go to Settings → Company Profile and fill in your legal name, address and contact details. They appear on service reports and other documents.',
        'Go to Settings → Branding to upload your logo and set your colours.',
        'Go to Settings → Email to set the sender name and where replies should go.',
        'Go to Users & Access → Invite User to add your team, choosing each person’s role.',
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
        'Set up your factories and their sections under Settings → Organisation.',
        'Register the equipment you maintain under Assets, choosing a category so its datasheet fields appear.',
        'Check Settings → Numbering Rules if you want your own prefixes for work orders and reports.',
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
        'Manager — operations, maintenance, finance figures, approvals and reports.',
        'Dispatcher — work orders and maintenance.',
        'Technician — the mobile technician app: their jobs, time & leave, guides.',
        'Materials Controller — stores and logistics: stock, issuing parts, purchase orders, suppliers and transfers. Starts on Inventory.',
        'Finance — exchange rates, approvals and maintenance costs. Starts on Finance.',
        'Viewer — read-only access to operations and maintenance.',
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
        'Users & Access → Roles & Permissions: tick what each role can do (for example, let Materials Controllers override stock levels, or let Dispatchers view reports).',
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
        'Under Photos, untick In report for photos you don’t want on the report.',
        'Click Download PDF to check how it looks.',
      ],
      tip: 'Editing an approved report sends it back for approval.',
    },
    {
      title: 'Approve',
      steps: [
        'Reports waiting for review are listed under Work Orders → Report Queue. Filter the list, then Generate, Approve, open the PDF or Download ZIP for several at once.',
        'Approve the report once it reads right; editing it afterwards sends it back for approval.',
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
  summary: 'What technicians do on their phone, from accepting a job to signing it off.',
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
      title: 'Doing the work',
      steps: [
        'Tap Start Work.',
        'Work through the checklist on the Overview tab, and log Materials used.',
        'On the Photos tab, choose Before, During or After, take or choose a photo, add a caption and tap Upload evidence.',
        'On the Parts tab, request parts from stores (pick a stock item or describe the part) or return Unused / Faulty parts.',
      ],
    },
    {
      title: 'Waiting for something',
      text: 'Waiting for parts? Use Put On Hold from the ⋯ menu, then Resume.',
    },
    {
      title: 'Finish the job',
      steps: [
        'Open the Finish tab, check the Problem reported, then write your Findings (what you found and why it failed) and the Work done (what you did). Both go on the service report and are required to complete the job.',
        'Capture your signature and the sign-off signature (with the name and role of the person signing).',
        'Tap Complete. An administrator reviews and closes the job.',
      ],
    },
    {
      title: 'No signal?',
      text: 'Keep working. Everything you do is saved on the phone and sent when you are back online; the job shows “waiting to sync” until then.',
    },
    {
      title: 'Time and your account',
      steps: [
        'Time & Leave: clock in and out, and request leave.',
        'Me: change your password, choose notifications, and confirm the tools you hold.',
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
  summary: 'Register factory equipment with datasheet details, and define what each kind of equipment records.',
  icon: Boxes,
  path: '/assets',
  pathLabel: 'Assets',
  sections: [
    {
      title: 'Add an asset',
      steps: [
        'Go to Assets and click New Asset.',
        'Choose where the asset is located.',
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
      text: 'All Work Orders lists every job. Switch between the Table, Kanban and Calendar views, and use Filters to narrow by status, priority, technician or date. Report Queue lists finished jobs whose service report needs attention.',
    },
    {
      title: 'Create a job',
      text: 'New jobs are raised from Jobs (or by scanning an asset), against the factory section and asset they are for.',
    },
    {
      title: 'Move a job along',
      steps: [
        'Open the work order. The tabs are Overview, Work, Activity and Report.',
        'The main button changes with the status: Assign, then Start Work, then Complete (add the Work done notes and click Mark complete), then Close.',
        'Technicians record their checklist, parts and photos from the technician app; you see it live on the Work tab.',
      ],
    },
    {
      title: 'Other actions',
      text: 'Under More actions: Put On Hold (On Hold (general) or Waiting on Parts), Resume, Reassign, Reopen, Duplicate and Cancel Work Order.',
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
        'Choose the asset(s) the schedule covers.',
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
  summary: 'Parts, stock levels, issuing to jobs, tools, stock counts, purchasing, suppliers and reports.',
  icon: Package,
  path: '/inventory',
  pathLabel: 'Inventory',
  sections: [
    {
      title: 'Parts and stock',
      steps: [
        'Inventory opens on Overview: one list of what needs doing (requests to approve or issue, low stock, late deliveries, returns, counts in progress) and the stock at each branch.',
        'On Stock levels or Parts catalogue, click New Part, or Bulk Import a spreadsheet with opening quantities and the branch each goes into. Set the reorder level so you are warned before you run out.',
        'Re-importing a spreadsheet updates parts whose code is already in the catalogue: name, category, unit, reorder level and cost are replaced, and a quantity sets the stock at that branch. Leave the quantity blank to keep stock as it is.',
        'A part cannot be deactivated while it still has stock, open requests, purchase orders to receive, or is on an open count.',
        'Buy stock with a purchase order. On a part, Manual receipt is for stock that arrives any other way (a reason is required); Adjust Stock corrects a count. Both ask which branch, and can record batch or serial numbers.',
        'Stock levels shows each part by branch, or each location by part; filter by location, category or low stock. A part’s Stock by Location tab shows every branch holding it.',
        'Movements lists every receipt, issue, return, adjustment and transfer, filterable by type, branch, dates and search (including serial numbers).',
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
        'Under Stock counts, click New count and choose a branch (and a category to count part of the store). Tick Only parts held here to skip parts never stocked there, and Blind count to hide book quantities from counters.',
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
        'Parts a technician logs on a job come off the branch stock.',
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
      text: 'Inventory Reports has Stock Valuation by branch, Usage over a date range (parts used less parts returned), Purchase Orders, and a Movements export. Each exports to CSV.',
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
  summary: 'Exchange rates for parts bought in another currency.',
  icon: Wallet,
  path: '/finance',
  pathLabel: 'Finance',
  sections: [
    {
      title: 'Where to find things',
      text: 'Exchange Rates holds the rates used when parts are bought in another currency. Labour is costed from each technician’s hourly rate, set under Maintenance team.',
    },
    {
      title: 'Costing',
      text: 'Job and asset costs, and the spend against each budget, are under Maintenance → Maintenance costs.',
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

const reports: Guide = {
  id: 'reports',
  title: 'Reports and dashboards',
  summary: 'How maintenance is going: work orders, SLAs, preventive maintenance and technicians.',
  icon: BarChart3,
  path: '/reports',
  pathLabel: 'Reports',
  sections: [
    {
      title: 'Reports',
      text: 'Reports has tabs for Work Order, SLA, PM Compliance and Technician Performance. Pick a date range and filters; Export CSV downloads the figures.',
    },
    {
      title: 'Executive view',
      text: 'The Executive View link on the Dashboard shows the headline numbers on one page.',
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
        'Email, Email Templates and Delivery Logs: the email the workspace sends.',
        'Numbering Rules, Operational Defaults, PM Templates, Work Order Templates, Asset Categories, SLA Policies, Status Workflows and Escalation Rules: how work runs.',
        'Approval Matrix: who signs off purchase orders.',
        'Sensors, Integrations and Monitoring Intake: data coming in from machines and other systems.',
        'Notifications, Notification Templates, Security and Audit Logs: alerts and control.',
      ],
    },
  ],
}

export const guides: Guide[] = [
  gettingStarted,
  workOrders,
  serviceReports,
  maintenance,
  assets,
  inventory,
  finance,
  approvals,
  timeAttendance,
  reports,
  technicianApp,
  maintenanceTemplates,
  usersAccess,
  settings,
]

export const technicianGuide = technicianApp
