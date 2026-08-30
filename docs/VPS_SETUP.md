# Ecosys ServiceOps — VPS Provisioning Guide (Ubuntu / Debian)

Last updated: 2026-08-29.

[docs/DEPLOYMENT.md](DEPLOYMENT.md) documents the Docker Compose stack itself — environment
variables, volumes, backups, upgrades. It assumes an **already-provisioned Docker host**.
This guide is the wrapper around it: taking a bare Ubuntu/Debian VPS to a running,
TLS-terminated deployment.

Read this once end to end before starting. Cross-references to `DEPLOYMENT.md` sections are
marked `DEPLOYMENT.md §N`.

---

## 0. Before you touch the VPS

- **DNS**: point an `A` record (e.g. `serviceops.yourdomain.com`) at the VPS public IP.
  Confirm it has propagated before starting TLS:

  ```bash
  dig +short serviceops.yourdomain.com
  ```

- **Size**: the stack runs PostgreSQL + the .NET API + nginx. Floor: **2 vCPU / 4 GB RAM /
  20 GB disk**. A 1 GB box will OOM during the .NET image build.
- **Access**: SSH in as a sudo-capable **non-root** user. All commands below assume that.
- **Code**: make sure every change you want deployed is **committed and pushed**. A fresh
  clone on the VPS only has what is in the remote.

## 1. Harden the box

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ufw fail2ban
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Do **not** open `5432` (Postgres) or `8080` (the `web` container) to the internet.
Postgres stays on the Docker network; `web` is reached only through the TLS proxy in §7.

## 2. Install Docker Engine + Compose plugin

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker                 # or log out and back in
docker compose version        # must show Compose v2 (the plugin, not docker-compose)
```

## 3. Get the code onto the VPS

```bash
sudo mkdir -p /srv/ecosys && sudo chown $USER:$USER /srv/ecosys
git clone <your-repo-url> /srv/ecosys
cd /srv/ecosys
```

Private repo: use a deploy key or a personal access token. The compose project name is
derived from the directory name (`ecosys`), which is why volumes and the Docker network are
prefixed `ecosys_` throughout this guide and `DEPLOYMENT.md`.

## 4. Build the `.env`

```bash
cp .env.example .env
```

Generate the two secrets:

```bash
echo "POSTGRES_PASSWORD=$(openssl rand -base64 32)"
echo "ECOSYS_JWT_SIGNING_KEY=$(openssl rand -base64 48)"
```

Edit `.env` and set at least:

| Variable | Value |
|---|---|
| `POSTGRES_PASSWORD` | generated above |
| `ECOSYS_JWT_SIGNING_KEY` | generated above — ≥ 32 chars, not a denylisted dev default |
| `ECOSYS_PUBLIC_URL` | `https://serviceops.yourdomain.com` — scheme + host, **no trailing slash** |
| `ECOSYS_CORS_ALLOWED_ORIGINS` | the same URL |
| `ECOSYS_PLATFORM_ADMIN_EMAIL` | your real admin email (not a known default) |
| `ECOSYS_PLATFORM_ADMIN_PASSWORD` | ≥ 12 chars, not `ChangeMe123!` / `SuperAdmin123!` etc. |
| `WEB_PORT` | `8080` — kept bound to localhost in §6 |
| `ECOSYS_KNOWN_PROXIES` | leave blank for now; filled in §5 once the Docker subnet exists |

The API validates these at startup and **refuses to boot** on missing, too-short, or
known-default values (`DEPLOYMENT.md §1`). `ECOSYS_PUBLIC_URL` is compiled into the frontend
bundle **at image build time** — changing it later needs `docker compose build web`, not
just a restart. SMTP variables are optional, but with no SMTP host the app sends no email
at all, including password-reset mail.

## 5. First build and start

```bash
docker compose build
docker compose up -d
docker compose logs -f api
```

EF Core migrations run automatically at API startup and the process aborts if they fail, so
a container that reaches "ready" has a migrated schema (`DEPLOYMENT.md §3`). Watch for a
clean migration log and `Now listening on`, then Ctrl-C out of the log tail.

Now capture the Docker subnet and write it into `.env`:

```bash
docker network inspect ecosys_default -f '{{range .IPAM.Config}}{{.Subnet}}{{end}}'
# e.g. 172.18.0.0/16   ->   set  ECOSYS_KNOWN_PROXIES=172.18.0.0/16  in .env
docker compose up -d api        # recreate the API so it picks up the new value
```

Without this, `X-Forwarded-For` is not trusted and every request looks like it came from
the nginx container: audit logs record the proxy address and all callers share a single
auth rate-limit bucket, so one noisy client can lock everyone out of login
(`DEPLOYMENT.md §1`). Unparseable entries are logged and skipped at startup — check the API
log after changing this.

## 6. Bind the web port to localhost only

Edit `docker-compose.yml`, the `web` service, so the published port is not world-facing:

```yaml
    ports:
      - "127.0.0.1:${WEB_PORT:-8080}:80"
```

Apply:

```bash
docker compose up -d web
```

The TLS proxy in §7 is now the only thing that can reach the SPA.

## 7. TLS-terminating reverse proxy

Run this as a **host process**, in front of the `web` container — not inside compose.
`web` publishes plain HTTP on `127.0.0.1:8080`; the proxy terminates TLS and forwards
`X-Forwarded-For` / `X-Forwarded-Proto` (`DEPLOYMENT.md §4`).

