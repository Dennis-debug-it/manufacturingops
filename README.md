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
- [ServiceOps provenance](docs/serviceops-provenance.md) (fork point and ported fixes)
- [Deployment](docs/DEPLOYMENT.md), [VPS setup](docs/VPS_SETUP.md), [WhatsApp setup](docs/WHATSAPP_SETUP.md)

## Status

Phase 0: forked from Ecosys ServiceOps (with history); pruning out-of-scope modules. Stack: .NET 10 + PostgreSQL + React PWA.
