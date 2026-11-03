# Ecosys Factory Maintenance — Requirements

Nov 2, 2026 · @Dennis

## 1. Vision and scope

Ecosys Factory Maintenance is an enterprise-grade CMMS that launches with tea factories and then expands to other asset-intensive industries. It moves factories from reactive, paper-based maintenance to planned, traceable and data-driven maintenance.

**Guiding principle:** every asset is identifiable, every maintenance activity is traceable, every resource is accountable, and every decision is supported by data.

**Core objectives**

- Central register of all equipment, with QR/barcode tags readable by phone
- Preventive, corrective and (later) predictive maintenance in one place
- Full work order lifecycle from fault report to closure and approval
- Technician, spares, contractor and cost management
- Management dashboards that answer: what we own, where it is, its condition, what is due, what keeps failing, who is responsible, and what it costs

**First market: tea factories.** They are machine-heavy, seasonal, often in low-connectivity areas and frequently organised in groups, so one win can open many sites. The product is designed for tea first but must not hard-code tea: later markets include sugar, dairy, cement, flower farms, data centres and hospitals (biomedical equipment, cross-sold with ECOSYS HEALTH).

**Design principles**

- A complete standalone product first; ERP integration built into the architecture but delivered later
- Enterprise scale from day one: multiple groups, factories, sections and thousands of assets
- Mobile-first and offline-capable for technicians on the factory floor
- Configurable workflows, approvals and forms rather than one rigid process
- A complete audit trail of every maintenance activity

## 2. Organisation and asset hierarchy

One configurable hierarchy serves a single factory and a multi-factory group alike; levels can be hidden when a customer doesn't need them.

> _[Embedded diagram not included: organisation and asset hierarchy · 6 levels, 8 tea sections]_

The QR tag lives at asset level, while history can be recorded down to component level. Sections ship as a tea default and are fully editable, so other industries can use their own (e.g. production lines).

## 3. Functional requirements

The platform has twelve interconnected modules plus safety, organised around three maintenance rhythms that tea factories actually run on.

### 3.1 Three maintenance rhythms

| Rhythm | When | What happens |
| --- | --- | --- |
| Running checks | Daily / per shift, line running | Operator and artisan inspections, readings, defect logging |
| Weekly window | Monday, no leaf intake (configurable) | Planned jobs that need the line stopped; routine weekly tasks |
| Annual overhaul | Low-crop season shutdown | Major repairs, roller re-cutting, boiler work, deferred jobs |

### 3.2 Asset management

- Asset register with specifications, location, manufacturer, model, serial, install date and documents (manuals, drawings, versioned)
- Parent–child components (e.g. motor within a fan, segments within a CTC machine), so history follows the component
- Criticality rating (A/B/C) driving priority, spares stocking and PM frequency
- Lifecycle events: commissioning, transfer between sections or factories, decommissioning, disposal
- Warranty tracking with alerts before expiry
- Asset classes with pre-built templates for tea machinery: withering troughs and fans, CTC machines, rotorvanes, continuous fermenting units, fluid bed dryers, sorting and grading machines, packing, boilers, generators, weighbridges, compressors, transformers, small hydro plants
- Vehicles as an asset class with odometer meters (leaf collection fleet as a later add-on)
- Bulk import from Excel templates and a mobile asset survey mode for onboarding

### 3.3 Tags and identification

- Unique asset number for every asset; QR or barcode labels, NFC as an option for harsh areas
- Label printing in batches; reprint, replace damaged tags, prevent duplicates
- Scan opens the asset: details, location, history, open work orders, report a fault, start a task, checklists, readings, parts used, photos, submit for verification

### 3.4 Work orders

- Types: corrective, preventive, inspection, statutory, project/modification, safety
- Lifecycle: requested → approved → planned → assigned → in progress → completed → verified → closed
- Hold statuses: waiting for parts, waiting for window, waiting for shutdown, waiting for contractor
- Operator request portal: scan a QR code and report a fault without a full login
- Priority × criticality SLA matrix for response and repair times
- Job plan templates: tasks, tools, parts, estimated hours
- Structured failure codes (problem, cause, remedy), aligned with ISO 14224
- Rework tracking: same fault within a set number of days links to and flags the previous job
- Split into child jobs, reassign, before/after photos, shift handover notes, e-signature on completion