### Option A — Caddy (recommended: automatic Let's Encrypt)

```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
  | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
  | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy
```

`/etc/caddy/Caddyfile`:

```
serviceops.yourdomain.com {
    reverse_proxy 127.0.0.1:8080 {
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
    }
}
```

```bash
sudo systemctl reload caddy
sudo systemctl enable caddy
```

Caddy issues and renews the certificate automatically. It reaches the container over the
Docker bridge gateway, which is inside the subnet you already put in
`ECOSYS_KNOWN_PROXIES`, so no extra entry is needed — confirm with smoke check 5 in §8.
WebSocket upgrades for `/hubs` pass through `reverse_proxy` without extra config.

### Option B — nginx + certbot

```bash
sudo apt install -y nginx certbot python3-certbot-nginx
```

`/etc/nginx/sites-available/serviceops` (symlink into `sites-enabled/`, remove `default`):

```nginx
server {
    listen 80;
    server_name serviceops.yourdomain.com;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Upgrade    $http_upgrade;   # /hubs WebSocket
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 3600s;                    # long-lived SignalR connections
    }
}
```

```bash
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d serviceops.yourdomain.com
```

certbot rewrites the block for TLS and installs a renewal timer.

## 8. Post-deploy smoke checks

Against `https://serviceops.yourdomain.com` (full list in `DEPLOYMENT.md §6`):

1. `GET /health/live` → `200` — process is up.
2. `GET /health/ready` → `200` — database reachable. This is what the compose healthcheck polls.
3. `GET /swagger` → `404` — Swagger must be off in Production.
4. Log in as the platform admin. Response headers include `Strict-Transport-Security` and
   `X-Content-Type-Options`.
5. 21+ rapid failed logins **from one IP** → `429 RATE_LIMITED`, and a login from a
   **different** IP still succeeds. If a second IP is also blocked, `ECOSYS_KNOWN_PROXIES`
   is wrong and everyone shares one bucket.
6. Upload an attachment **and download it back** — this is the check that catches a volume
   the non-root API user cannot write to (`DEPLOYMENT.md §7.2`).
7. Tenant logo upload works and survives `docker compose up -d --force-recreate api`.
8. SignalR connects — browser devtools → WS to `/hubs/serviceops`, no CSP error in console.
9. The audit-log entry from step 4 records **your** IP, not the nginx container's.

`backend/smoke-test.ps1/run-serviceops-smoke.ps1` automates an end-to-end functional pass.
It **creates a tenant, users and work orders and does not clean up** — run it against a
disposable instance, never a production one you care about. It reads credentials from
`ECOSYS_SMOKE_SUPERADMIN_EMAIL` / `ECOSYS_SMOKE_SUPERADMIN_PASSWORD` (`DEPLOYMENT.md §6`).

## 9. Backups — set up now, not later

Schedule the dump from host cron (the backup container runs once and exits; it is not a
scheduler — `DEPLOYMENT.md §5`):

```bash
sudo tee /etc/cron.d/ecosys-backup >/dev/null <<'EOF'
15 3 * * * root cd /srv/ecosys && docker compose --profile backup run --rm db-backup >> /var/log/ecosys-backup.log 2>&1
EOF
```

The database dump does **not** contain attachments or logos. Capture the file volumes
alongside it and copy both off-box:

```bash
docker run --rm \
  -v ecosys_uploads:/uploads:ro -v ecosys_tenant-logos:/logos:ro \
  -v "$PWD/backups:/backups" alpine \
  tar czf /backups/files-$(date -u +%Y%m%dT%H%M%SZ).tar.gz -C / uploads logos
```

The backup script exits non-zero if no usable dump was produced. **Alert on that** — a
backup job nobody watches is one that has already failed. Rehearse a restore into a scratch
database periodically (`DEPLOYMENT.md §5`).

## 10. Redeploying after a change

```bash
cd /srv/ecosys
docker compose --profile backup run --rm db-backup    # snapshot first
git pull
docker compose build
docker compose up -d
docker compose logs -f api                             # confirm a clean migration run
```

- If `ECOSYS_PUBLIC_URL` changed, also run `docker compose build web && docker compose up -d web`
  — it is baked into the bundle at build time.
- Diff `.env` against `.env.example` after every pull for newly required variables.
- Upgrading a deployment first built **before 2026-08**: read `DEPLOYMENT.md §7` first —
  there are one-time volume `chown` and tenant-logo rescue steps, and pre-migration data
  checks in `§7.4`, that must run before `up -d` or attachment uploads silently fail.
- `docker compose down -v` deletes `pgdata`, `uploads` and `tenant-logos` — the entire
  database and every file, with no undo. Stop the stack with `docker compose down` (no `-v`).

## 11. Operational notes

- **Log rotation**: `docker compose` uses the `json-file` log driver by default, which
  grows unbounded. Add to `/etc/docker/daemon.json` and `sudo systemctl restart docker`:

  ```json
  { "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
  ```

- **Auto-start on reboot**: every service in the stack is `restart: unless-stopped`, and
  Docker's own service is enabled by the install script, so the stack comes back after a
  reboot. Caddy/nginx come back via their systemd units (`systemctl enable`).
- **Time zone**: the app's fallback zone is `Africa/Nairobi` unless a tenant or the
  platform overrides it, or you set `ECOSYS_DEFAULT_TIME_ZONE` in `.env`.
