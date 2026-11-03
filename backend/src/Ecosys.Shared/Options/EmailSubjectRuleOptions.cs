namespace Ecosys.Shared.Options;

public sealed record EmailSubjectRuleOptions(
    string SubjectPrefix,
    string? SubjectSuffix,
    bool IncludeEnvironmentInSubject,
    string EnvironmentLabel,
    bool IncludeTenantNameInSubject,
    bool EnableEventSubjectTags)
{
    public static EmailSubjectRuleOptions Default { get; } = new(
        "[Ecosys]",
        null,
        false,
        "Production",
        false,
        true);
}
