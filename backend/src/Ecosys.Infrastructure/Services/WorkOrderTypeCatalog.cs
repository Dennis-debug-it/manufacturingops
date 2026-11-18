using Ecosys.Domain.Enums;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

public static class WorkOrderTypes
{
    public const string NewInstallation = "NewInstallation";
    public const string PreventiveMaintenance = "PreventiveMaintenance";
    public const string CorrectiveMaintenance = "CorrectiveMaintenance";
    public const string InspectionAssessment = "InspectionAssessment";

    public static readonly IReadOnlyCollection<string> All =
    [
        NewInstallation,
        PreventiveMaintenance,
        CorrectiveMaintenance,
        InspectionAssessment
    ];

    public static WorkOrderType Parse(string? workType, WorkOrderType? fallback = null)
    {
        var normalized = string.IsNullOrWhiteSpace(workType)
            ? fallback.HasValue ? ToValue(fallback.Value) : null
            : workType.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException("Work type is required.");
        }

        // Factory jobs store their own work types (FactoryWorkTypes); they map onto the four older ones so the screens
        // and endpoints that still speak those (the technician app among them) can read factory jobs.
        return normalized switch
        {
            NewInstallation or "New Installation" or "Modification" => WorkOrderType.NewInstallation,
            PreventiveMaintenance or "Preventive Maintenance" or "Preventive" => WorkOrderType.PreventiveMaintenance,
            CorrectiveMaintenance or "Corrective Maintenance" or "BreakdownCorrective" or "Breakdown / Corrective Maintenance" or "Corrective" or "Safety" => WorkOrderType.CorrectiveMaintenance,
            InspectionAssessment or "Inspection / Assessment" or "Inspection" or "Statutory" => WorkOrderType.InspectionAssessment,
            _ => throw new BusinessRuleException("Invalid work type.")
        };
    }

    public static string Normalize(string? workType, string? fallback = null)
        => ToValue(Parse(workType, string.IsNullOrWhiteSpace(fallback) ? null : Parse(fallback)));

    public static bool IsPreventiveMaintenanceType(string? workType) =>
        string.Equals(Normalize(workType, CorrectiveMaintenance), PreventiveMaintenance, StringComparison.OrdinalIgnoreCase);

    public static bool IsPreventiveMaintenanceType(WorkOrderType workType) =>
        workType == WorkOrderType.PreventiveMaintenance;

    public static string ToValue(WorkOrderType workType) => workType switch
    {
        WorkOrderType.NewInstallation => NewInstallation,
        WorkOrderType.PreventiveMaintenance => PreventiveMaintenance,
        WorkOrderType.CorrectiveMaintenance => CorrectiveMaintenance,
        WorkOrderType.InspectionAssessment => InspectionAssessment,
        _ => throw new BusinessRuleException("Invalid work type.")
    };
}
