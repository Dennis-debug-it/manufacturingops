using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Generic per-tenant, per-module settings blob stored as JSON. Backs the simpler tenant
/// settings sub-pages (executive dashboard targets, inventory prefs, payments, portal, integrations)
/// that don't warrant their own dedicated table.
/// </summary>
public sealed class TenantModuleSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";

    public Tenant? Tenant { get; set; }
}
