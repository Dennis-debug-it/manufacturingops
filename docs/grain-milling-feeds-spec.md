# Maize milling and animal feeds — design

Nov 19, 2026 · Draft for review

How Ecosys serves a **maize mill** (sifted and whole-meal flour, with bran and germ as by-products) and
an **animal feeds plant** (mash, pellets and crumbles for poultry, dairy, pigs and fish). Many Kenyan
millers run both on one site: the mill's bran and germ go into the feed. The design has two layers:

1. **Two industry packs**, `maize` and `feeds`, on the existing pack mechanism
   ([industry-packs.md](industry-packs.md)). They make maintenance, running checks, the weekly window,
   compliance, visitors and patrols fit a mill on day one. No new screens.
2. **A new add-on module, Intake, Quality and Traceability**. It covers what a food or feed plant needs
   and a tea factory doesn't:
   - grain intake and aflatoxin testing
   - silo stock and fumigation
   - production runs and flour fortification
   - feed formulas and medicated batches
   - product holds and release
   - lot traceability and recalls
   - dispatch checked at the gate

   It is written so dairy, sugar and tea can use it later.

Items marked **(confirm)** are regulatory or technical details to check with the pilot customer and
the current KEBS standards before they are built in as defaults.

---

## 1. Why a mill is different

| Area | What a mill or feed plant has that we don't cover yet |
|---|---|
| Raw material | Maize bought from many farmers and traders by weight. Each load is tested for moisture and aflatoxin, and accepted, rejected or paid less |
| Food safety | Aflatoxin, metal (magnets, metal detectors), torn sieves, pests and fumigant residues. A failed check can mean holding product already made |
| Fortification | Maize flour must be fortified **(confirm the current regulation and dose)**. Inspectors compare premix used with flour made |
| Feeds | Formulas (recipes) with versions; mixing uniformity; medicated feed that must not carry into the next batch; pellet quality |
| Wear parts | Wear depends on tonnes, not hours: roller-mill rolls (regrinding/fluting), hammers and screens, pellet dies and rollers |
| Hazards | Grain dust can explode. Silos are confined spaces. Fumigation uses phosphine. Bucket elevators and conveyors are pinch and entanglement points |
| Security | Bags of flour and feed are easy to steal. Outgoing loads must match the dispatch note |

## 2. Layer 1: industry packs

Two packs share grain-handling plant through a new `GrainCommon`, the same way packs share
`IndustryCommon` (boilers, compressors, generators, Kenyan statutory core).

| Pack | Key | Class prefix | Production unit | Quiet season |
|---|---|---|---|---|
| Maize mill | `maize` | `MZE-` | tonnes of maize milled (`t milled`) | depends on the harvest; often none (confirm) |
| Animal feeds | `feeds` | `FED-` | tonnes of feed produced (`t feed`) | none: steady all year |

**Words used on screen:**

| | Maize mill | Animal feeds |
|---|---|---|
| Yearly shutdown | "annual shutdown" | "annual shutdown" |
| Raw material | "maize" | "raw materials" |
| Supply fleet | "Grain haulage" | "Raw material haulage" |

### 2.1 Sections

**Maize mill:**
1. Intake and weighbridge
2. Pre-cleaning and cleaning
3. Silos and storage
4. Conditioning and degermination
5. Milling
6. Sifting
7. Fortification and blending
8. Packing
9. Finished goods and dispatch
10. Utilities

**Feeds plant:**
1. Raw material intake
2. Bulk storage (silos, bins, bulk shed)
3. Grinding
4. Batching and dosing
5. Mixing
6. Liquids (molasses, oil)
7. Conditioning and pelleting
8. Cooling and crumbling
9. Extrusion (optional)
10. Packing
11. Finished goods and dispatch
12. Utilities

### 2.2 Asset classes (with tonnage meters)

Shared (`GrainCommon`):

