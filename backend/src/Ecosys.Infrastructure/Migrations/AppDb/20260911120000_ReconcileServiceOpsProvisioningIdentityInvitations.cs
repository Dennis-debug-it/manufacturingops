using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb;

[Migration("20260911120000_ReconcileServiceOpsProvisioningIdentityInvitations")]
public partial class ReconcileServiceOpsProvisioningIdentityInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $schema$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'tenants' AND column_name = 'Currency') THEN
                    ALTER TABLE tenants ADD COLUMN "Currency" varchar(3) NOT NULL DEFAULT '';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'password_reset_tokens' AND column_name = 'Purpose') THEN
                    ALTER TABLE password_reset_tokens ADD COLUMN "Purpose" varchar(30) NOT NULL DEFAULT 'PasswordReset';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'password_reset_tokens' AND column_name = 'RevokedAt') THEN
                    ALTER TABLE password_reset_tokens ADD COLUMN "RevokedAt" timestamptz NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'password_reset_tokens' AND column_name = 'SupersededAt') THEN
                    ALTER TABLE password_reset_tokens ADD COLUMN "SupersededAt" timestamptz NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'email_settings' AND column_name = 'EmailSignature') THEN
                    ALTER TABLE email_settings ADD COLUMN "EmailSignature" varchar(4000) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'email_settings' AND column_name = 'InboundEnabled') THEN
                    ALTER TABLE email_settings ADD COLUMN "InboundEnabled" boolean NOT NULL DEFAULT false;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'email_settings' AND column_name = 'ManagedEmailAddress') THEN
                    ALTER TABLE email_settings ADD COLUMN "ManagedEmailAddress" varchar(256) NULL;
                END IF;
            END $schema$;

            UPDATE tenants SET "TimeZoneId" = CASE lower(trim("Country"))
                WHEN 'kenya' THEN 'Africa/Nairobi' WHEN 'ke' THEN 'Africa/Nairobi'
                WHEN 'uganda' THEN 'Africa/Kampala' WHEN 'ug' THEN 'Africa/Kampala'
                WHEN 'tanzania' THEN 'Africa/Dar_es_Salaam' WHEN 'tz' THEN 'Africa/Dar_es_Salaam'
                WHEN 'rwanda' THEN 'Africa/Kigali' WHEN 'rw' THEN 'Africa/Kigali'
                WHEN 'nigeria' THEN 'Africa/Lagos' WHEN 'ng' THEN 'Africa/Lagos'
                WHEN 'south africa' THEN 'Africa/Johannesburg' WHEN 'za' THEN 'Africa/Johannesburg'
                WHEN 'united kingdom' THEN 'Europe/London' WHEN 'gb' THEN 'Europe/London' WHEN 'uk' THEN 'Europe/London'
                ELSE 'Etc/UTC' END
            WHERE "TimeZoneId" IS NULL OR btrim("TimeZoneId") = '';
            UPDATE tenants SET "Currency" = CASE lower(trim("Country"))
                WHEN 'kenya' THEN 'KES' WHEN 'ke' THEN 'KES' WHEN 'uganda' THEN 'UGX' WHEN 'ug' THEN 'UGX'
                WHEN 'tanzania' THEN 'TZS' WHEN 'tz' THEN 'TZS' WHEN 'rwanda' THEN 'RWF' WHEN 'rw' THEN 'RWF'
                WHEN 'nigeria' THEN 'NGN' WHEN 'ng' THEN 'NGN' WHEN 'south africa' THEN 'ZAR' WHEN 'za' THEN 'ZAR'
                WHEN 'united kingdom' THEN 'GBP' WHEN 'gb' THEN 'GBP' WHEN 'uk' THEN 'GBP' ELSE 'USD' END
            WHERE "Currency" IS NULL OR btrim("Currency") = '';
            ALTER TABLE tenants ALTER COLUMN "TimeZoneId" TYPE varchar(100) USING "TimeZoneId"::varchar(100);
            ALTER TABLE tenants ALTER COLUMN "TimeZoneId" SET NOT NULL;
            ALTER TABLE tenants ALTER COLUMN "Currency" SET NOT NULL;

            UPDATE license_plans SET "IsActive" = false
            WHERE "Id" IN ('33fe16d8-5b55-45aa-89e2-1af0f2e7367d','4df8af7d-8514-45e7-ad42-f5d7b745fe71','7e2bc113-bc77-4206-8c53-4696402f69c0','d220965a-38b4-45d0-9870-a8e45627e21a'::uuid);
            INSERT INTO license_plans ("Id","AdvancedReports","AnnualPrice","ClientPortal","CreatedAt","DisplayName","EmailIngestion","IsActive","MaxAssets","MaxBranches","MaxUsers","ModulesIncluded","MonitoringIntegration","MonthlyPrice","MonthlyWorkOrders","PlanCode","UpdatedAt")
            SELECT v."Id"::uuid,v."AdvancedReports",0,v."ClientPortal",TIMESTAMPTZ '2026-09-05',v."DisplayName",v."EmailIngestion",true,NULL,NULL,NULL,v."ModulesIncluded",v."MonitoringIntegration",0,NULL,v."PlanCode",NULL
            FROM (VALUES
              ('55555555-5555-5555-5555-555555555551',true,true,'Ecosys ServiceOps Operations',true,'Core,operations,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal',true,'operations'),
              ('55555555-5555-5555-5555-555555555552',true,false,'Ecosys ServiceOps Finance',false,'Core,finance,AdvancedReports',false,'finance'),
              ('55555555-5555-5555-5555-555555555553',true,true,'Ecosys ServiceOps Suite',true,'Core,operations,finance,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal',true,'suite')
            ) v("Id","AdvancedReports","ClientPortal","DisplayName","EmailIngestion","ModulesIncluded","MonitoringIntegration","PlanCode")
            WHERE NOT EXISTS (SELECT 1 FROM license_plans p WHERE p."PlanCode" = v."PlanCode");
            UPDATE password_reset_tokens p SET "Purpose" = 'Invitation' FROM users u WHERE u."Id"=p."UserId" AND u."MustChangePassword"=true AND p."UsedAt" IS NULL;
            """);

        migrationBuilder.Sql("""
            DO $email$
            DECLARE t record; candidate text; base_slug text; address text; suffix integer;
            BEGIN
              FOR t IN SELECT * FROM tenants WHERE "Id" <> '00000000-0000-0000-0000-000000000001'::uuid ORDER BY "CreatedAt","Id" LOOP
                base_slug := trim(both '-' from regexp_replace(lower(trim(coalesce(nullif(t."Slug",''),t."CompanyName"))), '[^a-z0-9]+','-','g'));
                base_slug := trim(trailing '-' from left(base_slug,63)); IF length(base_slug)<3 THEN base_slug:='tenant-'||left(replace(t."Id"::text,'-',''),12); END IF;
                IF base_slug=ANY(ARRAY['admin','api','app','mail','support','help','serviceops','ecosys','system','root','postmaster']) THEN base_slug:=base_slug||'-tenant'; END IF;
                candidate:=base_slug; suffix:=1;
                LOOP address:=candidate||'@serviceops.ecosysdigital.co.ke'; EXIT WHEN NOT EXISTS(SELECT 1 FROM tenants x WHERE x."Id"<>t."Id" AND lower(x."Slug")=candidate) AND NOT EXISTS(SELECT 1 FROM email_intake_settings x WHERE x."TenantId"<>t."Id" AND lower(x."IntakeEmailAddress")=address) AND NOT EXISTS(SELECT 1 FROM email_settings x WHERE x."TenantId"<>t."Id" AND (lower(x."ManagedEmailAddress")=address OR lower(x."SenderAddress")=address)); suffix:=suffix+1; candidate:=trim(trailing '-' from left(base_slug,63-length(suffix::text)-1))||'-'||suffix; END LOOP;
                INSERT INTO email_settings ("Id","TenantId","CreatedAt","UsePlatformDefaults","OverrideSmtpSettings","IsEnabled","Provider","Host","Port","UseSsl","SenderName","SenderAddress","IsConfigured","InboundEnabled","ManagedEmailAddress") VALUES (md5(t."Id"::text||':managed-email')::uuid,t."Id",now(),true,false,true,'Smtp','',25,false,left(t."CompanyName",150),address,false,true,address) ON CONFLICT ("TenantId") DO UPDATE SET "InboundEnabled"=true,"ManagedEmailAddress"=COALESCE(NULLIF(email_settings."ManagedEmailAddress",''),excluded."ManagedEmailAddress");
              END LOOP;
            END $email$;
            """);
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_email_settings_ManagedEmailAddress\" ON email_settings (\"ManagedEmailAddress\") WHERE \"ManagedEmailAddress\" IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
