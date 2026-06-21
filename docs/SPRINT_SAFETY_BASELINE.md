# Sprint Safety Baseline

- Current branch: `ui-email-governance-sprint`
- Baseline commit: `80afaf1` (`Complete Phase 1.5 responsive UI hardening`)
- Confirmed production API base URL: `https://app.ecosysdigital.co.ke`

## Protected Areas

- Do not break login.
- Do not break SuperAdmin or tenant routing.
- Do not change auth response contracts unless tests prove it is required.
- Do not change role normalization unless a failing test proves it is required.
- Do not change `VITE_API_BASE_URL` back to IP-based or localhost production values.
- Do not touch VPS, Nginx, systemd, certbot, SSH, or production PostgreSQL/SMTP from Codex.

## Local Validation Commands

### Frontend

- `npm ci`
- `npm run build`
- `npm run lint`

### Backend

- `dotnet restore Ecosys.slnx`
- `dotnet build Ecosys.slnx`
- `dotnet test Ecosys.slnx`

## Baseline Notes

- The repository had pre-existing uncommitted frontend auth/platform changes and Playwright report artifacts before this sprint branch was created.
- Frontend local development `.env` remains pointed at `http://localhost:5072` for repository-local validation only.
- Explicit production/example frontend env files were added so production builds keep the HTTPS domain API base.
