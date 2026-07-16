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
    public const string Finance = "Finance";
    public const string Auditor = "Auditor";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Dispatcher = "Dispatcher";
    public const string Technician = "Technician";
    public const string FinanceUser = "finance";
    public const string Viewer = "Viewer";

    /// <summary>Legacy tenant role. Treated as <see cref="Viewer"/> when resolving permissions.</summary>
    public const string User = "User";

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
        FinanceUser,
        Viewer
    ];

    public static readonly string[] AllRoles =
    [
        .. PlatformRoles,
        .. TenantRoles
    ];
}
