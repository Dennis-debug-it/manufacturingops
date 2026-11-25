# Wood fuel management — design

Nov 24, 2026 · Draft for review

How Ecosys tracks the **firewood a tea factory burns**: from the supplier's truck at the gate, through
the weighbridge and moisture test, into the wood yard, out to the boilers and dryers, and into the one
number the factory manager asks about: **kg of wood per kg of made tea, and what it cost**.

It is a new add-on module, `woodfuel`, sold like Visitors and Security. It reuses the platform, the gate
log, meters, stores, KPIs and the offline outbox. Nothing here changes the Maintenance module.

Items marked **(confirm)** are numbers or practices to check with a pilot factory before they become
defaults.

Related: [operations-platform-spec.md](operations-platform-spec.md) (how add-ons are built),
[grain-milling-feeds-spec.md](grain-milling-feeds-spec.md) (the same weighbridge-intake pattern),
[industry-packs.md](industry-packs.md).

---

## 1. Why this is not just "a stock item"

A tea factory dries made tea with heat, and most Kenyan factories make that heat by burning wood. It is
usually the largest cost after green leaf and labour, and the easiest to lose track of.

The obvious shortcut is to add "Firewood" as a material in the Fuel store: receive it on a purchase
order, issue it to the boiler. It works for a headcount of tonnes and no more. It falls short here:

| Need | A stock item can't |
|---|---|
| Pay for what the wood is worth | Wet wood burns badly. Payment depends on **moisture**, which differs on every load, so the price is worked out per delivery |
| Many small sellers | Wood comes from dozens of farmers and merchants, many paid by M-Pesa, with no purchase order |
| Weigh or measure | Some factories weigh on a weighbridge; many buy by **stacked cubic metre** or by the lorry or tractor load |
| Know where it is | Wood sits in **stacks** that season (dry) for weeks. Which stack is ready, how old, how wet |
| Know how long it lasts | Days of cover at the recent burn rate, not a reorder level |
| Catch loss | Received minus burnt minus counted. A gap is shrinkage or theft |
| Link to output | Wood per kg made tea, by boiler and by week |
| Work in the yard | A clerk with a phone, no signal, wet hands |
| Prove sourcing | Tea certification and some buyers ask where fuelwood comes from, with permits |

So wood fuel gets its own small set of records. It still **uses** the existing platform wherever it can.

---

## 2. How the wood moves

```
 supplier ──► gate ──► weighbridge ──► moisture test ──► decision ──► stack in the yard
 (farmer,     vehicle    gross / tare     3 samples,        accept, reject,   (seasoning,
  merchant,   log        or stacked m³    average           or accept with    ready)
  own woodlot)           or load count                      a deduction
                                                                                 │
   money: amount payable per delivery ──► supplier statement ──► paid            ▼
                                                                  issue to boiler / dryer (per shift)
                                                                                 │
                                                                                 ▼
                                          kg wood per kg made tea · cost · days of cover · loss
```

---

## 3. What is reused

| Need | Existing piece | How it is used |
|---|---|---|
| Factory, yard, boilers | `OrgNode`, `Asset` (class `TEA-BLR`) | The wood yard is a section (the tea pack already has *Boiler house* and a *Fuel store* zone). A boiler or dryer is an asset |
| Trucks at the gate | `VehicleMovement`, `VehiclePurposes` (`Collection` already says "firewood suppliers") | A delivery links to the gate entry. The gate won't let a loaded truck out until its second weighing is in |
| Fuel counters | `Meter` of type `Fuel`, `IMeterReadingService` | Each issue to a boiler writes a reading, so boiler PM and the existing `FuelPerUnit` KPI keep working |
| Suppliers' papers | `Contractor` / `ContractorDocument` pattern | Transport permits and licences with expiry alerts |
| KPIs | `EnergyKpis`, `FactoryKpiService` | Gains wood per unit and wood cost per unit, read from the ledger rather than meters |
| Production unit | `OrgNode.ProductionUnit` ("kg made tea") | The divisor for every wood KPI |
| Counts | `StoreCount` pattern | Stack counts, with variance and an approval for large write-offs |
| Offline | Outbox with idempotency keys and `X-Occurred-At` | Clerk's receiving and issuing work with no signal |
| Alerts | Notifications, email, push, WhatsApp | Low cover, rejected load, wet wood, unexplained loss |
| Evidence | `EvidenceSources`, audit log, attachments | Delivery note and permit photos; a sourcing report for auditors |
| Data protection | Visitor rules (masked ID, retention) | Suppliers' ID numbers are stored masked |

