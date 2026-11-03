#!/bin/sh
#
# PostgreSQL backup for the Ecosys ServiceOps stack (EC-P1-06a).
#
# The stack's only durability is the `pgdata` named volume. `docker compose down -v`
# deletes it, and every tenant's data with it, with no way back. This script takes a
# compressed, timestamped, *verified* pg_dump onto a host path and prunes dumps older
# than the retention window.
#
# Two modes, auto-detected:
#   * direct — PGHOST is set and pg_dump is on PATH. This is how the opt-in `db-backup`
#     compose service runs it: straight over the compose network.
#   * host   — anything else. Runs the postgres client binaries inside the running `db`
#     service via `docker compose exec`, so no local postgres install is needed.
#
# Usage:
#   scripts/backup-db.sh                                  # dump into ./backups
#   BACKUP_DIR=/srv/ecosys-backups scripts/backup-db.sh
#   BACKUP_RETENTION_DAYS=30 scripts/backup-db.sh
#
# Environment (all optional):
#   BACKUP_DIR             destination directory                 (default: ./backups)
#   BACKUP_RETENTION_DAYS  prune dumps older than N days         (default: 14; 0 = keep all)
#   POSTGRES_USER          role to dump as, host mode            (default: ecosys)
#   POSTGRES_DB            database to dump, host mode           (default: ecosys)
#   PGHOST PGUSER PGDATABASE PGPASSWORD                          (direct mode)
#   POSTGRES_PASSWORD      used as PGPASSWORD if PGPASSWORD is unset (direct mode)
#   COMPOSE_FILE           passed through to docker compose      (host mode)
#
# Exit 0 = a verified dump was written. Non-zero = nothing usable was produced; the
# partial file is removed rather than left looking like a good backup.
#
# POSIX sh on purpose (not bash): the compose backup service runs this inside
# postgres:16-alpine, which ships busybox ash and no bash.

set -eu

BACKUP_DIR="${BACKUP_DIR:-./backups}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"

log() { echo "[backup-db] $*"; }
die() { echo "[backup-db] ERROR: $*" >&2; exit 1; }

# ---- Pick a mode ------------------------------------------------------------------
if [ -n "${PGHOST:-}" ] && command -v pg_dump >/dev/null 2>&1; then
    MODE=direct
    DB_USER="${PGUSER:-ecosys}"
    DB_NAME="${PGDATABASE:-ecosys}"
    # pg_dump reads PGPASSWORD; accept POSTGRES_PASSWORD too so the compose service can
    # pass the one secret the rest of the stack already uses.
    if [ -z "${PGPASSWORD:-}" ] && [ -n "${POSTGRES_PASSWORD:-}" ]; then
        PGPASSWORD="$POSTGRES_PASSWORD"
        export PGPASSWORD
    fi
else
    MODE=host
    DB_USER="${POSTGRES_USER:-ecosys}"
    DB_NAME="${POSTGRES_DB:-ecosys}"

    if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
        COMPOSE="docker compose"
    elif command -v docker-compose >/dev/null 2>&1; then
        COMPOSE="docker-compose"
    else
        die "no PGHOST/pg_dump for direct mode, and docker compose is not available for host mode."
    fi
fi

# Run a postgres client binary, wherever this script happens to be running.
run_pg() {
    if [ "$MODE" = direct ]; then
        "$@"
    else
        # shellcheck disable=SC2086
        $COMPOSE exec -T db "$@"
    fi
}

# ---- Take the dump ----------------------------------------------------------------
mkdir -p "$BACKUP_DIR" || die "cannot create backup directory '$BACKUP_DIR'."
[ -w "$BACKUP_DIR" ] || die "backup directory '$BACKUP_DIR' is not writable."

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$BACKUP_DIR/ecosys-$DB_NAME-$stamp.dump"
partial="$target.partial"

log "mode=$MODE database=$DB_NAME role=$DB_USER -> $target"

# -Fc = custom format: already compressed, and pg_restore can list/verify it and do a
# selective restore. Plain SQL + gzip can do neither.
if ! run_pg pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc --no-owner --no-privileges > "$partial"; then
    rm -f "$partial"
    die "pg_dump failed; no backup written."
fi

if [ ! -s "$partial" ]; then
    rm -f "$partial"
    die "pg_dump produced an empty file; no backup written."
fi

# ---- Verify before promoting it to a real backup ----------------------------------
# `pg_restore -l` parses the archive's table of contents. It does not touch any
# database, but it does prove the file is a readable, non-truncated dump.
if ! run_pg pg_restore -l < "$partial" > /dev/null 2>&1; then
    rm -f "$partial"
    die "the dump did not survive verification (pg_restore -l); no backup written."
fi

mv "$partial" "$target"
size="$(du -h "$target" | cut -f1)"
log "OK: $target ($size)"

# ---- Prune ------------------------------------------------------------------------
case "$BACKUP_RETENTION_DAYS" in
    ''|*[!0-9]*) die "BACKUP_RETENTION_DAYS must be a whole number of days (got '$BACKUP_RETENTION_DAYS'). The dump at $target is fine; only pruning was skipped." ;;
esac

if [ "$BACKUP_RETENTION_DAYS" -gt 0 ]; then
    log "pruning dumps older than $BACKUP_RETENTION_DAYS days in $BACKUP_DIR"
    find "$BACKUP_DIR" -maxdepth 1 -type f -name "ecosys-*.dump" \
        -mtime "+$BACKUP_RETENTION_DAYS" -print -delete || true
    # Partials from an interrupted run are never useful.
    find "$BACKUP_DIR" -maxdepth 1 -type f -name "ecosys-*.dump.partial" \
        -mtime +1 -print -delete || true
else
    log "retention disabled (BACKUP_RETENTION_DAYS=0) — nothing pruned"
fi

log "done"