| Class | Code | Criticality | Meters / key fields |
|---|---|---|---|
| Weighbridge | `GRN-WB` | A | capacity t; last verification date |
| Intake pit and elevator | `GRN-PIT` | B | t handled |
| Bucket elevator | `GRN-ELV` | A | t handled; belt type, bucket count |
| Chain / screw conveyor | `GRN-CNV` | B | run hours |
| Pre-cleaner / drum sieve | `GRN-PRC` | B | t handled |
| Magnet (drum, plate, grate) | `GRN-MAG` | A (food safety) | location, type |
| Aspirator / cyclone / bag filter | `GRN-ASP` | B | filter ΔP |
| Silo with temperature cables | `GRN-SIL` | A | capacity t; cable count |
| Aeration fan | `GRN-FAN` | C | run hours |
| Rotary valve / airlock | `GRN-RV` | B | run hours |
| Explosion vent / suppression panel | `GRN-EXV` | A (safety) | vent area; last inspection |
| Metal detector | `GRN-MD` | A (food safety) | test piece sizes (Fe, non-Fe, SS) |
| Packing scale and bagger | `GRN-PKS` | A | bags packed; legal verification date |
| Bag stitcher / palletiser | `GRN-STC` | C | bags |

Maize mill (`MZE-`): destoner, dampener / conditioning screw, degerminator, **roller mill** (meter:
t milled since last roll regrind; fields: roll length, fluting, gap), plansifter, purifier, hammer mill
(t since last hammer turn), **fortification dosifier / microfeeder** (A; field: dose g/t,
**(confirm)**).

Feeds (`FED-`):
- **Hammer mill:** t since hammer turn; screen size.
- **Batching scale and micro-ingredient scale:** A; legal verification.
- **Mixer** (ribbon or paddle): A; last uniformity (CV) test.
- **Liquid dosing:** molasses, oil.
- **Steam conditioner:** target temperature.
- **Pellet mill:** A. Meters: t since die change, t since roller change. Fields: die hole size and compression ratio.
- **Cooler** (counterflow).
- **Crumbler.**
- **Extruder** (optional).

Boilers, compressors, generators, transformers and fire equipment come from `IndustryCommon`.

### 2.3 Running checks (shift / daily)

| Check | Asset class | Highlights |
|---|---|---|
| Magnet clean-out | Magnet | What was found (none / dust / metal pieces). **Metal pieces raises a quality incident** |
| Metal detector test | Metal detector | Fe / non-Fe / SS test pieces rejected? **A fail puts product made since the last pass on hold** (Layer 2) |
| Sieve and sleeve check | Plansifter / sieves | Torn sieve? **A tear raises a quality incident and a hold** |
| Roller mill | Roller mill | Bearing temperatures, roll gap, feed even, amps |
| Dosifier check | Fortification dosifier | Premix hopper level, catch test g/min vs target (Layer 2 uses it) |
| Silo temperatures | Silo | Highest reading per cable; above the limit raises a job "turn or aerate" (confirm the limit) |
| Dust and housekeeping | Plant areas | Dust on beams and ledges, spillage, explosion vents clear |
| Pellet mill | Pellet mill | Amps, conditioner temperature (below target = feed-safety issue; confirm the target), die and roller noise |
| Bag filter | Aspirator / filter | ΔP within range; pulse cleaning working |
| Packing scale check weight | Packing scale | 10-bag check-weigh vs declared weight |

### 2.4 Weekly window routines

- Roll changeover and regrind (by the t-milled meter)
- Turning and replacing hammers, and checking screens
- Pellet die and roller inspection (by t meters)
- Bucket elevator: belt tracking, bucket bolts, tail pulley clean-out, belt-slip / speed switch test
- Sifter and sieve full clean-out
- Explosion vents, dust extraction and housekeeping inspection
- Mixer uniformity (CV) test, monthly (confirm the frequency)

### 2.5 Failure codes (on top of ISO 14224)

Roll wear / dull fluting, roll chipped, choke (spout, elevator, hopper), sieve torn, hammer worn, screen
holed, die blocked / plugged, roller slip, wet material bridging, bearing overheat, dust leak, belt
misalignment / slip, metal contamination.

### 2.6 Statutory and certificates (Kenya; all to confirm with the pilot)

**Product, food and feed:**
- KEBS product certification (Standardization Mark) per product and its renewal
- Fortification compliance (inspections, premix supplier certificates)
- County food hygiene / public health licence
- Food handlers' medical certificates, usually 6-monthly
- Directorate of Veterinary Services requirements for medicated feeds

**Plant safety and equipment:**
- Workplace registration and DOSHS audits
- Boiler and pressure vessel examination
- Lifting equipment and hoists
- Fire safety audit
- Weighing equipment verification under the Weights and Measures Act: weighbridge, packing and batching scales

**Fumigation and environment:**
- Licensed fumigation contractor (PCPB) and fumigation certificates per silo
- NEMA licences and environmental audit

