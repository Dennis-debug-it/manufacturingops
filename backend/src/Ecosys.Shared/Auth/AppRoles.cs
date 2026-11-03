namespace Ecosys.Shared.Auth;

public static class AppRoles
{
    public const string PlatformOwner = "PlatformOwner";
    public const string PlatformAdminRole = "PlatformAdmin";
    public const string SupportAdmin = "SupportAdmin";
    public const string FinanceAdmin = "FinanceAdmin";
    public const string ReadOnlyAuditor = "ReadOnlyAuditor";
    public const string SuperAdmin = "SuperAdmin";
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Support = "Support";
    /// <summary>
    /// Platform finance staff. Deliberately NOT "Finance": that collided case-insensitively with the
    /// tenant role <see cref="FinanceUser"/> ("finance"), leaving the separation between a customer's
    /// finance clerk and Ecosys finance staff resting on letter case alone.
    /// </summary>
    public const string Finance = "PlatformFinance";
    public const string Auditor = "Auditor";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Dispatcher = "Dispatcher";
    public const string Technician = "Technician";
    /// <summary>
    /// Stores and logistics: keeps the stock, issues materials and parts to jobs and vans, receives
    /// purchase orders, and moves stock between stores. Shown as "Materials Controller".
    /// </summary>
    public const string MaterialsController = "MaterialsController";
    /// <summary>Tenant finance user. Lower-case for historical reasons; see <see cref="Finance"/>.</summary>
    public const string FinanceUser = "finance";
    public const string Viewer = "Viewer";

    /// <summary>Legacy tenant role. Treated as <see cref="Viewer"/> when resolving permissions.</summary>
    public const string User = "User";

    /// <summary>
    /// A client-portal login: a customer contact of a tenant's client, not a member of tenant staff.
    /// Deliberately absent from both <see cref="PlatformRoles"/> and <see cref="TenantRoles"/>.
    /// </summary>
    public const string PortalClient = "PortalClient";

    public static readonly string[] PlatformRoles =
    [
        SuperAdmin,
        PlatformOwner,
        PlatformSuperAdmin,
        PlatformAdminRole,
        PlatformAdmin,
        SupportAdmin,
        Support,
        FinanceAdmin,
        Finance,
        ReadOnlyAuditor,
        Auditor
    ];

    public static readonly string[] TenantRoles =
    [
        Admin,
        Manager,
        Dispatcher,
        Technician,
        MaterialsController,
        FinanceUser,
        Viewer,
        User
    ];

    /// <summary>
    /// Roles that get an editable row in the tenant's role-permission matrix.
    /// <see cref="Admin"/> is excluded: it is implicitly granted every capability.
    /// <see cref="User"/> is excluded: it is a legacy alias for <see cref="Viewer"/>.
    /// </summary>
    public static readonly string[] AssignableTenantRoles =
    [
        Manager,
        Dispatcher,
        Technician,
        MaterialsController,
        FinanceUser,
        Viewer
    ];

    public static readonly string[] AllRoles =
    [
        .. PlatformRoles,
        .. TenantRoles
    ];

    /// <summary>
    /// Whether this role belongs to platform staff. The two lists share no name even ignoring case,
    /// so this stays correct however a caller happens to compare — a property pinned by
    /// RoleBoundaryTests rather than left to convention.
    /// </summary>
    public static bool IsPlatformRole(string? role) =>
        role is not null && Array.Exists(PlatformRoles, candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether this role belongs inside a tenant workspace.</summary>
    public static bool IsTenantRole(string? role) =>
        role is not null && Array.Exists(TenantRoles, candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this role is a client-portal login. Portal tokens carry a tenant_id claim (so portal
    /// queries can be scoped), which is exactly why "has a tenant" is not the same question as
    /// "is tenant staff" — see <see cref="AuthorizationPolicies.TenantStaff"/>.
    /// </summary>
    public static bool IsPortalRole(string? role) =>
        role is not null && string.Equals(PortalClient, role, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this principal belongs inside a tenant workspace: neither a client-portal login nor
    /// platform staff. Both of those carry a tenant_id claim, which is exactly why "resolves a tenant"
    /// cannot stand in for "is staff of that tenant" — a portal login read and silently deactivated
    /// other customers' records (EC-001), and platform staff fell into tenant query paths (EC-020).
    ///
    /// Stated as a denial of the two outside audiences rather than an allow-list of tenant roles: a
    /// tenant role added later is inside the workspace automatically, whereas a forgotten allow-list
    /// entry would lock real staff out and invite someone to "fix" it by loosening the check. The
    /// platform side mirrors <see cref="PlatformRoles"/> in full rather than hand-picking
    /// <see cref="SuperAdmin"/>, so this and <c>ITenantContext.IsSuperAdmin</c> cannot disagree about
    /// who counts as platform staff.
    ///
    /// The single definition behind the <see cref="AuthorizationPolicies.TenantStaff"/> policy.
    /// </summary>
    public static bool IsTenantWorkspacePrincipal(System.Security.Claims.ClaimsPrincipal principal) =>
        !principal.IsInRole(PortalClient)
        && !Array.Exists(PlatformRoles, role => principal.IsInRole(role));
}

/// <summary>
/// Names of the authorization policies registered in <c>Program.cs</c>. Constants rather than literals
/// so the boundary a controller declares and the boundary the host registers cannot silently drift
/// apart on a typo.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// The tenant workspace boundary: an authenticated caller that is neither a client-portal login
    /// nor platform staff — see <see cref="AppRoles.IsTenantWorkspacePrincipal"/> for the rule and why
    /// it is stated as a denial. Carried by <c>TenantAwareControllerBase</c>, so every tenant
    /// controller inherits it and a new controller is inside the boundary by default: it must be
    /// opted out of, not remembered (EC-001, EC-020).
    /// </summary>
    public const string TenantStaff = "TenantStaff";

    /// <summary>The client-portal boundary: a caller holding <see cref="AppRoles.PortalClient"/>.</summary>
    public const string PortalAccess = "PortalAccess";
}
