using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RepairScheduledReportsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('public.scheduled_reports') IS NULL THEN
                        CREATE TABLE public.scheduled_reports
                        (
                            "Id" uuid NOT NULL,
                            "TenantId" uuid NOT NULL,
                            "Name" character varying(200) NOT NULL,
                            "ReportType" character varying(50) NOT NULL,
                            "Frequency" character varying(20) NOT NULL,
                            "DayOfMonth" integer NULL,
                            "DayOfWeek" character varying(10) NULL,
                            "TimeOfDay" character varying(5) NOT NULL DEFAULT '08:00',
                            "DateRangePreset" character varying(30) NOT NULL DEFAULT 'LastMonth',
                            "GroupBy" character varying(30) NULL,
                            "ClientId" character varying(100) NULL,
                            "Recipients" character varying(2000) NOT NULL,
                            "IsActive" boolean NOT NULL DEFAULT TRUE,
                            "LastRunAt" timestamp with time zone NULL,
                            "NextRunAt" timestamp with time zone NULL,
                            "LastRunStatus" character varying(20) NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NULL,
                            CONSTRAINT "PK_scheduled_reports" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_scheduled_reports_tenants_TenantId"
                                FOREIGN KEY ("TenantId") REFERENCES public.tenants ("Id") ON DELETE CASCADE
                        );
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_scheduled_reports_TenantId"
                ON public.scheduled_reports ("TenantId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_scheduled_reports_TenantId_IsActive"
                ON public.scheduled_reports ("TenantId", "IsActive");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
