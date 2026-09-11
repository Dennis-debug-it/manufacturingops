BEGIN;
-- August already created tenants.TimeZoneId. September cannot run as written because
-- it attempts to add that column again; the reconciliation migration replaces its effects.
DO $verify$
BEGIN
  IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.tenants') IS NULL THEN
    RAISE EXCEPTION 'Expected EF history and tenants tables are missing';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='tenants' AND column_name='TimeZoneId') THEN
    RAISE EXCEPTION 'August TimeZoneId prerequisite is missing';
  END IF;
END $verify$;
INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
SELECT '20260905061718_ServiceOpsProvisioningIdentityInvitations','10.0.7'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"='20260905061718_ServiceOpsProvisioningIdentityInvitations');
COMMIT;
