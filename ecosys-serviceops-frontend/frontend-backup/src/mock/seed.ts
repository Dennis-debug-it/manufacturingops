import { addDaysIso, addHoursIso, nowIso, priorityToHours } from '../utils/date'
import { createAvatar, createId } from '../utils/id'
import type {
  ActivityTimelineEntry,
  AppDatabase,
  AssetRecord,
  AuthAccount,
  Branch,
  ClientRecord,
  ClientSite,
  InventoryItem,
  NumberingRule,
  SlaRuleRecord,
  TenantData,
  TenantFeatureFlag,
  TenantRecord,
  TenantUser,
  TechnicianRecord,
  WorkOrderRecord,
} from '../types/app'

export const DEFAULT_TENANT_ID = 'tenant-uhuru'
export const DEFAULT_TENANT_CODE = 'UHF-NBO'

const BRANCHES: Branch[] = [
  { id: 'branch-nairobi', name: 'Nairobi HQ', code: 'NRB', region: 'Nairobi', city: 'Nairobi', active: true, siteCount: 2 },
  { id: 'branch-mombasa', name: 'Mombasa Branch', code: 'MBA', region: 'Coast', city: 'Mombasa', active: true, siteCount: 2 },
  { id: 'branch-kisumu', name: 'Kisumu Hub', code: 'KSM', region: 'Western', city: 'Kisumu', active: true, siteCount: 2 },
]

const PLATFORM_FLAGS: TenantFeatureFlag[] = [
  { id: 'flag-1', name: 'Advanced SLA Routing', audience: 'Enterprise tenants', status: 'Enabled' },
  { id: 'flag-2', name: 'Client Acknowledgement Flows', audience: 'All active tenants', status: 'Enabled' },
  { id: 'flag-3', name: 'Auto PM Scheduler', audience: 'Pilot tenants', status: 'Pilot' },
]

const SITES: ClientSite[] = [
  { id: 'site-nairobi-hq', name: 'Nairobi HQ Data Hall', branchId: 'branch-nairobi', city: 'Nairobi', region: 'Nairobi', address: 'Mombasa Road, Nairobi', active: true },
  { id: 'site-nakuru', name: 'Nakuru Switching Centre', branchId: 'branch-nairobi', city: 'Nakuru', region: 'Rift Valley', address: 'Kenyatta Avenue, Nakuru', active: true },
  { id: 'site-miritini', name: 'Miritini Cold Room', branchId: 'branch-mombasa', city: 'Mombasa', region: 'Coast', address: 'Miritini Industrial Area', active: true },
  { id: 'site-coast', name: 'Coast Region Hub', branchId: 'branch-mombasa', city: 'Mombasa', region: 'Coast', address: 'Nyali, Mombasa', active: true },
  { id: 'site-kisumu', name: 'Kisumu Exchange', branchId: 'branch-kisumu', city: 'Kisumu', region: 'Western', address: 'Oginga Odinga Street, Kisumu', active: true },
  { id: 'site-eldoret', name: 'Eldoret POP', branchId: 'branch-kisumu', city: 'Eldoret', region: 'North Rift', address: 'Uganda Road, Eldoret', active: true },
]

function activity(actor: string, title: string, detail: string, createdAt: string, type: ActivityTimelineEntry['type']) {
  return { id: createId('act'), actor, title, detail, createdAt, type }
}

function rules(branches: Branch[]): NumberingRule[] {
  return branches.map((branch, index) => ({
    branchId: branch.id,
    branchName: branch.name,
    workOrderPrefix: `WO-${branch.code}`,
    assetPrefix: `AST-${branch.code}`,
    requisitionPrefix: `REQ-${branch.code}`,
    nextWorkOrderNumber: 200 + index * 50,
    nextAssetNumber: 100 + index * 50,
    nextRequisitionNumber: 10 + index * 10,
    resetRule: 'Annual',
  }))
}

function baseSlaRules(): SlaRuleRecord[] {
  return [
    { id: 'sla-1', name: 'Gold Critical', priorityLevel: 'Critical', responseTimeHours: 0.5, resolutionTimeHours: 4, escalationPath: 'Supervisor -> Branch Lead -> Tenant Admin', clientIds: [] },
    { id: 'sla-2', name: 'Silver Priority', priorityLevel: 'High', responseTimeHours: 1, resolutionTimeHours: 8, escalationPath: 'Dispatch -> Supervisor -> Tenant Admin', clientIds: [] },
    { id: 'sla-3', name: 'Standard Resolution', priorityLevel: 'Medium', responseTimeHours: 4, resolutionTimeHours: 24, escalationPath: 'Dispatch -> Supervisor', clientIds: [] },
  ]
}