Medical certificates go on the existing **training / certification** records per person. Equipment
items go on **Compliance**.

### 2.7 Visitor zones and patrol checkpoints

**Visitor zones:**

| Zone | Level | Needs |
|---|---|---|
| Milling floor / feed plant | Restricted | Escort, induction; PPE: hearing protection, dust mask, hairnet. Food hygiene: no jewellery, no food |
| Packing hall | Restricted | Hairnet, coat |
| Silos | Permit | Confined space |
| Fumigation area | Restricted | **No entry while fumigating** |
| Weighbridge | Public | |
| Boiler house | Restricted | |

**Patrol checkpoints:** weighbridge, intake pit, silo bank, premix / medication store (locked, controlled),
finished goods warehouse, loading bay, fuel store, perimeter. Questions such as:
- "Premix store locked?"
- "Warehouse seal intact?"
- "Any lorry loading without a dispatch note?"

**Effort:** the two packs are mostly data. About **1–1.5 weeks** including tests (`IndustryPackTests`).
No screen or database changes.

---

## 3. Layer 2: Intake, Quality and Traceability (new add-on)

Module key `quality`, sold like Visitors and Security: an add-on per factory, gated through
`ServiceOpsPlans`. Permissions:
- `quality.intake`: weighbridge clerk, intake lab
- `quality.manage`: QA manager: specifications, release, recall
- `production.record`: shift supervisor

### 3.1 Entities

```
Commodity            Maize, wheat bran, maize germ, soya meal, sunflower cake, cotton seed cake, fishmeal (omena),
                     limestone, DCP, salt, premix, molasses, oil, packaging. Unit, kind (raw / premix / packaging / product).
CommoditySpec        Per commodity: limits for each test, with action Reject / Deduct / Hold / Warn.
                     e.g. maize: moisture ≤ 13.5 % (confirm), total aflatoxin ≤ 10 ppb and B1 ≤ 5 ppb (confirm
                     against the current KEBS maize standard), foreign matter, broken grains, insect damage.
QualityTest          Name, unit, method (rapid strip / reader / lab), linked to a CommoditySpec limit.

IntakeLot            Number IN-2026-0001; commodity; supplier (existing Supplier) or farmer name and phone;
                     vehicle (existing VehicleMovement: the gate log); weighbridge gross / tare / net;
                     results (IntakeResult rows); decision Accepted / Rejected / Accepted with deduction
                     (moisture shrink and quality deduction in kg); where it went (StorageLocation);
                     sampler, tested by, times. A rejected load is logged and its vehicle leaves.
StorageLocation      Silo, bin, bay or bulk shed: capacity, commodity, current stock (sum of lots in minus out),
                     lots it holds (FIFO), last fumigation, last temperature readings (from running checks).
Fumigation           Location, fumigant (phosphine), dose, start, end, licensed contractor (existing Contractor),
                     re-entry time, gas-free check. Opens a WorkPermit (existing) and blocks the silo
                     for intake and draw-off until gas-free.

Product              Sifted flour 2 kg, whole meal 1 kg, bran 50 kg, layers mash 70 kg, broiler finisher pellets...
                     Pack size, unit, fortified (y/n, dose), specification.
Formula (feeds)      Product + version: ingredient % (commodities), approved by (nutritionist), effective from;
                     medicated (drug, inclusion rate, withdrawal period); species.
ProductionRun        Line (section), product, shift, start / end, inputs (lots and kg drawn from StorageLocations),
                     outputs (FinishedLot), by-products (bran, germ: kg to stock), premix used (g),
                     downtime (existing jobs and breakdowns on the line), energy (existing meters).
FeedBatch            One mixer batch within a run: formula version, weighed amounts vs target per ingredient
                     (entered, or imported from the batching system), mix time, flush batch after a medicated one.
FinishedLot          Lot code printed on the bag (e.g. 2611-L2-A: date, line, shift), product, kg / bags,
                     status Hold / Released / Rejected, tests (moisture, aflatoxin; feeds: protein, PDI),
                     retained sample location, certificate of analysis (PDF).
QualityHold          A hold on lots, from a failed check (metal detector, sieve), a failed test or by hand:
                     reason, lots held, decision (release / rework / downgrade / destroy), by whom.
Dispatch             Customer, vehicle, driver, lots and bags per lot, delivery note number.
                     Checked at the gate (Layer 2.6).
RecallDrill / Recall A lot, the direction (back to suppliers / forward to customers), what was found, time taken.
```

