using System.Security.Claims;
using Ecosys.Infrastructure.Security;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;

namespace Ecosys.Tests;

/// <summary>
/// Platform staff and tenant users share one Users table, and <see cref="ITenantContext.IsSuperAdmin"/>
/// feeds the global tenant query filter — so a tenant user mistaken for platform staff would mean
/// cross-tenant reads. Platform finance was once stored as "Finance" against the tenant's "finance",
/// one careless comparison away from exactly that. These tests pin the boundary where it would break.
/// </summary>
public sealed class RoleBoundaryTests
{
    [Fact]
    public void TenantFinanceUser_DoesNotHoldThePlatformFinanceRole()
    {
        // Role claim matching happens to be case-sensitive today, but the separation no longer leans
        // on that: the two role names are simply different words.
        var principal = PrincipalWith(AppRoles.FinanceUser, Guid.NewGuid());

        Assert.True(principal.IsInRole(AppRoles.FinanceUser));
        Assert.False(principal.IsInRole(AppRoles.Finance));
    }

    [Fact]
    public void TenantFinanceUser_IsNotSuperAdmin()
    {
        var context = ContextFor(AppRoles.FinanceUser, Guid.NewGuid());

        Assert.False(context.IsSuperAdmin);
    }

    [Fact]
    public void PlatformFinanceUser_IsSuperAdmin()
    {
        var context = ContextFor(AppRoles.Finance, PlatformConstants.RootTenantId);

        Assert.True(context.IsSuperAdmin);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Dispatcher")]
    [InlineData("Technician")]
    [InlineData("finance")]
    [InlineData("Viewer")]
    [InlineData("User")]
    public void NoTenantRole_GrantsSuperAdmin(string tenantRole)
    {
        Assert.False(ContextFor(tenantRole, Guid.NewGuid()).IsSuperAdmin);
    }

    [Fact]
    public void RoleClassification_DoesNotConfuseTheTwoFinanceRoles()
    {
        Assert.True(AppRoles.IsPlatformRole(AppRoles.Finance));
        Assert.False(AppRoles.IsPlatformRole(AppRoles.FinanceUser));

        Assert.True(AppRoles.IsTenantRole(AppRoles.FinanceUser));
        Assert.False(AppRoles.IsTenantRole(AppRoles.Finance));
    }

    [Fact]
    public void PlatformAndTenantRoleListsDoNotOverlap_EvenIgnoringCase()
    {
        // The invariant that makes every other classifier safe regardless of how it compares. Adding
        // a role to one list whose name matches the other list — in any casing — breaks here first.
        var shared = AppRoles.PlatformRoles.Intersect(AppRoles.TenantRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Empty(shared);
    }

    [Fact]
    public void PlatformFinanceRole_IsNotAMereCaseVariantOfTheTenantRole()
    {
        Assert.NotEqual(AppRoles.Finance.ToLowerInvariant(), AppRoles.FinanceUser.ToLowerInvariant());
    }

    private static ITenantContext ContextFor(string role, Guid tenantId)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = PrincipalWith(role, tenantId) }
        };

        return (ITenantContext)Activator.CreateInstance(
            typeof(HttpTenantContext),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            binder: null,
            args: [accessor],
            culture: null)!;
    }

    private static ClaimsPrincipal PrincipalWith(string role, Guid tenantId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, role),
                new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
                new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString())
            ],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
}