function baseClients(slaRules: SlaRuleRecord[]): ClientRecord[] {
  return [
    {
      id: 'client-safaritel',
      name: 'Safaritel Kenya',
      branchId: 'branch-nairobi',
      contacts: [
        { id: 'c1', name: 'Noah Kariuki', email: 'noah.kariuki@safaritel.co.ke', phone: '+254 712 443 100', jobTitle: 'Facilities Lead' },
        { id: 'c2', name: 'Anne Wambui', email: 'anne.wambui@safaritel.co.ke', phone: '+254 722 545 221', jobTitle: 'NOC Supervisor' },
      ],
      sites: SITES.filter((site) => ['site-nairobi-hq', 'site-nakuru'].includes(site.id)),
      slaRuleId: slaRules[0].id,
      emailIntegrationStatus: 'Connected',
      brandingLogoUrl: '',
      notes: 'Telecom critical infrastructure coverage.',
    },
    {
      id: 'client-kifaru',
      name: 'Kifaru Logistics',
      branchId: 'branch-kisumu',
      contacts: [{ id: 'c3', name: 'Mercy Wanjiru', email: 'service@kifarulogistics.co.ke', phone: '+254 701 338 912', jobTitle: 'Operations Manager' }],
      sites: SITES.filter((site) => site.id === 'site-eldoret'),
      slaRuleId: slaRules[1].id,
      emailIntegrationStatus: 'Attention',
      brandingLogoUrl: '',
      notes: 'Regional logistics sites.',
    },
    {
      id: 'client-coastlink',
      name: 'Coastlink Cold Chain',
      branchId: 'branch-mombasa',
      contacts: [{ id: 'c4', name: 'Asha Khamis', email: 'ops@coastlink.co.ke', phone: '+254 723 922 110', jobTitle: 'Facility Coordinator' }],
      sites: SITES.filter((site) => site.id === 'site-miritini'),
      slaRuleId: slaRules[0].id,
      emailIntegrationStatus: 'Connected',
      brandingLogoUrl: '',
      notes: 'Cold room and HVAC intensive service coverage.',
    },
    {
      id: 'client-lakefront',
      name: 'Lakefront Finance',
      branchId: 'branch-kisumu',
      contacts: [{ id: 'c5', name: 'Linet Ouma', email: 'ops@lakefrontfinance.co.ke', phone: '+254 733 620 910', jobTitle: 'Branch Operations Head' }],
      sites: SITES.filter((site) => site.id === 'site-kisumu'),
      slaRuleId: slaRules[1].id,
      emailIntegrationStatus: 'Connected',
      brandingLogoUrl: '',
      notes: 'Financial branch support.',
    },
    {
      id: 'client-uhuru',
      name: 'Uhuru Mall Management',
      branchId: 'branch-mombasa',
      contacts: [{ id: 'c6', name: 'Peter Mwangangi', email: 'facilities@uhurumall.co.ke', phone: '+254 728 555 317', jobTitle: 'Site Supervisor' }],
      sites: SITES.filter((site) => site.id === 'site-coast'),
      slaRuleId: slaRules[2].id,
      emailIntegrationStatus: 'Attention',
      brandingLogoUrl: '',
      notes: 'Mixed-use retail facilities.',
    },
  ]
}

function baseTechnicians(): TechnicianRecord[] {
  return [
    { id: 'tech-1', fullName: 'Amina Mwangi', branchId: 'branch-nairobi', groupId: 'group-metro', skills: ['ATS Controllers', 'Generators', 'RCA'], availability: 'Assigned', status: 'On Site', activeWorkOrderIds: [], phone: '+254 712 000 111' },
    { id: 'tech-2', fullName: 'Kevin Maina', branchId: 'branch-nairobi', groupId: 'group-metro', skills: ['UPS', 'PDUs', 'Power Quality'], availability: 'Available', status: 'Idle', activeWorkOrderIds: [], phone: '+254 722 000 222' },
    { id: 'tech-3', fullName: 'Grace Njeri', branchId: 'branch-mombasa', groupId: 'group-coast', skills: ['Cooling', 'CRAC', 'Escalations'], availability: 'Assigned', status: 'On Site', activeWorkOrderIds: [], phone: '+254 733 000 333' },
    { id: 'tech-4', fullName: 'Brian Mutiso', branchId: 'branch-mombasa', groupId: 'group-coast', skills: ['Rectifiers', 'DC Power', 'Diagnostics'], availability: 'Available', status: 'Online', activeWorkOrderIds: [], phone: '+254 744 000 444' },
    { id: 'tech-5', fullName: 'David Otieno', branchId: 'branch-kisumu', groupId: 'group-western', skills: ['UPS', 'Cooling', 'Handover'], availability: 'Assigned', status: 'In Transit', activeWorkOrderIds: [], phone: '+254 755 000 555' },
    { id: 'tech-6', fullName: 'Mercy Achieng', branchId: 'branch-kisumu', groupId: 'group-western', skills: ['PM', 'Rectifiers', 'Scheduling'], availability: 'Assigned', status: 'On Site', activeWorkOrderIds: [], phone: '+254 766 000 666' },
    { id: 'tech-7', fullName: 'Joel Kiptoo', branchId: 'branch-kisumu', groupId: 'group-western', skills: ['Generators', 'Fuel Systems', 'Cooling'], availability: 'Available', status: 'Online', activeWorkOrderIds: [], phone: '+254 777 000 777' },
    { id: 'tech-8', fullName: 'Winnie Kendi', branchId: 'branch-nairobi', groupId: 'group-metro', skills: ['Fire Systems', 'PM Audits', 'Reporting'], availability: 'Available', status: 'Online', activeWorkOrderIds: [], phone: '+254 788 000 888' },
  ]
}

