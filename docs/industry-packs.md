# Industry packs

Ecosys is not tied to tea. Everything that depends on what a factory makes lives in one **industry
pack** per industry, under `backend/src/Ecosys.Infrastructure/Services/Factory/Industries/`:

| Pack | Key | Production unit | Yearly shutdown |
|---|---|---|---|
| Tea factory | `tea` | kg made tea | low-crop shutdown |
| Sugar mill | `sugar` | tonnes cane crushed | off-season shutdown |
| Dairy | `dairy` | litres of milk processed | annual shutdown |
| General manufacturing | `generic` | units produced | annual shutdown |

## What a pack holds

| Part | Used by |
|---|---|
| Sections | Organisation → Add default sections |
| Asset classes (fields, meters, criticality) | Asset classes → apply pack |
| Running checks (shift and daily) | Running checks |
| Weekly-window routines | Weekly window → add routines |
| Failure codes (on top of the ISO 14224 set) | Jobs (problem, cause, remedy) |
| Statutory items (Kenyan law) | Compliance → install |
| Visitor access zones | Visitors → Zones |
| Patrol checkpoints | Security → Checkpoints |
| Words: yearly shutdown, quiet season, supply fleet | Overhaul, PM deferrals, Fleet, Settings |
| Production unit and usual quiet season | Settings (filled in when the industry is chosen) |

Everything is a starting point. Once installed it is ordinary data the customer edits.

## Choosing an industry

- **Workspace:** Settings → Organisation → Factory settings → Industry.
- **One factory:** edit the factory and pick its Industry, for a group with different kinds of factory
  (e.g. a tea group that also runs a dairy). Sections under a factory follow it.

The server works a factory's industry out as: the factory's own, else the workspace's
(`IndustryTemplates.ForFactoryAsync`). Changing an industry never changes data already installed.

## Adding an industry (e.g. cement, flour, flowers)

1. Copy `GenericIndustry.cs` to `CementIndustry.cs`, give it a key (`cement`) and its own class-code
   prefix (`CEM-`). Codes must not be reused across packs.
2. Use `IndustryCommon` for plant every factory has (boilers, generators, compressors, the Kenyan
   statutory core, reception and substation zones).
3. Add it to `IndustryTemplates.All`.
4. Run the tests. `IndustryPackTests` checks every pack refers only to its own classes and zones.

No database change and no screen change is needed: the screens read the list of packs and their words.

## Limits

- The production unit is one per workspace. A group mixing tea and dairy reports KPIs in the
  workspace's unit.
- Tea's class codes (`TEA-…`) are what existing workspaces hold and must not change.
- The statutory items are the usual Kenyan ones; each customer checks them against its own licences.
