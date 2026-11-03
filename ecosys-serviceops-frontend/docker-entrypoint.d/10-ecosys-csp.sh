#!/bin/sh
#
# Render the SPA's nginx config at container start (EC-P3-05b).
#
# nginx.conf ships as a template with exactly one placeholder, ${ECOSYS_CSP_CONNECT_SRC},
# so the Content-Security-Policy no longer hardcodes one vendor's domain. envsubst is
# given an explicit single-variable allow-list, which is what keeps nginx's own runtime
# variables ($host, $uri, $remote_addr, $http_upgrade, ...) from being eaten.
#
# ECOSYS_CSP_CONNECT_SRC  space-separated extra origins for connect-src. Optional.
#   unset/empty -> `connect-src 'self'`, correct for the shipped compose stack where this
#                  same nginx proxies /api, /hubs and /files on this origin.
#   set         -> appended after 'self', e.g.
#                  ECOSYS_CSP_CONNECT_SRC="https://api.example.com wss://api.example.com"
#
# The value is validated, not trusted: it lands inside a response header, so a stray quote
# or semicolon would let it terminate the directive or the header. A bare `*` is refused -
# the point of this variable is to name the extra origins, not to switch the policy off.
#
# The official nginx image runs every executable /docker-entrypoint.d/*.sh in sorted order
# before starting nginx. The built-in 20-envsubst-on-templates.sh reads /etc/nginx/templates,
# which this image leaves empty, so it has nothing to do and cannot clobber this output.

set -eu

TEMPLATE=/etc/nginx/ecosys/default.conf.template
OUTPUT=/etc/nginx/conf.d/default.conf

log() { echo "10-ecosys-csp.sh: $*"; }
die() { echo "10-ecosys-csp.sh: ERROR: $*" >&2; exit 1; }

[ -r "$TEMPLATE" ] || die "missing template $TEMPLATE (broken image build)."

value="${ECOSYS_CSP_CONNECT_SRC:-}"
# Collapse surrounding/repeated whitespace so the rendered header stays tidy.
value="$(printf '%s' "$value" | tr -s '[:space:]' ' ' | sed 's/^ //; s/ $//')"

if [ -n "$value" ]; then
    # Only scheme/host/port shapes. No quotes, no ';', no ',', nothing that could break out
    # of the directive or the header.
    if ! printf '%s' "$value" | grep -qE '^[A-Za-z0-9*][A-Za-z0-9.:/*_-]*( [A-Za-z0-9*][A-Za-z0-9.:/*_-]*)*$'; then
        die "ECOSYS_CSP_CONNECT_SRC contains characters that are not allowed in a CSP source list: '$value'"
    fi
    for token in $value; do
        [ "$token" != "*" ] || die "ECOSYS_CSP_CONNECT_SRC may not contain a bare '*'. List the actual origins."
    done
    # The template reads `connect-src 'self'${ECOSYS_CSP_CONNECT_SRC}`, so supply the
    # separating space here rather than relying on the operator to include one.
    ECOSYS_CSP_CONNECT_SRC=" $value"
    log "connect-src 'self'$ECOSYS_CSP_CONNECT_SRC"
else
    ECOSYS_CSP_CONNECT_SRC=""
    log "connect-src 'self' (ECOSYS_CSP_CONNECT_SRC unset)"
fi
export ECOSYS_CSP_CONNECT_SRC

envsubst '${ECOSYS_CSP_CONNECT_SRC}' < "$TEMPLATE" > "$OUTPUT"

# A config this container cannot parse should fail here, with a readable message, rather
# than as an nginx start-up crash loop.
nginx -t -q || die "the rendered $OUTPUT is not valid nginx configuration."
