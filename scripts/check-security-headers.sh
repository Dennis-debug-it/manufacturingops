#!/usr/bin/env bash
#
# Security-header smoke test (F4-02).
#
# Verifies that the LIVE deployment actually serves the security headers the committed
# ecosys-serviceops-frontend/nginx.conf defines. The audit found the running edge served none of
# these while the committed config sets them all — i.e. deployment drift. Run this against the real
# origin after every deploy (and in CI) so the drift can't silently return.
#
# Usage:
#   scripts/check-security-headers.sh https://app.ecosysdigital.co.ke
#
# Exit code 0 = all required headers present; non-zero = at least one missing (details printed).

set -uo pipefail

URL="${1:-}"
if [[ -z "$URL" ]]; then
  echo "usage: $0 <url>" >&2
  exit 2
fi

# Fetch response headers once (follow redirects to the final HTML document).
headers="$(curl -sSL -D - -o /dev/null "$URL")" || {
  echo "ERROR: could not reach $URL" >&2
  exit 2
}

fail=0

require() {
  local name="$1" expect="${2:-}"
  local line
  line="$(printf '%s\n' "$headers" | grep -i "^${name}:" | head -1)"
  if [[ -z "$line" ]]; then
    echo "MISSING  $name"
    fail=1
  elif [[ -n "$expect" ]] && ! printf '%s' "$line" | grep -iq "$expect"; then
    echo "WRONG    $name (expected to contain '$expect') -> ${line#*: }"
    fail=1
  else
    echo "OK       ${line}"
  fi
}

forbid() {
  local name="$1"
  if printf '%s\n' "$headers" | grep -iq "^${name}:"; then
    echo "LEAK     $name should not be present -> $(printf '%s\n' "$headers" | grep -i "^${name}:" | head -1)"
    fail=1
  else
    echo "OK       $name absent"
  fi
}

echo "Checking security headers on: $URL"
echo "---------------------------------------------"
require "Content-Security-Policy" "frame-ancestors 'none'"
require "Strict-Transport-Security" "max-age="
require "X-Content-Type-Options" "nosniff"
require "X-Frame-Options" "DENY"
require "Referrer-Policy"
# server_tokens off => the Server header must not disclose an nginx version (X-03).
if printf '%s\n' "$headers" | grep -i "^Server:" | grep -Eiq "nginx/[0-9]"; then
  echo "LEAK     Server header discloses nginx version -> $(printf '%s\n' "$headers" | grep -i '^Server:' | head -1)"
  fail=1
else
  echo "OK       Server header does not disclose a version"
fi
echo "---------------------------------------------"

if [[ "$fail" -ne 0 ]]; then
  echo "RESULT: FAIL — one or more security headers are missing/incorrect on the live origin."
  exit 1
fi
echo "RESULT: PASS — all required security headers present."
