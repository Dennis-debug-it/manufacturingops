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
    public const string User = "User";

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
        User
    ];

    public static readonly string[] AllRoles =
    [
        .. PlatformRoles,
        .. TenantRoles
    ];
}
