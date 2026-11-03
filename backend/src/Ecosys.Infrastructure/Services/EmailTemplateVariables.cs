namespace Ecosys.Infrastructure.Services;

public static class EmailTemplateVariables
{
    public static Dictionary<string, string?> MergeTemplateVariables(params IEnumerable<KeyValuePair<string, string?>>[] sources)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                result[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        return result;
    }

    public static Dictionary<string, string?> WithRecipientAliases(string? fullName, params IEnumerable<KeyValuePair<string, string?>>[] sources)
    {
        var allSources = new List<IEnumerable<KeyValuePair<string, string?>>>
        {
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fullName"] = fullName,
                ["recipientFullName"] = fullName,
            }
        };
        allSources.AddRange(sources);
        return MergeTemplateVariables(allSources.ToArray());
    }

    public static Dictionary<string, string?> WithRecipientAndActorAliases(
        string? recipientFullName,
        string? actorFullName,
        params IEnumerable<KeyValuePair<string, string?>>[] sources)
    {
        var allSources = new List<IEnumerable<KeyValuePair<string, string?>>>
        {
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fullName"] = recipientFullName,
                ["recipientFullName"] = recipientFullName,
                ["actorFullName"] = actorFullName,
            }
        };
        allSources.AddRange(sources);
        return MergeTemplateVariables(allSources.ToArray());
    }
}