function baseUsers(): TenantUser[] {
  return [
    { id: 'user-admin-1', fullName: 'Mercy Wanjiru', email: 'admin@uhuruholdings.co.ke', phone: '+254 712 450 890', jobTitle: 'Tenant Administrator', role: 'Admin', branchAccess: BRANCHES.map((branch) => branch.id), permissionGroupIds: ['perm-ops', 'perm-admin'], active: true, lastLoginAt: nowIso() },
    { id: 'user-user-1', fullName: 'Kevin Otieno', email: 'tech@uhuruholdings.co.ke', phone: '+254 722 302 900', jobTitle: 'Field Supervisor', role: 'User', branchAccess: ['branch-nairobi'], permissionGroupIds: ['perm-dispatch', 'perm-reports'], active: true, lastLoginAt: nowIso() },
    { id: 'user-user-2', fullName: 'Asha Khamis', email: 'coast.ops@uhuruholdings.co.ke', phone: '+254 723 100 777', jobTitle: 'Branch Coordinator', role: 'User', branchAccess: ['branch-mombasa'], permissionGroupIds: ['perm-inventory', 'perm-clients'], active: true },
  ]
}

function baseAccounts(users: TenantUser[]): AuthAccount[] {
  return [
    { id: 'acct-superadmin', role: 'superadmin', name: 'Lena Atieno', email: 'superadmin@ecosys.io', password: 'Ecosys123!', title: 'Platform Owner', active: true },
    ...users.map<AuthAccount>((user) => ({
      id: `acct-${user.id}`,
      tenantId: DEFAULT_TENANT_ID,
      userId: user.id,
      role: user.role === 'Admin' ? 'admin' : 'user',
      name: user.fullName,
      email: user.email,
      password: 'Tenant123!',
      title: user.jobTitle,
      branchId: user.branchAccess[0],
      active: user.active,
    })),
  ]
}