### 3.5 Preventive maintenance

- Triggers: calendar, run hours, kg of made tea processed, other meters, manufacturer recommendations
- Fixed vs floating schedules (due by date, or X days after last completion)
- Compliance window (e.g. ±10%), defer/skip with mandatory reason
- "Defer until low crop" option, with risk flagged on critical assets
- Workload forecasting and levelling across weeks
- Route-based PM (lubrication and inspection rounds)

### 3.6 Weekly maintenance window (Monday)

- Maintenance windows as a per-factory setting; Monday default, movable or skippable for holidays or exceptional peak weeks
- "Needs line stop" flag: such jobs automatically queue for the next window
- Operators log defects during the week as "fix in window"
- Window job pack generated ahead of time (e.g. Saturday): jobs, technicians, reserved and kitted spares, permits, contractors
- Capacity check: planned hours vs available technician hours
- Standard weekly routine templates: lubrication, belt and chain tension, CTC segment inspection, dryer and fluid bed cleaning, fan checks, panel inspections, boiler checks
- Handback to production: start-up checklist and sign-off before Tuesday leaf intake
- Carryover: unfinished jobs roll to the next window; repeated carryovers escalate or move to the overhaul list

### 3.7 Annual overhaul planning

- Overhaul job list built throughout the season from deferred jobs, carryovers and condition findings
- Scope, budget, spares and contractor planning ahead of shutdown
- Progress tracking during shutdown and recommissioning checklists

### 3.8 Technicians and teams

- Teams, trades and skills (fitters, electricians, boiler operators, instrument technicians)
- Shifts, rosters, on-call and leave, so assignment respects availability
- Certification expiry tracking (electrical licences, boiler operator, welding, working at heights)
- Labour rates including overtime for accurate costing
- Workload view and productivity tracking

### 3.9 Spares and inventory

- Multiple stores and bin locations; stock levels, minimums and reorder points
- Reservation and kitting against planned jobs and the weekly window
- Parts lists per asset (where-used) and critical spares flags
- Serialised and repairable spares (e.g. CTC roller segments out for re-cutting and back)
- Issues, returns, transfers, cycle counts and stock valuation
- Units of measure; multi-currency pricing for imported spares
- Lightweight purchase requisitions and vendor catalogue, before any ERP link
- Shared spares visibility across factories in a group

### 3.10 Inspections and checklists

- Configurable checklist and form builder (pass/fail, readings, photos, signatures)
- Failed checklist items can raise a work order automatically

### 3.11 Condition monitoring (foundation)

- Meter and reading history per asset (hours, kg processed, temperature, vibration, pressure)
- Thresholds that raise alerts or work orders
- Data model ready for sensors and IoT in a later phase

### 3.12 Contractors

- Contractor register with compliance documents (insurance, licences) and expiry alerts
- Annual maintenance contracts (AMCs): scope, SLAs, visit schedules, renewal alerts
- Contractor job assignment, optional contractor portal
- Contractor invoices matched against completed work orders

### 3.13 Costs and budgets

- Labour, parts, contractor and downtime cost per work order, asset, section and factory
- Downtime costed in kg of made tea lost (lost-production rate per asset)
- Annual budget per factory and cost centre, budget vs actual
- Multi-currency (KES, USD) and VAT handling

### 3.14 Safety

- Permit-to-work for high-risk jobs (hot work, confined space, electrical, working at heights)
- Lockout/tagout (LOTO) records, especially during the weekly window and overhaul
- Safety checks on start-up before handback to production

## 4. Compliance and regulatory

Compliance tracking is a core differentiator against generic CMMS tools, so statutory items are built in rather than left to custom checklists.