---

## 4. Entities

All carry `TenantId` and the factory (`FactoryId`), and follow the existing `AuditableEntity` conventions.

```
WoodFuelSettings     One per factory. How this factory buys and burns wood.
                       measure basis default (Weighbridge | StackedVolume | LoadCount)
                       moisture base % (the wetness the price assumes), reject-above %, deduction per point over
                       burn-rate window (days) and low-cover alert (days)
                       target kg of wood per production unit; seasoning days before a stack is "ready"
                       the weighbridge asset, if there is one

FuelSpecies          Eucalyptus, cypress, pine, grevillea, black wattle, mixed, and "other biomass"
                     (briquettes, macadamia or coffee husks, sawdust). Kg per stacked m³ at the base
                     moisture, and an optional energy value (MJ/kg) (confirm both with the pilot).

FuelSupplier         Name, type (Farmer, Merchant, Cooperative, Own woodlot), phone, M-Pesa or bank details,
                     KRA PIN, national ID (masked last 4 + salted hash), source area, transport permit
                     number and expiry, status. Optionally linked to an existing Supplier.

FuelPriceAgreement   Supplier (or all), species, price per tonne or per stacked m³, valid from/to, and
                     optional moisture terms that override the factory's.

WoodDelivery         WF-2026-0001. Factory, supplier, vehicle (gate entry), driver, species and form (logs,
                     splits, slabs, offcuts, briquettes), source area, transport permit, supplier's delivery
                     note number, measure basis.
                       weighed:   gross, tare, net kg
                       stacked:   length, width, height, stacking factor, m³, estimated kg
                       load:      count and size of loads, estimated kg
                       moisture samples (WoodMoistureSample rows: value %, meter, who, when) and the average
                       decision: Accepted | Rejected | AcceptedWithDeduction, reason, kg rejected
                       payable kg, price applied, moisture deduction, amount payable
                       stack it went into, received by, photos, status
                       (Received → Accepted/Rejected → InStatement → Paid)

WoodStack            Code (e.g. Y1-S07), yard, species, built date, status (Receiving, Seasoning, Ready,
                     Depleted), current kg (from the ledger), average moisture, ready-from date.

WoodStockMovement    The ledger. Receipt (+), Issue (−), StackTransfer, Adjustment (count variance, drying
                     loss, spillage), RejectedReturn. Signed kg, balance after, unit cost (moving average).
                     Every stock figure in the module comes from this table.

WoodIssue            Factory, boiler or dryer (asset), date and shift, kg (or loads converted to kg), stack,
                     issued by. Creates a ledger movement and a reading on the asset's Fuel meter.

WoodStockCount       Stack-by-stack count by measurement or weighing, variance against the ledger,
                     adjustment entered after approval above a set size.

WoodStatement        A supplier's settlement for a period: deliveries, gross, deductions, advances, net
                     payable, status (Draft → Approved → Paid), payment reference (e.g. M-Pesa code).

ProductionDay        Factory, date, made tea kg, green leaf kg. The wood KPIs need made tea per day.
                     (Today made tea is only a meter reading; see decision 4.)
```

The delivery is the same shape as the grain spec's `IntakeLot` (gross, tare, net, tests, decision,
deduction). If the Quality add-on is built later, the two can share an intake engine; wood fuel does
not wait for it.

---

## 5. Receiving a delivery

1. The truck reaches the gate. The guard logs the vehicle (existing), purpose *Delivery*. A wood
   delivery is started from that entry, or from the clerk's phone.
2. **Measure.** By the factory's basis:
   - **Weighbridge:** gross on the way in, tare on the way out. Net = gross − tare.
   - **Stacked volume:** length × width × height × the species' stacking factor, then kg from the species'
     density at base moisture (confirm both).
   - **Load count:** tractor or lorry loads at a set size.
3. **Moisture.** Three samples with a handheld moisture meter, averaged. Each sample is checked as it
   is entered: a reading above the reject limit raises a warning at once.
4. **Decision.** The clerk picks Accept, Reject (reason, slip for the driver) or Accept with deduction.
   The deduction in kg and the amount payable are worked out, and shown before they are saved.