Reused, not rebuilt:
- **Suppliers, contractors, vehicles (gate log):** existing.
- **Work permits and incidents:** existing Safety.
- **Running checks and meters:** existing.
- **Stores and stock movements:** existing inventory, for packaging and premix as stores items.
- **Attachments, audit log, approvals:** existing.

### 3.2 Intake (weighbridge and lab, offline-capable)

1. The truck arrives. The guard's vehicle log entry (existing) links to a new **IntakeLot**.
2. **First weighing (gross)** at the weighbridge.
3. **Sample and test** on a phone or tablet in the intake lab:
   - Moisture and aflatoxin readings, foreign matter.
   - Each result is checked against the commodity's specification as it is entered.
4. **Decision:**
   - Accept, choosing a silo.
   - Reject, with a reason and a slip for the driver.
   - Accept with deduction, where the kg deducted is worked out from moisture above the limit (confirm the formula with the miller).
5. **Second weighing (tare).** Net weight and payable weight are worked out, and the intake slip PDF is printed for the supplier.
6. **The silo's stock goes up.** A load into a silo under fumigation is refused.

Works with no signal, using the same outbox as the guard app. The lot number comes from the server
when it syncs.

### 3.3 Production and fortification

- **A shift supervisor records a run:** product, lots or silos drawn from, kg made (or bag count × pack
  size), by-products, and premix used.
- **Fortification reconciliation** for each run, day or month:
  - **expected premix** = flour kg × dose (g/kg)
  - **compared with premix issued** from the premix store (an existing stock movement)
  - shown against the dosifier catch tests from the running checks
  - a variance beyond ±10 % (customer-set) is flagged
  - this gives the report a fortification inspector asks for
- **Extraction rate:** flour out ÷ maize in, per run and per week, with bran and germ. The trend shows
  roll wear (linked to the t-milled meter) and changes in grain quality.

### 3.4 Feeds: formulas, batches and medicated feed

- **Formulas:**
  - Formulas are versioned. Only an approved version can be produced.
  - A change means a new version, and the old one stays on record.
  - Importing from formulation software (CSV) is a later step.
- **Batches:**
  - Each batch records the target and actual weight per ingredient.
  - A deviation beyond tolerance (customer-set per ingredient; micro-ingredients tighter) is flagged.
  - It needs a supervisor's sign-off.
- **Medicated feed:**
  - The run screen requires a **flush batch**, or a production order that avoids carry-over, before a
    non-medicated feed for sensitive species (e.g. layers producing eggs) (confirm the rules).
  - The bag label shows the withdrawal period.
- **Quality tests:**
  - Pellet durability (PDI) per lot.
  - Mixer uniformity (CV) test results stored against the mixer asset.

### 3.5 Holds, release and certificates

- Finished lots start **On hold** until released by QA. For small mills, releasing on the run's tests
  can be the default (setting).
- **Automatic holds:**
  - **Metal detector test fail:** lots of that line since the last passed test.
  - **Torn sieve found:** lots of that line since the last good sieve check.
  - **Test result out of specification:** the lot tested.
- **Releasing a held lot** needs a decision and a reason. It is logged with the user, as with gate overrides.
- **Certificate of analysis** PDF per lot (QuestPDF): test results, standard, signatory.

### 3.6 Dispatch and the gate

- Finance or sales creates the **Dispatch**: lots and bags. Only released lots can be dispatched.
- At the gate the guard **scans the dispatch QR** (offline-capable, like badges) and checks it:
  - the vehicle registration
  - the number of bags (counted or seal-checked)
  - recorded with the vehicle's exit in the gate log
- A mismatch raises a **security incident** (existing) with photo evidence.
- This closes the main theft route for bagged product.

### 3.7 Traceability and recalls

- **Back trace** from a finished lot: runs → batches → raw lots → suppliers and their test results →
  silo and its fumigation.
- **Forward trace** from a raw lot: runs → finished lots → dispatches → customers and quantities.
- **Recall drill:** pick a lot, run both traces, and record how long it took and what was found. The
  drill report is evidence for KEBS, ISO 22000 / FSSC audits and big buyers.
- A real recall uses the same screens, plus a customer contact list and a quantity-recovered log.

### 3.8 Reports and KPIs

