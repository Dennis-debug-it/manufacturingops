using System.Security.Claims;
using System.Text.Json;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class Phase2GovernanceTests
{
    [Fact]
    public void EmailNotificationRegistry_ExposesOnlyApprovedPhaseTwoHooksAsActive()
    {
        var registry = new EmailNotificationRegistry();

        var activeHooks = registry.List()
            .Where(item => string.Equals(item.DispatchStatus, "Active", StringComparison.Ordinal))
            .Select(item => item.EventKey)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "action-item.assigned",
                "action-item.due",
                "activity.cancelled",
                "activity.minutes-published",
                "activity.reminder",
                "activity.scheduled",
                "activity.updated",
                "asset.meter-out-of-range",
                "asset.warranty-expiring",
                "auth.password-reset.requested",
                "auth.signup.verify-email",
                "compliance.due",
                "compliance.overdue",
                "crm.deal.email",
                "crm.deal.stalled",
                "crm.follow-up.due",
                "crm.quote.awaiting-answer",
                "crm.tender.deadline",
                "crm.weekly-summary",
                "csat.survey",
                "email-intake.email-intake-received",
                "email-intake.email-intake-reply-added",
                "email-intake.email-intake-unrecognised",
                "factory-check.missed",
                "factory-job.reported",
                "factory-window.carryover",
                "inventory.low-stock",
                "inventory.request-decided",
                "inventory.request-dispatched",
                "inventory.request-raised",
                "invoice.sent",
                "platform.invoice.issued",
                "platform.lead.received",
                "platform.payment.received",
                "platform.subscription.extended",
                "platform.subscription.grace-warning",
                "platform.subscription.suspended",
                "pm.contract-lapsed",
                "project.end-date-approaching",
                "project.member-added",
                "project.milestone-due",
                "project.milestone-overdue",
                "project.muster-missing",
                "project.retention-due",
                "project.task-assigned",
                "project.task-due",
                "project.valuation-certified",
                "project.variation-decided",
                "project.variation-submitted",
                "quotation.sent",
                "service-report.sent",
                "tenant.onboarding",
                "user.password-reset.admin",
                "users.invited",
                "work-order.assigned",
                "work-order.cancelled",
                "work-order.closed",
                "work-order.completed",
                "work-order.on-hold",
                "work-order.overdue",
                "work-order.started",
            ],
            activeHooks);

        Assert.DoesNotContain(registry.List(), item =>
            string.Equals(item.EventKey, "pm.due", StringComparison.Ordinal) &&
            string.Equals(item.DispatchStatus, "Active", StringComparison.Ordinal));
        Assert.DoesNotContain(registry.List(), item =>
            string.Equals(item.EventKey, "pm.overdue", StringComparison.Ordinal) &&
            string.Equals(item.DispatchStatus, "Active", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PasswordChangeEnforcementMiddleware_BlocksRestrictedToken_OnOtherRoutes()
    {
        var nextCalled = false;
        var middleware = new PasswordChangeEnforcementMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<PasswordChangeEnforcementMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/workorders";
        context.Response.Body = new MemoryStream();
        context.User = BuildRestrictedPrincipal();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var payload = await JsonSerializer.DeserializeAsync<PasswordChangeRequiredPayload>(
            context.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        Assert.NotNull(payload);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", payload.ErrorCode);
        Assert.Equal("You must change your password before continuing.", payload.Message);
    }

    [Fact]
    public async Task PasswordChangeEnforcementMiddleware_AllowsRestrictedToken_OnChangePasswordRoute()
    {
        var nextCalled = false;
        var middleware = new PasswordChangeEnforcementMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<PasswordChangeEnforcementMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/change-password";
        context.Response.Body = new MemoryStream();
        context.User = BuildRestrictedPrincipal();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static ClaimsPrincipal BuildRestrictedPrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(TenantClaimTypes.Scope, "password_change_only"),
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private sealed class PasswordChangeRequiredPayload
    {
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
    }
}