5. **Stack.** The accepted wood goes to a stack. The ledger records a receipt.
6. **Slip.** A delivery slip PDF (QuestPDF, same renderer as the other documents) is printed or sent
   to the supplier's phone.
7. **Gate.** The truck's exit is allowed only when the delivery has its tare weight (weighbridge basis)
   or a decision.

### The deduction (confirm with the pilot)

Default rule, editable per factory and per price agreement:

```
payable kg = net kg × (1 − deduction % per point × points over the base moisture)
rejected   = anything over the reject-above %
```

Whatever each factory does today is the rule to build. Some pay a lower price per tonne in bands
rather than deducting kg; the agreement supports both (decision 3).

Works with no signal. The delivery number comes from the server when it syncs, as with visitor
check-outs.

---

## 6. The yard and the burn

- **Stacks.** A stack is built, seasons for the factory's number of days, becomes *Ready*, then
  *Depleted*. The yard board lists stacks with kg, moisture and ready-by date, so the foreman burns the
  driest ready stack first (FIFO, or driest first, by setting).
- **Issuing.** At each shift the boiler attendant (or the clerk) records wood fed to each boiler or
  dryer, in kg or loads, from a stack. It decreases the ledger and adds a reading to the boiler's
  `Fuel` meter, so there is **one entry**, not two.
- **Days of cover.** Closing stock divided by the average daily burn over the settings' window. Below
  the alert level, the store manager and procurement are notified.
- **Counts.** A periodic stack count (measure, or weigh a sample) gives the variance against the
  ledger. Adjustments above a set size need approval (existing approval flow). Drying loss is expected
  and posted as its own adjustment kind, so it isn't confused with theft.
- **Loss check.** Opening + received − issued ± adjustments = closing. A weekly report flags
  unexplained gaps above a threshold.

---

## 7. Money

- Each accepted delivery has its **amount payable**: payable kg × price, or the band price.
- **Supplier statements** group deliveries for a period, subtract advances and deductions, and give the
  net payable. Approving a statement uses the existing approval matrix.
- **Payment** is recorded with a reference. The statement exports as CSV, including a file shaped for
  M-Pesa bulk payment (confirm the current format). Ecosys does not move money; finance pays and enters
  the reference.
- **Own woodlots** (common for KTDA-group factories) appear as a supplier of type *Own woodlot* with a
  zero or internal price, so the same ledger and KPIs apply. Harvest and replanting records are later.

---

## 8. Reports and KPIs

| Report | What it shows |
|---|---|
| **Yard board** | Stock now in tonnes, days of cover, stacks by status and moisture, deliveries awaiting a decision |
| **Wood per unit** | kg wood per kg made tea, by day, week and boiler, against the target |
| **Wood cost** | Cost per tonne and per kg made tea, by supplier and by month |
| **Supplier performance** | Volume, average moisture, rejects, deductions, share of deliveries |
| **Reconciliation** | Opening, received, issued, adjustments, closing, and the unexplained gap |
| **Moisture** | Average by supplier and species, over time, and the effect on wood per unit |
| **Sourcing** | Every delivery with source area, species, permit and photos, as an evidence report for auditors |

The factory KPI page gains *Wood per unit* and *Wood cost per unit*. When the add-on is on, the
ledger replaces the meter-based fuel figure so the two never disagree. The group dashboard shows
wood per unit by factory, so a poor factory stands out.

---

## 9. Screens

| Area | Screen |
|---|---|
| Office | **Wood fuel** with tabs: Yard, Deliveries, Issues, Suppliers and prices, Statements, Reports, Settings |
| Clerk's phone (PWA, offline) | *Receive wood*: supplier, measure, three moisture readings, decision, stack, slip. *Issue to boiler*. *Count stack* |
| Gate | The existing gate screens gain a "delivery open" marker on a vehicle |
| Boiler house | A shift entry on the boiler's page: wood fed |

Words follow the industry pack: "Wood fuel" for tea; the same screens can say "Bagasse" or "Husk" for
other industries later.

---

## 10. Licensing, permissions, gating

- **Module key** `woodfuel`, added beside `visitors` and `security` in `ServiceOpsPlans`
  (`OperationsAddOnModules`). New controllers (`WoodDeliveries`, `WoodYard`, `WoodIssues`,
  `WoodSuppliers`, `WoodStatements`, `WoodFuelReports`, `WoodFuelSettings`) go in a
  `WoodFuelControllers` set; `ServiceOpsModuleClassificationTests` fails for any controller not
  classified. `ProductionDay` sits with the Maintenance KPI controllers.
