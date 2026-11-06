using System.ComponentModel.DataAnnotations;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Route("api/reports/scheduled")]
[Authorize]
public sealed class ScheduledReportsController(
    IScheduledReportService scheduledReportService,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // GET /api/reports/scheduled
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var reports = await scheduledReportService.ListAsync(TenantId, ct);
        return Ok(reports.Select(MapToDto).ToList());
    }

    // POST /api/reports/scheduled
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScheduledReportInputDto dto, CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var request = MapToRequest(dto);
        var report = await scheduledReportService.CreateAsync(TenantId, request, ct);
        return CreatedAtAction(nameof(List), new { }, MapToDto(report));
    }

    // PUT /api/reports/scheduled/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ScheduledReportInputDto dto, CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var request = MapToRequest(dto);
            var report = await scheduledReportService.UpdateAsync(TenantId, id, request, ct);
            return Ok(MapToDto(report));
        }
        catch (KeyNotFoundException)
        {
            throw new NotFoundException("Scheduled report not found.");
        }
    }

    // DELETE /api/reports/scheduled/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        try
        {
            await scheduledReportService.DeleteAsync(TenantId, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            throw new NotFoundException("Scheduled report not found.");
        }
    }

    // PUT /api/reports/scheduled/{id}/toggle
    [HttpPut("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        try
        {
            var report = await scheduledReportService.ToggleAsync(TenantId, id, ct);
            return Ok(MapToDto(report));
        }
        catch (KeyNotFoundException)
        {
            throw new NotFoundException("Scheduled report not found.");
        }
    }

    // POST /api/reports/scheduled/{id}/run-now
    [HttpPost("{id:guid}/run-now")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        try
        {
            var report = await scheduledReportService.RunNowAsync(TenantId, id, ct);
            return Ok(MapToDto(report));
        }
        catch (KeyNotFoundException)
        {
            throw new NotFoundException("Scheduled report not found.");
        }
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static CreateScheduledReportRequest MapToRequest(ScheduledReportInputDto dto) => new(
        dto.Name,
        dto.ReportType,
        dto.Frequency,
        dto.DayOfMonth,
        dto.DayOfWeek,
        dto.TimeOfDay ?? "08:00",
        dto.DateRangePreset ?? "LastMonth",
        dto.GroupBy,
        dto.ClientId,
        dto.Recipients,
        dto.OrgNodeId);

    private static ScheduledReportDto MapToDto(Domain.Entities.ScheduledReport r) => new(
        r.Id,
        r.Name,
        r.ReportType,
        r.Frequency,
        r.DayOfMonth,
        r.DayOfWeek,
        r.TimeOfDay,
        r.DateRangePreset,
        r.GroupBy,
        r.ClientId,
        r.Recipients,
        r.IsActive,
        r.LastRunAt,
        r.NextRunAt,
        r.LastRunStatus,
        r.CreatedAt,
        r.UpdatedAt);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ScheduledReportInputDto(
    [Required, MaxLength(200)] string Name,
    [Required, AllowedValues("Revenue", "Aging", "ClientStatement", "WorkOrderSummary", "Invoices", "FactoryKpis")] string ReportType,
    [Required, AllowedValues("Daily", "Weekly", "Monthly", "Quarterly")] string Frequency,
    [Range(1, 28)] int? DayOfMonth,
    [AllowedValues("Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday", null)] string? DayOfWeek,
    string? TimeOfDay,
    [AllowedValues("LastWeek", "LastMonth", "LastQuarter", "LastYear", "YTD", null)] string? DateRangePreset,
    string? GroupBy,
    string? ClientId,
    [Required] string Recipients,
    Guid? OrgNodeId = null);

public sealed record ScheduledReportDto(
    Guid Id,
    string Name,
    string ReportType,
    string Frequency,
    int? DayOfMonth,
    string? DayOfWeek,
    string TimeOfDay,
    string DateRangePreset,
    string? GroupBy,
    string? ClientId,
    string Recipients,
    bool IsActive,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    string? LastRunStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