function assetTemplates(rules: NumberingRule[]): AssetRecord[] {
  const rows = [
    ['Generator G-17', 'Generators', 'branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 'GEN-8172-KE', 'Needs Attention', 30, true, '2026-04-12T08:00:00.000Z', '2026-05-12T08:00:00.000Z'],
    ['UPS Bank UPS-22', 'UPS', 'branch-kisumu', 'client-lakefront', 'site-kisumu', 'UPS-2238-KS', 'Operational', 90, true, '2026-03-25T08:00:00.000Z', '2026-06-25T08:00:00.000Z'],
    ['CRAC Unit CRAC-18', 'CRAC Units', 'branch-mombasa', 'client-coastlink', 'site-miritini', 'CRAC-9921-MB', 'Under Maintenance', 14, true, '2026-04-03T08:00:00.000Z', '2026-05-01T08:00:00.000Z'],
    ['Rectifier RECT-31', 'Rectifiers', 'branch-mombasa', 'client-uhuru', 'site-coast', 'RECT-31-MB', 'Operational', 60, false, '2026-03-12T08:00:00.000Z', '2026-05-12T08:00:00.000Z'],
    ['PDU Rack PDU-44', 'PDUs', 'branch-nairobi', 'client-safaritel', 'site-nakuru', 'PDU-4420-NB', 'Operational', 30, true, '2026-04-19T08:00:00.000Z', '2026-05-19T08:00:00.000Z'],
    ['Cooling System CS-11', 'Cooling Systems', 'branch-kisumu', 'client-kifaru', 'site-eldoret', 'CS-1188-EL', 'Needs Attention', 21, false, '2026-04-05T08:00:00.000Z', '2026-04-30T08:00:00.000Z'],
    ['Fire Suppression Panel FS-04', 'Fire suppression systems', 'branch-mombasa', 'client-coastlink', 'site-miritini', 'FS-0449-MB', 'Operational', 180, true, '2026-02-01T08:00:00.000Z', '2026-07-31T08:00:00.000Z'],
    ['Generator G-21', 'Generators', 'branch-kisumu', 'client-kifaru', 'site-eldoret', 'GEN-2101-EL', 'Operational', 30, true, '2026-04-11T08:00:00.000Z', '2026-05-11T08:00:00.000Z'],
    ['UPS Edge UPS-09', 'UPS', 'branch-nairobi', 'client-safaritel', 'site-nakuru', 'UPS-0911-NK', 'Operational', 90, true, '2026-04-08T08:00:00.000Z', '2026-07-08T08:00:00.000Z'],
    ['CRAC Unit CRAC-07', 'CRAC Units', 'branch-mombasa', 'client-uhuru', 'site-coast', 'CRAC-0701-MB', 'Needs Attention', 14, false, '2026-04-10T08:00:00.000Z', '2026-04-28T08:00:00.000Z'],
    ['Rectifier RECT-14', 'Rectifiers', 'branch-kisumu', 'client-lakefront', 'site-kisumu', 'RECT-1450-KS', 'Operational', 60, true, '2026-03-20T08:00:00.000Z', '2026-05-19T08:00:00.000Z'],
    ['PDU Rack PDU-52', 'PDUs', 'branch-kisumu', 'client-lakefront', 'site-kisumu', 'PDU-5209-KS', 'Operational', 30, false, '2026-04-07T08:00:00.000Z', '2026-05-07T08:00:00.000Z'],
    ['Cooling System CS-19', 'Cooling Systems', 'branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 'CS-1972-NB', 'Operational', 21, true, '2026-04-14T08:00:00.000Z', '2026-05-05T08:00:00.000Z'],
    ['Fire Suppression Panel FS-09', 'Fire suppression systems', 'branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 'FS-0912-NB', 'Operational', 180, true, '2026-01-15T08:00:00.000Z', '2026-07-13T08:00:00.000Z'],
    ['Generator G-30', 'Generators', 'branch-mombasa', 'client-coastlink', 'site-miritini', 'GEN-3008-MB', 'Operational', 30, true, '2026-04-16T08:00:00.000Z', '2026-05-16T08:00:00.000Z'],
  ] as const

  return rows.map((row, index) => {
    const rule = rules.find((item) => item.branchId === row[2])
    return {
      id: createId('asset'),
      assetCode: `${rule?.assetPrefix ?? 'AST'}-${String((rule?.nextAssetNumber ?? 100) + index).padStart(6, '0')}`,
      name: row[0],
      category: row[1],
      branchId: row[2],
      clientId: row[3],
      siteId: row[4],
      serialNumber: row[5],
      status: row[6],
      pmScheduleDays: row[7],
      autoSchedulePm: row[8],
      lastServiceDate: row[9],
      nextServiceDate: row[10],
      maintenanceHistory: [],
      linkedWorkOrderIds: [],
    }
  })
}

function baseInventory(): InventoryItem[] {
  return [
    { id: createId('inv'), name: 'Cooling Fan Assembly', sku: 'CLG-FAN-118', branchId: 'branch-mombasa', store: 'Mombasa Store A1', quantity: 2, reorderLevel: 4, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'ATS Relay Pack', sku: 'ATS-RLY-404', branchId: 'branch-nairobi', store: 'Nairobi HQ Cage 4', quantity: 5, reorderLevel: 3, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Battery Harness Kit', sku: 'UPS-HAR-712', branchId: 'branch-kisumu', store: 'Kisumu PM Shelf', quantity: 1, reorderLevel: 2, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Breaker Cartridge', sku: 'PDU-BRK-019', branchId: 'branch-nairobi', store: 'Nairobi HQ Cage 2', quantity: 7, reorderLevel: 4, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Fire Suppressant Canister', sku: 'FSP-CAN-330', branchId: 'branch-nairobi', store: 'Nairobi Safety Bay', quantity: 6, reorderLevel: 2, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Compressor Oil Pack', sku: 'CMP-OIL-205', branchId: 'branch-mombasa', store: 'Mombasa Store B2', quantity: 4, reorderLevel: 3, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Rectifier Fuse Set', sku: 'RECT-FUSE-011', branchId: 'branch-mombasa', store: 'Mombasa DC Shelf', quantity: 8, reorderLevel: 4, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'Generator Filter Kit', sku: 'GEN-FIL-220', branchId: 'branch-nairobi', store: 'Nairobi Generator Cage', quantity: 3, reorderLevel: 3, unit: 'kits', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'UPS Battery Module 12V', sku: 'UPS-BAT-440', branchId: 'branch-kisumu', store: 'Kisumu PM Shelf', quantity: 2, reorderLevel: 4, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
    { id: createId('inv'), name: 'CRAC Sensor Pack', sku: 'CRAC-SENS-820', branchId: 'branch-kisumu', store: 'Kisumu HVAC Rack', quantity: 5, reorderLevel: 2, unit: 'pcs', linkedWorkOrderIds: [], lastUpdatedAt: nowIso() },
  ]
}

function baseWorkOrders(ruleset: NumberingRule[], assets: AssetRecord[], technicians: TechnicianRecord[], clients: ClientRecord[]): WorkOrderRecord[] {
  const templates = [
    ['branch-mombasa', 'client-coastlink', 'site-miritini', 2, 2, 'Critical', 'In Progress', 'Reactive Maintenance', 'CRAC high temperature alarm', 'Return air temperature rising in cold room zone B.', 8],
    ['branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 0, 0, 'High', 'Assigned', 'Reactive Maintenance', 'Generator ATS switchover failure', 'ATS did not complete switchover during weekly test.', 7],
    ['branch-kisumu', 'client-lakefront', 'site-kisumu', 1, 5, 'High', 'On Hold', 'Reactive Maintenance', 'UPS impedance alarm', 'Battery bank trend outside planned tolerance.', 10],
    ['branch-nairobi', 'client-safaritel', 'site-nakuru', 4, 1, 'Medium', 'Open', 'New Projects', 'PDU load balancing review', 'Repeated breaker trips during load balancing.', 11],
    ['branch-mombasa', 'client-uhuru', 'site-coast', 3, 3, 'Medium', 'Assigned', 'Reactive Maintenance', 'Rectifier communication loss', 'Telemetry feed dropped from DC plant alarm bus.', 9],
    ['branch-kisumu', 'client-lakefront', 'site-kisumu', 10, 5, 'Low', 'Completed', 'Preventive Maintenance', 'Quarterly rectifier PM', 'Scheduled PM checklist for rectifier bank.', 36],
    ['branch-kisumu', 'client-kifaru', 'site-eldoret', 7, 6, 'Critical', 'Open', 'Emergency Escalations', 'Generator fuel pressure drop', 'Fuel pressure dropped during outage test.', 4],
    ['branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 12, 7, 'Medium', 'In Progress', 'Preventive Maintenance', 'Cooling system PM follow-up', 'Manual PM visit generated from missed schedule.', 6],
    ['branch-mombasa', 'client-coastlink', 'site-miritini', 14, 2, 'High', 'Assigned', 'Preventive Maintenance', 'Monthly generator service', 'Routine generator service and panel inspection.', 20],
    ['branch-kisumu', 'client-kifaru', 'site-eldoret', 5, 4, 'Medium', 'Open', 'Reactive Maintenance', 'Cooling system low pressure', 'Cooling system pressure fluctuations affecting POP.', 5],
    ['branch-mombasa', 'client-uhuru', 'site-coast', 9, 3, 'High', 'On Hold', 'Reactive Maintenance', 'CRAC sensor drift', 'CRAC reporting unstable sensor data.', 12],
    ['branch-nairobi', 'client-safaritel', 'site-nakuru', 8, 1, 'Low', 'Completed', 'Preventive Maintenance', 'UPS quarterly battery test', 'Quarterly battery discharge test and firmware review.', 48],
    ['branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 13, 0, 'High', 'Assigned', 'Preventive Maintenance', 'Fire suppression audit', 'Suppression panel inspection and canister check.', 16],
    ['branch-kisumu', 'client-lakefront', 'site-kisumu', 11, 5, 'Medium', 'In Progress', 'New Projects', 'PDU rack expansion', 'Prepare power distribution for new teller floor equipment.', 14],
    ['branch-mombasa', 'client-coastlink', 'site-miritini', 6, 2, 'Low', 'Completed', 'Preventive Maintenance', 'Fire suppression semi-annual service', 'Semi-annual fire panel maintenance.', 72],
    ['branch-kisumu', 'client-kifaru', 'site-eldoret', 5, 6, 'Critical', 'Assigned', 'Emergency Escalations', 'Cooling outage at POP', 'POP room cooling degraded during high traffic.', 3],
    ['branch-kisumu', 'client-kifaru', 'site-eldoret', 7, 4, 'Medium', 'Open', 'New Projects', 'Generator enclosure upgrade assessment', 'Assess weatherproofing and attenuation works.', 24],
    ['branch-mombasa', 'client-uhuru', 'site-coast', 3, 3, 'Low', 'Completed', 'Preventive Maintenance', 'Rectifier PM visit', 'Routine rectifier PM with DC alarm validation.', 60],
    ['branch-nairobi', 'client-safaritel', 'site-nairobi-hq', 0, 0, 'Critical', 'In Progress', 'Reactive Maintenance', 'Generator controller alarm repeat', 'Controller alarm repeated within 24 hours.', 2],
    ['branch-kisumu', 'client-lakefront', 'site-kisumu', 1, 5, 'High', 'Assigned', 'Reactive Maintenance', 'UPS bypass stuck open', 'Static bypass failed to engage during inspection.', 1],
  ] as const

  const counters = Object.fromEntries(ruleset.map((rule) => [rule.branchId, rule.nextWorkOrderNumber]))

  return templates.map((row) => {
    const createdAt = addHoursIso(-row[10])
    const workOrderNumber = `${ruleset.find((rule) => rule.branchId === row[0])?.workOrderPrefix ?? 'WO'}-${String(counters[row[0] as string]++).padStart(6, '0')}`
    const technician = technicians[row[4]]
    const client = clients.find((item) => item.id === row[1]) ?? clients[0]
    const note = `Initial capture: ${row[9]}`
    return {
      id: createId('wo'),
      workOrderNumber,
      branchId: row[0],
      clientId: row[1],
      siteId: row[2],
      assetId: assets[row[3]]?.id,
      priority: row[5],
      status: row[6],
      type: row[7],
      title: row[8],
      description: row[9],
      reportedBy: client.contacts[0]?.name ?? 'Operations Desk',
      technicianId: row[6] === 'Open' ? undefined : technician.id,
      technicianGroupId: technician.groupId,
      slaRuleId: row[5] === 'Critical' ? 'sla-1' : row[5] === 'High' ? 'sla-2' : 'sla-3',
      slaStatus: 'On Track',
      responseDueAt: addHoursIso(1, createdAt),
      resolutionDueAt: addHoursIso(priorityToHours(row[5]), createdAt),
      arrivalTime: row[6] === 'In Progress' || row[6] === 'Completed' ? addHoursIso(1, createdAt) : undefined,
      departureTime: row[6] === 'Completed' ? addHoursIso(4, createdAt) : undefined,
      closureSummary: row[6] === 'Completed' ? 'Work completed and handed over to the client representative.' : '',
      clientAcknowledgement: row[6] === 'Completed' ? 'Acknowledged by site contact.' : 'Awaiting client acknowledgement.',
      createdAt,
      updatedAt: createdAt,
      completedAt: row[6] === 'Completed' ? addHoursIso(5, createdAt) : undefined,
      notes: [{ id: createId('note'), body: note, author: 'Operations Desk', createdAt }],
      activity: [
        activity('Operations Desk', 'Work order created', row[9], createdAt, 'created'),
        activity(technician.fullName, `Status: ${row[6]}`, `${row[8]} moved into ${String(row[6]).toLowerCase()} flow.`, addHoursIso(0.5, createdAt), row[6] === 'Completed' ? 'completion' : row[6] === 'Assigned' ? 'assignment' : 'status'),
      ],
    }
  })
}

function hydrateLinks(assets: AssetRecord[], technicians: TechnicianRecord[], workOrders: WorkOrderRecord[]) {
  const assetMap = new Map(assets.map((asset) => [asset.id, asset]))
  const techMap = new Map(technicians.map((item) => [item.id, item]))
  workOrders.forEach((workOrder) => {
    if (workOrder.assetId) {
      const asset = assetMap.get(workOrder.assetId)
      if (asset) {
        asset.linkedWorkOrderIds.push(workOrder.id)
        if (workOrder.completedAt) {
          asset.maintenanceHistory.unshift({ id: createId('hist'), workOrderId: workOrder.id, summary: workOrder.title, servicedAt: workOrder.completedAt })
        }
      }
    }
    if (workOrder.technicianId && !['Completed', 'Cancelled'].includes(workOrder.status)) {
      techMap.get(workOrder.technicianId)?.activeWorkOrderIds.push(workOrder.id)
    }
  })
}

export function createBlankTenantData(companyName: string, tenantCode: string): TenantData {
  const branch = { id: createId('branch'), name: 'Main Branch', code: 'MAIN', region: 'Kenya', city: 'Nairobi', active: true, siteCount: 1 }
  return {
    settings: {
      companyProfile: { companyName, legalName: companyName, supportEmail: `support@${tenantCode.toLowerCase()}.co.ke`, supportPhone: '+254 700 000 000', country: 'Kenya', timezone: 'Africa/Nairobi', address: 'Nairobi, Kenya' },
      branding: { logoUrl: '', primaryColor: '#0ea5e9', accentColor: '#10b981' },
      emailIntegration: { status: 'Not Configured', senderName: companyName, senderEmail: `service@${tenantCode.toLowerCase()}.co.ke`, smtpHost: 'smtp.example.com', lastCheckedAt: nowIso() },
      security: { sessionTimeoutHours: 12, maxConcurrentSessions: 5, mfaRequired: false, passwordRotationDays: 90 },
      inventory: { defaultStore: 'Main Store', approvalRequired: true, stockAdjustmentRequiresReason: true },
      notifications: { criticalAlerts: true, dailyDigest: true, emailAcknowledgements: true },
      workOrderRules: {
        allowMissingClientWithReason: false,
        allowMissingAssetWithReason: true,
        requireAssignmentGroupBeforeDispatch: true,
        requireClientAcknowledgementBeforeClosure: true,
      },
      permissionGroups: [
        { id: 'perm-ops', name: 'Operations Control', permissions: ['work-orders.manage', 'assets.view', 'clients.view'] },
        { id: 'perm-admin', name: 'Settings Admin', permissions: ['settings.manage', 'users.manage'] },
        { id: 'perm-dispatch', name: 'Dispatch', permissions: ['work-orders.assign', 'field-operations.view'] },
        { id: 'perm-reports', name: 'Reports View', permissions: ['reports.view'] },
        { id: 'perm-inventory', name: 'Inventory', permissions: ['inventory.manage', 'requisitions.issue'] },
        { id: 'perm-clients', name: 'Clients', permissions: ['clients.manage'] },
      ],
      rolePermissions: [{ id: 'role-admin', role: 'Admin', permissions: ['*'] }, { id: 'role-user', role: 'User', permissions: ['work-orders.manage', 'assets.view', 'clients.view', 'reports.view'] }],
      numberingRules: rules([branch]).map((rule) => ({ ...rule, workOrderPrefix: 'WO-MAIN', assetPrefix: 'AST-MAIN', requisitionPrefix: 'REQ-MAIN' })),
      technicianGroups: [{ id: 'group-default', name: 'General Response', branchIds: [branch.id], supervisor: 'Unassigned' }],
      assetTemplates: [{ id: 'tmpl-gen', name: 'Generator Template', category: 'Generators', autoSchedulePm: true, pmFrequencyDays: 30, checklistSummary: 'Generator PM checklist' }],
      pmRules: [{ id: 'pm-default', name: 'Default Monthly PM', assetCategory: 'Generators', frequencyDays: 30, autoSchedule: true, enabled: true }],
    },
    branches: [branch],
    users: [],
    technicians: [],
    clients: [],
    assets: [],
    inventoryItems: [],
    workOrders: [],
    requisitions: [],
    slaRules: [],
    auditLog: [],
  }
}

export function buildSessionName(name: string) {
  return { avatar: createAvatar(name) }
}

export function createSeedDatabase(): AppDatabase {
  const tenantRules = rules(BRANCHES)
  const tenantUsers = baseUsers()
  const tenantAccounts = baseAccounts(tenantUsers)
  const slaRules = baseSlaRules()
  const clients = baseClients(slaRules)
  const technicians = baseTechnicians()
  const assets = assetTemplates(tenantRules)
  const inventoryItems = baseInventory()
  const workOrders = baseWorkOrders(tenantRules, assets, technicians, clients)
  hydrateLinks(assets, technicians, workOrders)
  const requisitions = [
    { id: createId('req'), requisitionNumber: 'REQ-MBA-000010', workOrderId: workOrders[0].id, inventoryItemId: inventoryItems[0].id, itemName: inventoryItems[0].name, branchId: 'branch-mombasa', store: inventoryItems[0].store, quantityRequested: 2, quantityIssued: 2, urgency: 'Critical', remarks: 'Replace failing condenser fan assembly.', status: 'Issued', requestedBy: workOrders[0].reportedBy, requestedAt: addHoursIso(1, workOrders[0].createdAt), approvedAt: addHoursIso(1.2, workOrders[0].createdAt), issuedAt: addHoursIso(1.5, workOrders[0].createdAt) },
    { id: createId('req'), requisitionNumber: 'REQ-NRB-000010', workOrderId: workOrders[1].id, inventoryItemId: inventoryItems[1].id, itemName: inventoryItems[1].name, branchId: 'branch-nairobi', store: inventoryItems[1].store, quantityRequested: 1, quantityIssued: 0, urgency: 'Priority', remarks: 'Hold ATS relay pack for diagnostic swap.', status: 'Pending', requestedBy: workOrders[1].reportedBy, requestedAt: addHoursIso(1, workOrders[1].createdAt) },
    { id: createId('req'), requisitionNumber: 'REQ-KSM-000010', workOrderId: workOrders[2].id, inventoryItemId: inventoryItems[2].id, itemName: inventoryItems[2].name, branchId: 'branch-kisumu', store: inventoryItems[2].store, quantityRequested: 1, quantityIssued: 0, urgency: 'Priority', remarks: 'Replace damaged battery harness after test.', status: 'Approved', requestedBy: workOrders[2].reportedBy, requestedAt: addHoursIso(1, workOrders[2].createdAt), approvedAt: addHoursIso(1.3, workOrders[2].createdAt) },
  ] as const
  inventoryItems[0].quantity -= 2
  inventoryItems[0].linkedWorkOrderIds.push(workOrders[0].id)
  inventoryItems[1].linkedWorkOrderIds.push(workOrders[1].id)
  inventoryItems[2].linkedWorkOrderIds.push(workOrders[2].id)
  workOrders[0].activity.unshift(activity('Inventory Desk', 'Material issued', `${inventoryItems[0].name} x2 issued.`, addHoursIso(1.5, workOrders[0].createdAt), 'material'))
  workOrders[1].activity.unshift(activity('Operations Desk', 'Material requested', `${inventoryItems[1].name} x1 requested.`, addHoursIso(1, workOrders[1].createdAt), 'material'))
  workOrders[2].activity.unshift(activity('Operations Desk', 'Material approved', `${inventoryItems[2].name} request approved.`, addHoursIso(1.3, workOrders[2].createdAt), 'material'))

  const tenantData: TenantData = {
    settings: {
      companyProfile: { companyName: 'Uhuru Facilities', legalName: 'Uhuru Facilities Limited', supportEmail: 'service@uhurufacilities.co.ke', supportPhone: '+254 712 450 890', country: 'Kenya', timezone: 'Africa/Nairobi', address: 'Upper Hill, Nairobi, Kenya' },
      branding: { logoUrl: '', primaryColor: '#0ea5e9', accentColor: '#10b981' },
      emailIntegration: { status: 'Attention', senderName: 'Uhuru Facilities', senderEmail: 'clientcare@uhurufacilities.co.ke', smtpHost: 'smtp.office365.com', lastCheckedAt: nowIso() },
      security: { sessionTimeoutHours: 12, maxConcurrentSessions: 10, mfaRequired: false, passwordRotationDays: 90 },
      inventory: { defaultStore: 'Nairobi HQ Cage 4', approvalRequired: true, stockAdjustmentRequiresReason: true },
      notifications: { criticalAlerts: true, dailyDigest: true, emailAcknowledgements: true },
      workOrderRules: {
        allowMissingClientWithReason: true,
        allowMissingAssetWithReason: true,
        requireAssignmentGroupBeforeDispatch: true,
        requireClientAcknowledgementBeforeClosure: true,
      },
      permissionGroups: [
        { id: 'perm-ops', name: 'Operations Control', permissions: ['dashboard.view', 'work-orders.manage', 'assets.manage', 'field-operations.view'] },
        { id: 'perm-admin', name: 'Settings Admin', permissions: ['settings.manage', 'users.manage', 'numbering.manage'] },
        { id: 'perm-dispatch', name: 'Dispatch', permissions: ['work-orders.assign', 'work-orders.status', 'clients.view'] },
        { id: 'perm-reports', name: 'Reports View', permissions: ['reports.view'] },
        { id: 'perm-inventory', name: 'Inventory', permissions: ['inventory.manage', 'requisitions.issue'] },
        { id: 'perm-clients', name: 'Clients', permissions: ['clients.manage'] },
      ],
      rolePermissions: [{ id: 'role-admin', role: 'Admin', permissions: ['*'] }, { id: 'role-user', role: 'User', permissions: ['dashboard.view', 'work-orders.manage', 'assets.view', 'inventory.view', 'reports.view'] }],
      numberingRules: tenantRules,
      technicianGroups: [
        { id: 'group-metro', name: 'Metro Electrical', branchIds: ['branch-nairobi'], supervisor: 'Mercy Wanjiru' },
        { id: 'group-coast', name: 'Coast Response', branchIds: ['branch-mombasa'], supervisor: 'Asha Khamis' },
        { id: 'group-western', name: 'Western PM', branchIds: ['branch-kisumu'], supervisor: 'David Otieno' },
      ],
      assetTemplates: [
        { id: 'tmpl-gen', name: 'Generator Template', category: 'Generators', autoSchedulePm: true, pmFrequencyDays: 30, checklistSummary: 'Monthly generator PM and load test' },
        { id: 'tmpl-ups', name: 'UPS Template', category: 'UPS', autoSchedulePm: true, pmFrequencyDays: 90, checklistSummary: 'Quarterly UPS battery review' },
        { id: 'tmpl-crac', name: 'CRAC Template', category: 'CRAC Units', autoSchedulePm: false, pmFrequencyDays: 14, checklistSummary: 'Cooling inspection' },
      ],
      pmRules: [
        { id: 'pm-gen', name: 'Monthly generator PM', assetCategory: 'Generators', frequencyDays: 30, autoSchedule: true, enabled: true },
        { id: 'pm-ups', name: 'Quarterly UPS service', assetCategory: 'UPS', frequencyDays: 90, autoSchedule: true, enabled: true },
        { id: 'pm-cool', name: 'Bi-weekly cooling inspections', assetCategory: 'Cooling Systems', frequencyDays: 14, autoSchedule: false, enabled: true },
      ],
    },
    branches: BRANCHES,
    users: tenantUsers,
    technicians,
    clients,
    assets,
    inventoryItems,
    workOrders,
    requisitions: [...requisitions],
    slaRules,
    auditLog: [{ id: createId('audit'), tenantId: DEFAULT_TENANT_ID, actor: 'System', action: 'seed', entityType: 'tenant', entityId: DEFAULT_TENANT_ID, detail: 'Tenant demo data initialized.', createdAt: nowIso() }],
  }

  const defaultTenant: TenantRecord = {
    id: DEFAULT_TENANT_ID,
    name: 'Uhuru Facilities',
    code: DEFAULT_TENANT_CODE,
    region: 'Kenya',
    plan: 'Enterprise',
    status: 'Active',
    subscriptionStatus: 'Active',
    activeSessionCount: 0,
    userCount: tenantUsers.length,
    featureFlags: PLATFORM_FLAGS,
    createdAt: addDaysIso(-120),
  }

  return {
    version: 1,
    initializedAt: nowIso(),
    tenants: [
      defaultTenant,
      { id: 'tenant-savanna', name: 'Savanna Energy Services', code: 'SES-EA', region: 'East Africa', plan: 'Growth', status: 'Active', subscriptionStatus: 'Trial', activeSessionCount: 0, userCount: 21, featureFlags: PLATFORM_FLAGS.slice(0, 2), createdAt: addDaysIso(-40) },
      { id: 'tenant-harbor', name: 'Harbor Cold Chain', code: 'HCC-CST', region: 'Coast', plan: 'Enterprise', status: 'Suspended', subscriptionStatus: 'Past Due', activeSessionCount: 0, userCount: 16, featureFlags: PLATFORM_FLAGS, createdAt: addDaysIso(-180) },
    ],
    tenantData: {
      [DEFAULT_TENANT_ID]: tenantData,
      'tenant-savanna': createBlankTenantData('Savanna Energy Services', 'SES-EA'),
      'tenant-harbor': createBlankTenantData('Harbor Cold Chain', 'HCC-CST'),
    },
    authAccounts: tenantAccounts,
    sessions: [],
    platformFeatureFlags: PLATFORM_FLAGS,
    systemHealth: [
      { id: 'sys-1', service: 'Auth Gateway', status: 'Healthy', latency: '122 ms' },
      { id: 'sys-2', service: 'Notifications Stream', status: 'Degraded', latency: '420 ms' },
      { id: 'sys-3', service: 'Audit Pipeline', status: 'Investigating', latency: '1.2 s' },
      { id: 'sys-4', service: 'Tenant Provisioning', status: 'Healthy', latency: '188 ms' },
    ],
    platformAuditLog: [
      { id: createId('paudit'), actor: 'Platform Bot', action: 'seed', detail: 'Platform demo data initialized.', createdAt: nowIso() },
      { id: createId('paudit'), actor: 'Lena Atieno', action: 'review', detail: 'Suspended Harbor Cold Chain pending subscription review.', createdAt: addHoursIso(-12) },
      { id: createId('paudit'), actor: 'Platform Bot', action: 'flag', detail: 'Auto PM Scheduler rolled out to pilot audience.', createdAt: addHoursIso(-20) },
    ],
  }
}