- **Permissions:** `woodfuel.receive` (yard clerk, weighbridge), `woodfuel.issue` (boiler attendant),
  `woodfuel.manage` (settings, prices, counts, statements, adjustments). Granted to people or
  permission groups, as for visitors and security. Managers get all by default.
- **Seats:** yard clerks and boiler attendants count as field seats.
- **Suppliers are not users** and never count as seats.
- **Pricing:** an add-on per factory (decision 7).

## 11. Data protection (Kenya DPA 2019)

Suppliers are individuals. Store the **last four** of the national ID plus a salted hash for
re-identification, never the full number; phone and payment details are visible to `woodfuel.manage`
only; erase or anonymise a supplier on request when no statement or audit period still needs the
record (same approach as the visitor module).

---

## 12. Tea pack additions

Within `TeaIndustry` (no database change):

| Part | Addition |
|---|---|
| Sections | *Wood yard* under the boiler house, if the factory doesn't have it |
| Patrol checkpoints | Wood yard fire patrol points; stack gap and fire break check |
| Running checks | *Boiler fuel feed* (moisture at the feed, fuel level), *Wood yard daily* (stack condition, fire breaks, extinguishers) |
| Failure codes | Fuel feed blockage, grate clinker, wet fuel derate |
| Visitor zones | *Wood yard* as Restricted for visitors; weighbridge already exists |

Other packs can add their own fuel words later (bagasse for sugar, husks for others).

---

## 13. Phasing

| Phase | Content | Estimate |
|---|---|---|
| W1 | Module key and gating, permissions, settings, species, suppliers, price agreements, migrations, tests | 1.5–2 weeks |
| W2 | Deliveries: three measure bases, moisture samples, decision and deduction, delivery slip, gate link, offline clerk page | 3 weeks |
| W3 | Stacks and ledger, issues to boilers (with the Fuel meter reading), counts and variance, days-of-cover alert | 3 weeks |
| W4 | Production days, KPIs and group tiles, reports, delivery CSV export for finance, sourcing report | 2 weeks |
| W5 | Tea pack additions, demo workspace data, browser tests, help guide | 1 week |

Rough, for one engineer. **W1–W3 is a usable pilot** (receive, stock, burn): about 8 weeks. W4 is the
part that makes the manager's case for the add-on, but it needs made-tea data (decision 4).

---

## 14. Decisions and questions for a pilot factory