| KPI | Mill | Feeds |
|---|---|---|
| Throughput t/h and t per shift | ✓ | ✓ |
| Extraction rate (flour %, bran %, germ %) | ✓ | |
| Intake rejection rate and aflatoxin results by supplier / region | ✓ | ✓ |
| Fortification variance (premix expected vs used) | ✓ | |
| Formula accuracy (batches within tolerance %) | | ✓ |
| PDI and pellet fines | | ✓ |
| kWh per tonne, fuel / steam per tonne | ✓ | ✓ |
| Downtime and cost per tonne (existing KPIs, in tonnes) | ✓ | ✓ |
| Holds raised / released / destroyed; time to trace in drills | ✓ | ✓ |
| Silo stock and age (days in store) | ✓ | ✓ |

### 3.9 Screens

- **Intake:** a weighbridge queue (in yard / awaiting test / awaiting tare), and a lot screen with
  tests and the decision. Phone-first for the lab.
- **Stores:** silos and bins with stock bars, lots in each, temperatures, fumigation status.
- **Production:** today's runs per line, start / record / close a run, and batches for feeds.
- **Quality:** holds to decide, lots awaiting release, specifications, test definitions.
- **Formulas** (feeds): versions, approval, compare two versions.
- **Dispatch:** create, print the note with QR, and the gate check in the guard app.
- **Trace:** search by lot, a back / forward tree, and recall drills.
- **Reports:** the KPIs above, fortification reconciliation, and supplier quality.

### 3.10 Integrations (later)

- **Weighbridge indicator** (serial / TCP) through the existing sensor gateway pattern (`SensorGateway`):
  gross and tare filled in automatically. Typed in until then.
- **Batching system / PLC:** a CSV or OPC export of batch actuals.
- **Formulation software:** CSV import of formulas.
- **Rapid aflatoxin readers:** typed in; some export CSV.
- **Accounting / ERP:** supplier payables from intake slips, and sales from dispatches (API and webhooks exist).

---

## 4. Phasing

| Phase | Content | Estimate |
|---|---|---|
| G1 | Industry packs `maize` and `feeds`, `GrainCommon`, tests | 1–1.5 weeks |
| G2 | Intake (lots, specs, tests, decision, slips), storage locations and stock, fumigation with permit | 3 weeks |
| G3 | Production runs, by-products, fortification reconciliation, finished lots, holds and automatic holds, CoA | 3–4 weeks |
| G4 | Feed formulas and batches, medicated rules, dispatch and gate check, traceability and recall drills, reports | 3 weeks |
| G5 | Weighbridge and batching integrations | as needed |

G1 can ship on its own: a mill could run maintenance, checks, compliance, visitors and patrols with
just the pack. G2–G4 make up the add-on.

## 5. Pricing (fits the price book)

- **Maintenance editions:** priced per factory as for other industries. The pack is included.
- **Intake, Quality and Traceability add-on:** suggested **KES 60,000–80,000 per factory per year**,
  about the price of Security Patrol. Add to the price book when G2 ships.
- **Implementation:** includes specifications per commodity, silo set-up and formula loading. Use the
  upper end of the implementation range for a feeds plant.
- **Hardware for the catalogue:** moisture meter, rapid aflatoxin reader, label printer for lot codes.
  Supplied at cost plus margin.

## 6. Decisions and questions for a pilot mill

| # | Question | Why it matters |
|---|---|---|
| 1 | Mill size: posho / small sifted mill or large roller mill? | Small mills need intake, fortification and dispatch, not formulas or full traceability |
| 2 | Feeds: which species and products; formulated in-house or by a consultant; any medicated lines? | Decides how deep G4 goes |
| 3 | Current aflatoxin, moisture and deduction rules at intake | Default specifications and the shrink formula |
| 4 | Fortification dose and how inspectors check it today | The reconciliation report format |
| 5 | Weighbridge make and model; batching system | Integration effort |
| 6 | Release by QA for every lot, or release on test results? | Default for holds |
| 7 | Certification held or wanted (KEBS mark, ISO 22000, FSSC)? | Recall drill and audit evidence |

## 7. Recommendation

Build **G1 (the packs)** now if a mill is in the sales pipeline: it is cheap, and it lets a demo show a
mill instead of a tea factory. Build **G2–G4 only with a pilot mill signed up**. Its intake rules,
fortification checks and formulas should shape them, as the tea pilot shapes Maintenance.
