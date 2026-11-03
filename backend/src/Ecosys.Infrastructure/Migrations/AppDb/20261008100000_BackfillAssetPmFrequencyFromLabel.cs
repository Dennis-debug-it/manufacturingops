using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <summary>
    /// Assets saved before the structured frequency existed only carry a free-text label such as
    /// "Monthly", which the PM scheduler cannot use. Convert the recognisable labels (the same set
    /// the asset API parses) into value + unit, and derive the next PM date from the last one.
    /// </summary>
    public partial class BackfillAssetPmFrequencyFromLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH named(label, value, unit) AS (VALUES
                    ('daily', 1, 'days'),
                    ('weekly', 1, 'weeks'),
                    ('fortnightly', 2, 'weeks'),
                    ('biweekly', 2, 'weeks'),
                    ('monthly', 1, 'months'),
                    ('bimonthly', 2, 'months'),
                    ('quarterly', 1, 'quarters'),
                    ('semiannual', 6, 'months'),
                    ('semiannually', 6, 'months'),
                    ('biannual', 6, 'months'),
                    ('biannually', 6, 'months'),
                    ('halfyearly', 6, 'months'),
                    ('sixmonthly', 6, 'months'),
                    ('annual', 1, 'years'),
                    ('annually', 1, 'years'),
                    ('yearly', 1, 'years'))
                UPDATE assets a
                SET ""PmFrequencyValue"" = n.value, ""PmFrequencyUnit"" = n.unit
                FROM named n
                WHERE a.""PmFrequencyValue"" IS NULL
                  AND a.""PmFrequencyUnit"" IS NULL
                  AND lower(regexp_replace(a.""RecommendedPmFrequency"", '[^a-zA-Z]', '', 'g')) = n.label;");

            // ""3 months"", ""Every 2 weeks"", ""1-year"" ...
            migrationBuilder.Sql(@"
                UPDATE assets a
                SET ""PmFrequencyValue"" = m.parts[1]::integer,
                    ""PmFrequencyUnit"" = CASE lower(m.parts[2])
                        WHEN 'day' THEN 'days' WHEN 'week' THEN 'weeks' WHEN 'month' THEN 'months'
                        WHEN 'quarter' THEN 'quarters' ELSE 'years' END
                FROM (
                    SELECT ""Id"", regexp_match(btrim(""RecommendedPmFrequency""), '^(?:every\s+)?(\d{1,4})\s*-?\s*(day|week|month|quarter|year)s?$', 'i') AS parts
                    FROM assets
                    WHERE ""PmFrequencyValue"" IS NULL AND ""PmFrequencyUnit"" IS NULL AND ""RecommendedPmFrequency"" IS NOT NULL
                ) m
                WHERE a.""Id"" = m.""Id"" AND m.parts IS NOT NULL AND m.parts[1]::integer > 0;");

            migrationBuilder.Sql(@"
                UPDATE assets
                SET ""NextPmDate"" = ""LastPmDate"" + CASE ""PmFrequencyUnit""
                    WHEN 'days' THEN make_interval(days => ""PmFrequencyValue"")
                    WHEN 'weeks' THEN make_interval(weeks => ""PmFrequencyValue"")
                    WHEN 'months' THEN make_interval(months => ""PmFrequencyValue"")
                    WHEN 'quarters' THEN make_interval(months => ""PmFrequencyValue"" * 3)
                    ELSE make_interval(years => ""PmFrequencyValue"") END
                WHERE ""NextPmDate"" IS NULL AND ""LastPmDate"" IS NOT NULL
                  AND ""PmFrequencyValue"" IS NOT NULL AND ""PmFrequencyUnit"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill only; the converted values are valid data and are left in place.
        }
    }
}