| # | Question | Why it matters |
|---|---|---|
| 1 | How is wood bought today: by weighbridge tonne, stacked m³, or per load? Does the factory have a weighbridge for wood? | The default measure basis, and whether the stacked-to-kg factors are needed at all |
| 2 | How is moisture measured, and what is the base moisture and reject limit? | The deduction rule and the settings defaults |
| 3 | Deduct kg for wet wood, or pay a lower price in bands? | Which price rule to build first |
| 4 | Where does daily made tea come from (the factory's own system, a spreadsheet, nothing)? | `ProductionDay` entry, CSV import, or an integration |
| 5 | Does the factory pay suppliers through Ecosys, or only record deliveries for finance? | Whether W4 includes statements or only a delivery export |
| 6 | Own woodlot, bought wood, or both? Briquettes, husks, furnace oil or coal as well? | Own woodlot as a supplier type; whether "wood fuel" must become "boiler fuel" |
| 7 | Priced how: per factory per year, or per seat? | Fits the price book; compare with Security Patrol |
| 8 | Which certifications ask for fuelwood sourcing records? | The sourcing report's columns |

## 14a. Pilot answers (Nov 24) and what they change

| # | Answer | Effect on the build |
|---|---|---|
| 1 | Factories buy all three ways: weighbridge tonne, stacked m³ and per load. Some have a wood weighbridge, some don't | All three measure bases are built from the start. The basis is chosen **per delivery**, with the factory's setting as the default |
| 2 | Base moisture and reject limit unknown | Settings hold the numbers, with starting defaults a manager can change (base 25 %, reject above 45 %) **(confirm)**. Nothing is hard-coded |
| 3 | Deduct kg or price bands: unknown | **Both** rules are built. A factory's setting picks one: *Deduct kg* or *Price bands*. Default is *Deduct kg* |
| 4 | Daily made tea is typed in by hand | `ProductionDay` has a simple daily entry screen (made tea kg, green leaf kg). A CSV import can follow |
| 5 | Ecosys only records deliveries for finance | **No supplier statements.** Each delivery carries its amount payable, and a CSV export lists deliveries for finance. Statements stay out of scope |
| 6 | Some factories burn briquettes | *Briquettes* and *Other biomass* are fuel forms from the start. Wood and briquettes are tracked in kg and reported together and by form. Own woodlot stays as a supplier type |

---

## 14b. Build status (Nov 25, 2026)

W1–W3 and the core of W4 are built and tested: 28 backend tests, 9 unit tests on the screen's arithmetic,
and 6 browser tests against the real screens (including a delivery saved with the network off).

Built as specified:

- Module `woodfuel`, an add-on taken with any edition; permissions `woodfuel.receive`, `woodfuel.issue`
  and `woodfuel.manage` (managers get all three by default).
- Deliveries on all three measure bases, chosen per delivery. Moisture samples, the average, and both
  moisture rules (*Deduct kg* and *Price bands*), with every number in the factory's settings.
- The yard ledger, stacks with a seasoning date, issues to boilers and dryers, counts that post the
  difference, days of cover, and the yard board with a period reconciliation.
- Each issue adds a reading to the asset's wood meter (a `Fuel` meter in kg, created on first use). An
  entry dated before the meter's latest reading is kept without a meter reading, so a phone's queued
  entry is never lost.
- Daily made tea entered by hand; wood per kg of made tea and its cost.
- Suppliers (only the last four of a national ID kept), price agreements (most specific wins), a CSV of
  deliveries for finance. No supplier statements, as decided.
- Receiving and feeding work with no signal through the existing outbox.
- **Notifications** to the factory's supervisors, each with its own notification key so people choose
  their channels: a **rejected load** (supplier, vehicle, weight, reason), **wet wood** (an accepted load
  at or above the factory's alert level, default 35%, with how it was paid) and **low cover**, once per
  dip, cleared when a delivery brings cover back up so the next dip is told again; under a day is
  critical. They are sent after the delivery or issue is saved, so a failure never undoes it.
- **The gate link.** A clerk picks the truck from the gate log (Delivery and Collection vehicles inside
  now); the registration and driver are filled in. For a weighbridge delivery the clerk can save the
  *weigh-in* and finish it at the tare: the delivery is *Awaiting tare*, nothing is in the ledger and
  nothing is payable until it is weighed out. The gate's vehicle register shows the waiting delivery
  and **refuses to let the truck out** until it is weighed out; a visitor manager can override. A truck
  turned away is closed out with no tare. A vehicle must have come in at the delivery's own factory.
- **Tea pack extras:** a *Wood yard* section, a *Wood yard* asset class with a daily fire and stack
  check, a *Boiler fuel feed* check, four failure codes, a *Wood yard* access zone, and two fire patrol
  checkpoints (added last so existing checkpoint numbers do not change). The pack already had a
  firewood store zone and checkpoint, which are kept.

Built differently from this spec:

- An issue with no stack named can only take wood that was received without a stack, so no stack's wood
  can leave "unstacked" and push that balance below zero.
- A stack's seasoning clock restarts when wood is added to it; its status is changed by hand, and the
  yard board shows *Ready (dry enough)* once the date has passed.
- Counting is open to yard clerks as well as managers, because the difference is posted at once.
- Alerts go to the factory's supervisors (admins and roles that may assign work orders, limited to the
  factory they can see), not to people who were granted a wood fuel permission through a group.

Not built yet:

- An unexplained-loss alert (the yard board shows the gap).
- Wood per unit on the factory KPI page and the group dashboard (the figures are on the yard board).
- The sourcing report for auditors and photos on a delivery.
- Own-woodlot harvest and replanting records; a CSV import for made tea.
- Wood yard extras in the demo workspace.
- PostgreSQL translation tests for the new queries (they need the database the other `*_AllTranslate`
  tests use). The gate register query was also changed and its existing translation test could not run here.

## 15. Recommendation

Build **W1–W3** with a pilot tea factory signed up, and let its decisions 1–4 shape the defaults. Treat
**W4 statements** as optional until decision 5 is answered. Do not model wood as a stock item: the
moisture-based price, stacks and days of cover are the point of the module.

Start with the delivery and the yard board. They are the two screens a factory manager will open every
morning, and they need no data from outside Ecosys.
