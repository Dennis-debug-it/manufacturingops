#!/usr/bin/env bash
#
# PostgreSQL restore for the Ecosys ServiceOps stack (EC-P1-06a).
#
# THIS IS DESTRUCTIVE. It drops and recreates every object in the target database from a
# dump produced by scripts/backup-db.sh. Anything written since that dump is gone.
# The operator has to type the target database name back before anything is touched.
#
# Usage:
#   scripts/restore-db.sh ./backups/ecosys-ecosys-20260825T031500Z.dump
#   scripts/restore-db.sh <dump> --database ecosys_staging
#   scripts/restore-db.sh <dump> --list          # inspect the archive, restore nothing
#
# Non-interactive (CI / runbook automation) — the value must equal the target database:
#   ECOSYS_RESTORE_CONFIRM=ecosys scripts/restore-db.sh <dump>
#
# Environment:
#   POSTGRES_USER  role to restore as    (default: ecosys)
#   POSTGRES_DB    default target db     (default: ecosys)
#   COMPOSE_FILE   passed through to docker compose
#
# Runs the postgres client binaries inside the running `db` service, so no local
# postgres install is needed. The `api` service is stopped for the duration — open
# connections would block the DROPs — and started again afterwards.

set -euo pipefail

DUMP=""
TARGET_DB="${POSTGRES_DB:-ecosys}"
DB_USER="${POSTGRES_USER:-ecosys}"
LIST_ONLY=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --database|-d) TARGET_DB="${2:?--database needs a value}"; shift 2 ;;
        --user|-U)     DB_USER="${2:?--user needs a value}"; shift 2 ;;
        --list|-l)     LIST_ONLY=1; shift ;;
        -h|--help)     sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        -*)            echo "unknown option: $1" >&2; exit 2 ;;
        *)             DUMP="$1"; shift ;;
    esac
done

if [[ -z "$DUMP" ]]; then
    echo "usage: $0 <dump-file> [--database NAME] [--user ROLE] [--list]" >&2
    exit 2
fi
[[ -f "$DUMP" ]] || { echo "ERROR: no such dump file: $DUMP" >&2; exit 2; }

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE=(docker-compose)
else
    echo "ERROR: docker compose is not available." >&2
    exit 2
fi

TOC="$(mktemp "${TMPDIR:-/tmp}/ecosys-restore-toc.XXXXXX")"

# API_WAS_STOPPED flips to 1 once the api service has been stopped, so this trap knows
# whether it has to put it back. Without it, a Ctrl-C or an unexpected failure part-way
# through leaves the API down and the deployment dark.
API_WAS_STOPPED=0
cleanup() {
    rm -f "$TOC"
    if [[ "$API_WAS_STOPPED" -eq 1 ]]; then
        API_WAS_STOPPED=0
        echo "==> starting api again" >&2
        "${COMPOSE[@]}" start api ||
            echo "WARNING: could not restart api. Start it by hand: docker compose start api" >&2
    fi
}
trap cleanup EXIT

# Prove the archive is readable before anyone is asked to confirm anything.
if ! "${COMPOSE[@]}" exec -T db pg_restore -l < "$DUMP" > "$TOC" 2>/dev/null; then
    echo "ERROR: '$DUMP' is not a readable pg_dump custom-format archive." >&2
    exit 1
fi

if [[ "$LIST_ONLY" -eq 1 ]]; then
    cat "$TOC"
    exit 0
fi

# pg_restore -l comments every non-entry line with ';', so this counts real TOC entries.
objects="$(grep -cv '^;' "$TOC" || true)"

# ---- The guard --------------------------------------------------------------------
cat >&2 <<EOF

  ############################################################################
  #  DESTRUCTIVE RESTORE                                                     #
  ############################################################################

  dump file        : $DUMP
  dump taken       : $(date -u -r "$DUMP" '+%Y-%m-%d %H:%M:%SZ' 2>/dev/null || echo 'unknown')
  objects in dump  : $objects
  TARGET DATABASE  : $TARGET_DB   (role: $DB_USER)
  compose project  : $(basename "$(pwd)")

  Every table, row and sequence currently in "$TARGET_DB" will be DROPPED and
  replaced by the contents of that dump. Data written since the dump was taken
  is NOT recoverable afterwards. The 'api' service will be stopped and restarted.

  Take a fresh backup first if you have not already:  scripts/backup-db.sh

EOF

if [[ -n "${ECOSYS_RESTORE_CONFIRM:-}" ]]; then
    typed="$ECOSYS_RESTORE_CONFIRM"
    echo "  (confirmed non-interactively via ECOSYS_RESTORE_CONFIRM)" >&2
else
    if [[ ! -t 0 ]] && [[ ! -r /dev/tty ]]; then
        echo "ERROR: no terminal to confirm on. Set ECOSYS_RESTORE_CONFIRM=$TARGET_DB to proceed." >&2
        exit 1
    fi
    printf '  Type the target database name to proceed (or anything else to abort): ' >&2
    read -r typed < /dev/tty
fi

if [[ "$typed" != "$TARGET_DB" ]]; then
    echo "  Aborted — '$typed' does not match '$TARGET_DB'. Nothing was changed." >&2
    exit 1
fi

# ---- Restore ----------------------------------------------------------------------
echo "==> stopping api so it releases its connections"
"${COMPOSE[@]}" stop api || true
API_WAS_STOPPED=1   # from here on the EXIT trap restarts it, however this script ends

restore_rc=0
echo "==> restoring $DUMP into $TARGET_DB"
# --clean --if-exists drops each object before recreating it, so dropping something that
# was never there is not an error; --no-owner/--no-privileges let the dump land under
# whatever role this deployment uses. --exit-on-error makes pg_restore stop at the first
# real failure rather than plough on and report a "successful" partial restore, so a
# non-zero exit here means the restore genuinely did not complete.
"${COMPOSE[@]}" exec -T db pg_restore \
    -U "$DB_USER" -d "$TARGET_DB" \
    --clean --if-exists --no-owner --no-privileges --exit-on-error \
    < "$DUMP" || restore_rc=$?

if [[ "$restore_rc" -ne 0 ]]; then
    echo "RESULT: FAIL — pg_restore exited $restore_rc. The database may be half-restored;" >&2
    echo "        re-run this script against the same dump once the cause is fixed." >&2
    exit 1
fi

echo "==> verifying"
tables="$("${COMPOSE[@]}" exec -T db psql -U "$DB_USER" -d "$TARGET_DB" -tAc \
    "select count(*) from information_schema.tables where table_schema='public'" | tr -d '[:space:]')"
echo "    $tables tables in $TARGET_DB.public"
if ! [[ "$tables" =~ ^[0-9]+$ ]] || [[ "$tables" -lt 1 ]]; then
    echo "RESULT: FAIL - $TARGET_DB.public has no tables after the restore." >&2
    exit 1
fi

echo "RESULT: PASS — $TARGET_DB restored from $DUMP."
echo "Check the API log for a clean migration run, then log in and spot-check a tenant."
