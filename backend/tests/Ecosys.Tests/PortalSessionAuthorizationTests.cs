using System.Security.Claims;
using Ecosys.Api.Configuration;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class PortalSessionAuthorizationTests
{
    [Theory]
    [InlineData("active", true)]
    [InlineData("disabled-contact", false)]
    [InlineData("disabled-client", false)]
    [InlineData("disabled-tenant", false)]
    [InlineData("foreign-tenant", false)]
    [InlineData("foreign-client", false)]
    [InlineData("foreign-contact", false)]
    [InlineData("missing-client", false)]
    [InlineData("malformed-tenant", false)]
    public async Task PortalPolicyRequiresCurrentActiveIdentity(string condition, bool expected)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = new Tenant { IsActive = condition != "disabled-tenant" };
        var client = new Client { TenantId = tenant.Id, IsActive = condition != "disabled-client" };
        var contact = new ClientPortalUser
        {
            TenantId = tenant.Id, Tenant = tenant, ClientId = client.Id, Client = client,
            IsActive = condition != "disabled-contact"
        };
        db.ClientPortalUsers.Add(contact);
        await db.SaveChangesAsync();
        var claims = new List<Claim>
        {
            new(TenantClaimTypes.PortalUserId, condition == "foreign-contact" ? Guid.NewGuid().ToString() : contact.Id.ToString()),
            new(TenantClaimTypes.TenantId, condition == "malformed-tenant" ? "invalid" :
                condition == "foreign-tenant" ? Guid.NewGuid().ToString() : tenant.Id.ToString())
        };
        if (condition != "missing-client")
            claims.Add(new Claim(TenantClaimTypes.ClientId,
                condition == "foreign-client" ? Guid.NewGuid().ToString() : client.Id.ToString()));
        var requirement = new PortalSessionRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await new PortalSessionAuthorizationHandler(db).HandleAsync(context);
        Assert.Equal(expected, context.HasSucceeded);

        if (expected)
        {
            contact.IsActive = false;
            await db.SaveChangesAsync();
            var afterRevocation = new AuthorizationHandlerContext([requirement], principal, null);
            await new PortalSessionAuthorizationHandler(db).HandleAsync(afterRevocation);
            Assert.False(afterRevocation.HasSucceeded);
        }
    }
}
