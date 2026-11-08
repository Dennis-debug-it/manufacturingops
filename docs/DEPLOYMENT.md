# Ecosys ServiceOps — Production Deployment Guide

Last updated: 2026-08-26.

The shipped deployment is the Docker Compose stack at the repository root: PostgreSQL, the
.NET API, and an nginx container serving the built SPA and proxying `/api`, `/hubs` and
`/files` to the API on the same origin.

> **Deploying to a bare VPS?** This guide assumes an already-provisioned Docker host. For
> taking a fresh Ubuntu/Debian server from nothing to a running, TLS-terminated deployment
> (firewall, Docker install, reverse proxy, first `up`), start with
> [VPS_SETUP.md](VPS_SETUP.md), which cross-references the sections here.

> **Upgrading a deployment that already exists?** Read
> [§7 Upgrading an existing deployment](#7-upgrading-an-existing-deployment) **before**
> pulling. The API container now runs as a non-root user, and volumes created by an older
> build need a one-time `chown` or the API cannot write attachments.

---

## 1. Environment variables

Copy `.env.example` to `.env` and fill it in. The API validates the security-critical
values at startup and **refuses to boot** on missing, too-short, or known-default values.
Compose refuses to start at all if a `:?`-marked variable is absent. Never put production
values in `appsettings.json`.

### Required

| Variable | Purpose | Notes |
|---|---|---|
| `POSTGRES_PASSWORD` | Database password | Strong random value |
| `ECOSYS_PUBLIC_URL` | **The origin your users reach this deployment on** | Scheme + host, no trailing slash. See below — getting this wrong silently breaks emailed links |
| `ECOSYS_JWT_SIGNING_KEY` | JWT HMAC key | ≥ 32 chars; generate fresh (§2) |
| `ECOSYS_PLATFORM_ADMIN_EMAIL` | Bootstrap platform admin | Not a known default |
| `ECOSYS_PLATFORM_ADMIN_PASSWORD` | Bootstrap admin password | ≥ 12 chars, not a known default |
| `ECOSYS_CORS_ALLOWED_ORIGINS` | Comma-separated frontend origins | **CORS denies everything if unset** |
| `ECOSYS_KNOWN_PROXIES` | Comma-separated proxy IPs or CIDR blocks | Required for this stack — see below |

`ECOSYS_PUBLIC_URL` drives three separate things, which is why it has no useful default:

* `VITE_API_BASE_URL`, baked into the frontend bundle **at image build time** — changing it
  later requires `docker compose build web`, not just a restart;
* `Storage__BaseUrl`, the prefix on attachment download links;
* `App__PublicUrl`, the origin in every password-reset, account-setup and technician-ETA
  link the API emails out.

`appsettings.json` carries only a `http://localhost` placeholder for `App:PublicUrl`, so a
deployment that does not set `ECOSYS_PUBLIC_URL` would email links pointing at the wrong
host. Compose therefore treats it as required and will not start without it.

`ECOSYS_KNOWN_PROXIES` populates the forwarded-headers trust list. **When it is unset,
`X-Forwarded-For` is not trusted**, which in this stack means every request appears to come
from the nginx container: audit logs record the proxy's address instead of the user's, and
all callers share a single auth rate-limit bucket, so one noisy client can lock everyone
out of login. The nginx container's address is assigned by Docker and changes, so give it
the compose network's **subnet**, not a single IP:

```bash
docker network inspect ecosys_default -f '{{range .IPAM.Config}}{{.Subnet}}{{end}}'
# e.g. 172.18.0.0/16   ->   ECOSYS_KNOWN_PROXIES=172.18.0.0/16
```

If you additionally terminate TLS on a proxy in front of the `web` container, add that
proxy's address too. Unparseable entries are logged and skipped at startup — check the API
log after changing this.

### Optional

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_USER` / `POSTGRES_DB` | `ecosys` | Database role and name |
| `WEB_PORT` | `8080` | Host port the `web` container publishes |
| `ECOSYS_PLATFORM_ADMIN_FULL_NAME` | `Platform Admin` | Display name |
| `ECOSYS_DEFAULT_TIME_ZONE` | `Africa/Nairobi` | IANA zone used when neither tenant nor platform sets one |
| `ECOSYS_CSP_CONNECT_SRC` | *(empty)* | Extra `connect-src` origins for the SPA's CSP — §4 |
| `ECOSYS_BACKUP_DIR` | `./backups` | Host directory for database dumps — §5 |
| `ECOSYS_BACKUP_RETENTION_DAYS` | `14` | Prune dumps older than this; `0` keeps everything |
| `ECOSYS_SMTP_HOST` / `_PORT` / `_USE_SSL` / `_USERNAME` / `_PASSWORD` | *(unset)* | Outbound email. With no host configured the app runs but sends nothing — including password-reset mail |
| `ECOSYS_SELF_SIGNUP_ENABLED` | `true` | The public **Start free trial** page (`/signup`). A company enters its details, confirms its email from the link, and lands in a 14-day Suite trial workspace; each sign-up appears under Platform → Leads as "Converted to Workspace". Needs SMTP; without it the page tells people to request a demo. `false` turns it off |
| `ECOSYS_MQTT_ENABLED` | `false` | Subscribe to an MQTT broker for sensor readings (Settings → Sensors). The HTTP feed, `POST /api/sensors/readings` with an `X-Sensor-Key` header, works without it |
| `ECOSYS_MQTT_HOST` / `_PORT` / `_USE_TLS` / `_USERNAME` / `_PASSWORD` | *(unset)* / `1883` | The broker. Only one API instance subscribes at a time (the background-work lease), so readings are stored once |
| `ECOSYS_MQTT_TOPIC_PREFIX` | `ecosys/sensors` | Readings arrive on `{prefix}/{gatewayId}/{tag}`. Use the broker's ACLs to let each gateway publish only under its own id; a gateway that does not trust the broker must also put its key in each message |

`ASPNETCORE_ENVIRONMENT` is pinned to `Production` by the compose file and the Dockerfile.
That is what disables Swagger and test seeding and enables HSTS; do not override it.

## 2. Generate the production JWT signing key

```bash
openssl rand -base64 48
```

Never reuse a key that has appeared in a repo, build output, or chat. The old dev key
(`EcosysDevelopmentSigningKey-…`) is denylisted at startup. **Rotating the key invalidates
all active logins** — rotate during a maintenance window.

## 3. Build and run

```bash
cp .env.example .env      # then fill it in
docker compose build
docker compose up -d
docker compose logs -f api
```

Database migrations run automatically at API startup; the process aborts if they fail, so
a container that reaches "ready" has a migrated schema.

The stack defines three named volumes. **All persistent state lives in them:**

| Volume | Mounted at | Holds |
|---|---|---|
| `pgdata` | `/var/lib/postgresql/data` | The entire database |
| `uploads` | `/app/storage/uploads` | Work-order attachments and photos |
| `tenant-logos` | `/app/wwwroot/uploads/logos` | Tenant branding logos |

`tenant-logos` is mounted at the `logos` subdirectory specifically, not at `/app/wwwroot`,
because mounting the whole web root would shadow the application's own static assets.

<details>
<summary>Deploying without Docker</summary>

```bash
dotnet publish backend/src/Ecosys.Api/Ecosys.Api.csproj -c Release -o /srv/ecosys-api
cd ecosys-serviceops-frontend && npm ci && VITE_API_BASE_URL=https://your.host npm run build
```

Always publish fresh; never copy a `bin/` folder from a dev machine. You are then
responsible for everything the compose file does for you: the environment variables in §1,
the reverse proxy in §4, persistent paths for `Storage__LocalBasePath` and the web root's
`uploads/logos`, and the backups in §5.

</details>

## 4. Reverse proxy, TLS and CSP

`ecosys-serviceops-frontend/nginx.conf` is the config for the bundled `web` container. It
serves the SPA and proxies `/api`, `/hubs` (WebSockets, 1-hour read timeout) and `/files`
to the API on the same origin, and it sets the SPA's security headers.

**Put a TLS-terminating proxy in front of the `web` container.** It publishes plain HTTP on
`WEB_PORT`. Forward `X-Forwarded-For` and `X-Forwarded-Proto`, and add that proxy to
`ECOSYS_KNOWN_PROXIES`.

### Content-Security-Policy

There are two policies, deliberately different, and neither is a copy of the other:

* **The SPA's policy comes from nginx** (`nginx.conf`). A browser only honours the policy
  delivered with the *document*, and the document is `index.html` from nginx.
* **The API sets its own per-request policy** in `Program.cs`: a `default-src 'none'`
  lockdown for JSON and file downloads, and a looser one for Swagger UI, which only exists
  in Development. Nothing needs to be configured for it.

The SPA policy no longer hardcodes a vendor domain. `nginx.conf` ships as a template whose
only placeholder is `connect-src`; `/docker-entrypoint.d/10-ecosys-csp.sh` renders it at
container start using `envsubst` with a one-variable allow-list, so nginx's own `$host`,
`$uri` and `$remote_addr` survive untouched.

Leave `ECOSYS_CSP_CONNECT_SRC` empty for the shipped stack: nginx proxies the API and the
SignalR hub on the SPA's own origin, so `connect-src 'self'` already covers them. Set it
**only** if you split the API or the hub onto a different origin:

```dotenv
ECOSYS_CSP_CONNECT_SRC=https://api.example.com wss://api.example.com
```

Space-separated. The value is validated at container start — quotes, semicolons and commas
are rejected, and a bare `*` is refused. If SignalR fails to connect after a split-origin
change, check the browser console for a CSP violation naming `connect-src` before
suspecting the hub.

`style-src 'unsafe-inline'` remains in the SPA policy for email-template previews and
component libraries; tighten later if possible.

## 5. Backup and restore

> **`docker compose down -v` deletes `pgdata`, `uploads` and `tenant-logos`.** That is
> every tenant's database, every attachment and every logo, unrecoverably. Use
> `docker compose down` (no `-v`) to stop the stack. The `-v` flag has no undo.

### What this mechanism does and does not protect

`scripts/backup-db.sh` takes a compressed, timestamped, verified `pg_dump` of the database
onto a **host path** — deliberately not a Docker volume, since a named volume would be
destroyed by the same `down -v` the backups exist to survive.

It protects you from: `down -v`, a bad migration, a bad bulk edit, a dropped table.

It does **not** protect you from:

* **Losing files.** The dump covers the database only. Attachments (`uploads`) and logos
  (`tenant-logos`) are not in it — see "Backing up the file volumes" below.
* **Losing the host.** Dumps written to `./backups` die with the machine. Copy them
  off-box; that copy is your job, nothing here does it.
* **Losing recent work.** This is a periodic snapshot, not point-in-time recovery. A
  restore loses everything written since the last dump. If that window is unacceptable,
  you need WAL archiving or a managed Postgres, which this stack does not set up.

### Taking a backup

```bash
docker compose --profile backup run --rm db-backup
```

The `backup` profile means a plain `docker compose up` never starts this service. The
container takes one dump and exits; dumps land in `ECOSYS_BACKUP_DIR` (default `./backups`)
as `ecosys-<db>-<UTC timestamp>.dump`, and dumps older than
`ECOSYS_BACKUP_RETENTION_DAYS` (default 14) are pruned.

The same script also runs from the host, using `docker compose exec` against the running
`db` container, so no local `postgres` install is needed:

```bash
scripts/backup-db.sh
BACKUP_DIR=/srv/ecosys-backups BACKUP_RETENTION_DAYS=30 scripts/backup-db.sh
```

### Scheduling it

**This container is not a scheduler.** It runs once and exits. A looping `sleep` container
would have no retry, no alerting and no missed-run catch-up, and would silently stop
backing you up the moment it crashed — worse than nothing, because it looks like a backup
system. Schedule it from the host instead, where the failure is visible:

```cron
# Linux — /etc/cron.d/ecosys-backup, 03:15 daily. Log the output and monitor the log.
15 3 * * * root cd /srv/ecosys && docker compose --profile backup run --rm db-backup >> /var/log/ecosys-backup.log 2>&1
```

```powershell
# Windows — Task Scheduler, daily at 03:15
$action  = New-ScheduledTaskAction -Execute 'docker' `
    -Argument 'compose --profile backup run --rm db-backup' -WorkingDirectory 'C:\ecosys'
$trigger = New-ScheduledTaskTrigger -Daily -At 3:15am
Register-ScheduledTask -TaskName 'Ecosys DB backup' -Action $action -Trigger $trigger `
    -RunLevel Highest -User 'SYSTEM'
```

The script exits non-zero if no usable dump was produced, so the scheduler can alert on it.
**Set up that alert.** A backup job nobody watches is a backup job that has already failed.

### Verifying a backup

The backup script verifies each dump before promoting it — it runs `pg_restore -l`, which
parses the archive's table of contents and proves the file is readable and not truncated,
without touching any database. A dump that fails is deleted rather than left looking valid.

Verify one yourself the same way:

```bash
scripts/restore-db.sh ./backups/ecosys-ecosys-20260825T031500Z.dump --list
```

That lists the archive contents and restores nothing. A dump that lists thousands of
objects across the expected tables is structurally sound.

**Structurally sound is not the same as restorable.** The only real test is a restore.
Periodically restore a dump into a scratch database and check that the API starts against
it:

```bash
docker compose exec -T db createdb -U ecosys ecosys_restoretest
scripts/restore-db.sh ./backups/<dump> --database ecosys_restoretest
docker compose exec -T db dropdb -U ecosys ecosys_restoretest   # when finished
```

### Restoring

```bash
scripts/restore-db.sh ./backups/ecosys-ecosys-20260825T031500Z.dump
```

**This is destructive.** It drops and recreates every object in the target database; work
done since the dump is gone. The script makes you type the target database name back before
it touches anything, prints what it is about to do, stops the `api` service so open
connections cannot block the drops, and restarts it afterwards — including if the restore
fails or you interrupt it. It reports `PASS` only after confirming the restored schema
actually has tables.

For unattended use (runbook automation only), set `ECOSYS_RESTORE_CONFIRM` to the target
database name.

Take a fresh backup **before** restoring an old one. If the old dump turns out to be the
wrong one, that fresh dump is the only way back.

After a restore: check the API log for a clean migration run, then log in and spot-check a
tenant's work orders and users.

### Backing up the file volumes

Attachments and logos are not in the database dump. Volume names are prefixed with the
compose project name (the directory name — confirm with `docker volume ls`):

```bash
docker run --rm \
  -v ecosys_uploads:/uploads:ro -v ecosys_tenant-logos:/logos:ro \
  -v "$PWD/backups:/backups" alpine \
  tar czf /backups/files-$(date -u +%Y%m%dT%H%M%SZ).tar.gz -C / uploads logos
```

Run it alongside the database backup so the two stay roughly in step, and copy both
off-box together.

## 6. Post-deploy smoke checks

1. `GET /health/live` returns 200 — the process is up.
2. `GET /health/ready` returns 200 — it can reach the database. This is what the compose
   healthcheck polls; `/health` is the older database-backed endpoint and still works.
3. `GET /swagger` returns 404 (Swagger must be off in Production).
4. Login works; response headers include `Strict-Transport-Security` and
   `X-Content-Type-Options`.
5. 21+ rapid failed logins **from one client IP** return `429 RATE_LIMITED` — and logins
   from a *different* IP still succeed. If they do not, `ECOSYS_KNOWN_PROXIES` is wrong and
   everyone is sharing one bucket.
6. `POST /api/public/leads` more than 30×/min returns 429.
7. Attachment upload **and** download work — this is the check that catches a volume the
   non-root user cannot write to (§7).
8. Tenant logo upload works, and the logo is still there after
   `docker compose up -d --force-recreate api`.
9. SignalR connects (browser devtools → WS to `/hubs/serviceops`, no CSP error in console).
10. An audit-log entry created by step 4 records **your** IP, not the nginx container's.
11. `docker compose --profile backup run --rm db-backup` writes a dump and exits 0.

`backend/smoke-test.ps1/run-serviceops-smoke.ps1` automates an end-to-end functional pass.
It takes credentials from the environment and **creates a tenant, users and work orders
without cleaning up** — point it at a disposable environment, never production:

```powershell
$env:ECOSYS_SMOKE_SUPERADMIN_EMAIL    = 'superadmin@your-instance'
$env:ECOSYS_SMOKE_SUPERADMIN_PASSWORD = '...'
./backend/smoke-test.ps1/run-serviceops-smoke.ps1 -ApiBaseUrl https://your.host
```

## 7. Upgrading an existing deployment

Deployments built before 2026-08 need three one-time steps. Read all three before starting.

### 7.1 Rescue tenant logos before recreating the API container — do this first

Older builds wrote tenant branding logos into `/app/wwwroot/uploads/logos`, which was
**not** a volume. Those files live in the running container's writable layer and are
destroyed the moment it is recreated, while each tenant's `LogoUrl` still points at them.
Copy them out **before** you pull, then put them back afterwards:

```bash
# Before upgrading — while the old container is still running:
docker compose cp api:/app/wwwroot/uploads/logos ./logos-rescue

# ... perform the upgrade ...

# After the new stack is up, restore them into the new tenant-logos volume:
docker compose cp ./logos-rescue/. api:/app/wwwroot/uploads/logos
docker compose exec --user root api chown -R app:app /app/wwwroot/uploads/logos
```

If you have already recreated the container, the logos are gone; affected tenants must
re-upload from Settings → Branding. From this upgrade onward the `tenant-logos` volume
keeps them across redeploys.

### 7.2 Chown pre-existing volumes — the API cannot write until you do

The API container now runs as the image's non-root `app` user (uid 1654) instead of root.
Docker only applies the image's ownership when it *initialises* a volume, so a **new**
volume comes up owned by `app` automatically — but a volume that already exists was
created root-owned and stays that way. The API will start normally and then fail every
attachment upload with a permission error.

Run this once, after pulling the new images and before `up -d`:

```bash
docker compose run --rm --user root --entrypoint sh api \
  -c 'chown -R app:app /app/storage/uploads /app/wwwroot/uploads/logos'
```

Then verify with smoke check 7 above: upload an attachment and download it again. If uploads
fail with a permission error, this step did not run or did not cover the right path.

New deployments need none of this.

### 7.3 Add the newly required variables

`ECOSYS_PUBLIC_URL` is now **required** — `docker compose up` fails fast if it is missing,
rather than emailing password-reset links pointing at the previous vendor domain. Set it,
then rebuild the frontend, because `VITE_API_BASE_URL` is baked in at build time:

```bash
docker compose build web && docker compose up -d web
```

Also set `ECOSYS_KNOWN_PROXIES` (§1) if you have not already — without it, audit logs
record the proxy's IP and all callers share one auth rate-limit bucket.

Diff your `.env` against `.env.example` for anything else new.

### 7.4 The migration baseline

The ServiceOps migration history was squashed into one baseline, `InitialBaseline`, when the
out-of-scope modules were pruned (Phase 0). A fresh database is built from that baseline at
first start-up. A database built from the old history cannot be upgraded in place: the API
refuses to start against one and says so. Recreate it, or load its data into a fresh database
built from the baseline.

**Tenant offboarding is an ordered teardown, not one statement.** `tenants` cascades to nearly
every tenant-scoped table, but roughly 28 intra-tenant `RESTRICT` foreign keys are checked
immediately rather than deferred, so a single `DELETE FROM tenants` fails. Delete in dependency
order: work-order leaves and `work_orders`, then inventory and knowledge leaves,
then assets, systems, sites, contracts, clients, suppliers, technicians, materials, branches
(leaf branches before parents), then `users`, then the tenant row.

## 8. Secrets hygiene

* `.env`, `config.env`, `*.log`, `*.out`, `*.err` and `*.bak` are git-ignored. Captured
  process output and a stale `.cs.bak` were tracked historically and have been untracked;
  they remain on disk but must not be re-added.
* Untracking removes them from future commits, **not from history**. Anything genuinely
  secret that was ever committed must be treated as disclosed and rotated — that includes
  the dev credentials formerly hardcoded in the smoke-test script, which now come from
  `ECOSYS_SMOKE_*` environment variables.
* Two tenant logo PNGs under `backend/src/Ecosys.Api/wwwroot/uploads/logos/` are still
  tracked. They are runtime uploads, not source, and are shadowed at runtime by the
  `tenant-logos` volume.

## 9. Known deferred items (post-launch backlog)

- No point-in-time recovery: backups are periodic dumps (§5). Consider WAL archiving or
  managed Postgres if the loss window matters.
- Nothing verifies that backups are *restorable* automatically — the restore rehearsal in
  §5 is a manual exercise. Put it on the calendar.
- Move JWTs from localStorage to httpOnly cookies (or add refresh-token rotation).
- Shorten `Jwt:ExpiryMinutes` from 480 (product decision — field-shift length).
- Hash the legacy `MonitoringSettings.WebhookSecret` at rest (needs a data migration).
- Upgrade Vite 6 → 8 (dev-only esbuild advisory; does not affect production builds).
- Consider a global rate limiter in addition to per-endpoint policies.
- `PublicAppOptions.PublicUrl` still has the vendor domain as its compiled-in C# default.
  It is unreachable in practice (`appsettings.json` always supplies the key, and compose
  overrides it), but it should become `string.Empty` with a startup guard.