| Area | What the system tracks |
| --- | --- |
| Statutory inspections (OSHA 2007, DOSH) | Boilers, pressure vessels, lifting equipment: certified examiner reports, certificates, expiry alerts |
| Boiler operation | Operator certification, inspection records, blowdown and water treatment logs |
| Calibration (ISO 9001) | Instruments, weighbridges, scales, metal detectors: calibration due dates and certificates |
| Food safety (ISO 22000 / FSSC 22000, HACCP) | Food-grade lubricant records, cleaning logs, glass and hard plastic register, metal detector checks |
| Certification audits | Rainforest Alliance, Fairtrade: maintenance and safety records on demand |
| Fire safety | Extinguishers, hydrants, alarms: inspection schedules and records |
| Energy (ISO 50001) | Energy use per asset and per kg of made tea |
| Management systems | Audit trail supporting ISO 9001, 14001, 45001 and 55001 |
| Data protection | Kenya Data Protection Act 2019: personal data handling, consent, data residency options |

Every compliance item has an owner, a due date, evidence attachments and escalation when overdue.

### 4.1 Rainforest Alliance

Tea factories that process certified leaf are audited under the Rainforest Alliance Supply Chain Standard, with annual surveillance audits and full recertification every three years ([SCS Global Services](https://www.scsglobalservices.com/index%2ephp/africa/services/rainforest-alliance-certification)). The standard's themes include efficient water and energy use, waste management, and safe and healthy working conditions ([SCS Global Services](https://www.scsglobalservices.com/index%2Ephp/europe/services/rainforest-alliance-certification)). The system should produce the maintenance-side evidence for these on demand.

| Audit area | What the system records |
| --- | --- |
| Safe working conditions | Machine guarding inspections, PPE issue to maintenance staff, first aid and fire equipment checks, emergency drills, permits and LOTO |
| Incidents | Accident and near-miss register linked to the asset and the corrective work order |
| Training | Safety induction and skills training records for technicians and contractors |
| Energy and water | Consumption per asset and per kg of made tea; boiler fuel use; improvement actions |
| Waste | Disposal records for used oil, scrap metal, batteries and e-waste, with the licensed contractor |
| Chemicals | Register of lubricants and boiler water treatment chemicals, safety data sheets, storage inspections |
| Non-conformities | Audit findings logged as corrective actions with owner, due date and evidence of closure |
| Audit calendar | Self-assessment, surveillance audit and recertification dates with reminders |

**Design approach:** build a generic *compliance framework* feature rather than an RA-specific screen. Each standard (Rainforest Alliance, Fairtrade, ISO 22000, OSHA statutory) is a configurable list of requirements, each mapped to the checklists, records and documents that prove it. One click then produces an audit evidence pack per factory for a chosen period. Delivered across Phases 3, 4 and 6 of the build plan.

To confirm with the pilot factory: which RA requirements their last audit actually checked on the maintenance and engineering side.

## 5. Mobile and offline

Offline operation is non-negotiable: tea factories sit in rural highland areas with patchy signal, so technicians must work fully without a connection and sync later.

- Works offline: scan tags, view asset history and open jobs, complete checklists, record readings, parts and hours, take photos
- Background sync with conflict handling when two people edit the same job
- Assigned jobs and the weekly window job pack downloaded in advance
- Camera-based QR/barcode scanning on ordinary Android phones; installable as an app (PWA or native)
- Large touch targets, minimal typing, usable with gloves and in poor lighting
- Low data usage: compressed photos, delta sync
- Shared-device mode for artisans without personal smartphones
- Notifications by SMS and WhatsApp as well as in-app and email
- Language support: English first, Swahili as an option

## 6. Dashboards and KPIs

Dashboards exist at three levels — group, factory and section — so a group engineer can compare factories and a factory engineer can run the week.

| Category | KPIs |
| --- | --- |
| Assets | Total assets, operational vs non-operational, assets by criticality |
| Reliability | MTBF, MTTR, breakdown frequency, top failing assets, repeat failures |
| Planning | PM compliance, overdue jobs, backlog (in labour hours and weeks), planned vs reactive ratio |
| Weekly window | Window plan completion %, jobs carried over and their age, breakdowns on assets with deferred window jobs |
| Overhaul | Overhaul scope completed, cost vs budget, recommissioning on time |
| Downtime | Hours lost, kg of made tea lost, peak vs low-season downtime |
| Costs | Maintenance cost per kg of made tea, cost per asset and section, budget vs actual |
| Spares | Consumption, stock-outs, critical spares below minimum, stock value |
| People | Technician productivity, utilisation, overtime, expiring certifications |
| Energy | kWh per kg of made tea, firewood per kg of made tea (boiler efficiency) |
| Compliance | Statutory and calibration items due and overdue |

Reports export to Excel and PDF, and can be scheduled by email to factory managers and boards.

## 7. Platform, architecture and integrations

Launch as cloud SaaS first, with on-premises as an enterprise tier built from the same codebase, because supporting both from day one roughly doubles release and support effort.

**Platform**

- Multi-tenant SaaS with strict tenant isolation; on-premises deployment from the same release
- Configurable roles and permissions down to factory and section level
- Configurable approval workflows, custom fields and form builder
- Complete audit trail on every record change
- SSO / Active Directory for enterprise customers
- Notifications: in-app, email, SMS and WhatsApp
- Multi-currency, VAT, time zones and multiple languages
- Backups, disaster recovery and data residency options
- On-premises licensing, update delivery and remote support model

**Integration readiness (built in, delivered in Phase 8)**

- API-first design: everything the UI does is available through a documented API
- Webhooks for events (work order closed, stock below minimum, asset down)
- Stable external IDs for assets, parts, cost centres and vendors to map to ERP records
- Future targets: SAP, Oracle, Microsoft Dynamics 365, existing inventory and procurement systems, SCADA/PLC and IoT platforms, energy monitoring
- Customers keep their ERP; Ecosys exchanges assets, maintenance, inventory, procurement and cost data with it

## 8. Commercial model and go-to-market

Win one pilot tea factory, prove the results, then sell to factory groups where one decision covers many sites.

**Positioning:** against MaintainX, UpKeep, Limble, Fiix and SAP PM, Ecosys is offline-first, SMS-enabled, ships with tea machinery templates and local compliance, and is priced and supported locally.

**Revenue streams**

- Subscription: pricing model to decide (per asset, per technician, or per factory)
- Implementation services: asset survey, data collection, tag printing and installation — often more profitable than the licence
- Training and support packages
- On-premises licences for enterprise customers
- A free or low-cost tier for small workshops (to decide)

**Go-to-market sequence**

1. Pilot one tea factory: build Phase 1 around its real asset list, Monday window and overhaul cycle
2. Publish pilot results (downtime, PM compliance, cost per kg of made tea)
3. Approach factory groups and management agencies for multi-factory rollouts
4. Expand to adjacent sectors: sugar, dairy, cement, flower farms, data centres
5. Cross-sell biomedical equipment maintenance to hospitals alongside ECOSYS HEALTH

## 9. Phased roadmap

Phases 1–6 are the sellable core, re-ordered for tea: the weekly window, overhaul list and statutory reminders move up into Phase 3, and safety joins technicians in Phase 4.

> _[Embedded diagram not included: phased roadmap · 6 core phases, 2 expansion phases]_

Offline mobile, meters (including kg of made tea) and integration-ready IDs are built into Phase 1 because adding them later means reworking the data model.

Detailed build sequence, durations and ServiceOps reuse: Build plan

## 10. Open questions and decisions

- [x] Which tea factory will be the pilot, and who is the champion there?
- [x] Smallholder factory group or private estate as the first target segment?
- [ ] Pricing model: per asset, per technician or per factory? Free tier or not?
- [ ] Is the Monday window a full day at every factory, and is it ever cancelled during peak flush?
- [ ] Which assets does the pilot factory consider critical (A-class)?
- [ ] Native Android app or installable web app (PWA) for offline mobile?
- [ ] Tech stack and hosting location (Kenya vs international cloud) for data residency
- [ ] Is leaf collection fleet maintenance in scope for the first release?
- [ ] Which statutory inspections does the pilot factory currently track, and how?
- [ ] Swahili interface in the first release or later?
