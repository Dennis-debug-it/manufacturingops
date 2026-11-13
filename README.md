# Ecosys Factory Maintenance

An enterprise-grade CMMS (computerised maintenance management system), launching with tea factories and built to expand to other asset-intensive industries (sugar, dairy, cement, flower farms, data centres, hospitals).

It moves factories from reactive, paper-based maintenance to planned, traceable, data-driven maintenance.

> Every asset is identifiable, every maintenance activity is traceable, every resource is accountable, and every decision is supported by data.

## Key characteristics

- **Offline-first mobile** for technicians on the factory floor, with QR/barcode scanning
- **Three maintenance rhythms**: running checks, the weekly (Monday) window, and the annual low-crop overhaul
- **Multi-tenant, multi-factory** hierarchy: group → factory → section → asset → component
- **Built-in compliance**: OSHA/DOSH statutory inspections, calibration, food safety, Rainforest Alliance / Fairtrade evidence packs
- **API-first**, with ERP integration (SAP, Oracle, Dynamics 365) delivered in a later phase

## Documentation

- [Requirements](docs/requirements.md)
- [Scope of work](docs/scope.md)
- [Industry packs](docs/industry-packs.md) (tea, sugar, dairy, general; how to add one)
- [Factory Operations spec: Visitor Management and Security Patrol](docs/operations-platform-spec.md)
- [ServiceOps provenance](docs/serviceops-provenance.md) (fork point and ported fixes)
- [Deployment](docs/DEPLOYMENT.md), [VPS setup](docs/VPS_SETUP.md), [WhatsApp setup](docs/WHATSAPP_SETUP.md)

## Status

Stack: .NET 10 + PostgreSQL + React PWA.

- **Phase 0 (done):** forked from Ecosys ServiceOps (with history). Out-of-scope modules pruned:
  client finance (quotations, invoices, credit notes, receipts, withholding, expenses), the sales
  and platform CRM, the client portal, projects, vans and dispatch, CSAT, and Kip AI and the
  knowledge base. The migration history is squashed into one baseline (`InitialBaseline`).
- **Phases 1–6 (done):** factory hierarchy and assets, work orders and the technician app,
  preventive maintenance and the weekly window, safety and compliance, spares and contractors,
  costs, KPIs and evidence packs.
- **Phase 7 (in progress):** condition monitoring, sensor feeds, predictive groundwork and
  leaf collection fleet maintenance.
- **Factory Operations add-ons** ([spec](docs/operations-platform-spec.md)), switched on per tenant
  from Platform → Tenants:
  - **Visitor Management** (built): access zones, host approval, gate checks, badges, roll call,
    vehicle in/out log, standing passes for regular contractors, offline badge scanning at the gate.
  - **Security Patrol** (built): checkpoints (QR, NFC, GPS), routes and schedules, the offline guard
    app, verified scans, missed-patrol alerts, security incidents, panic button, shift handover,
    supervisor board and reports.
