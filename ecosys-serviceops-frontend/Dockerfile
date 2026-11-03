# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite bakes this in at build time. Point it at the public origin; nginx below
# proxies /api, /hubs and /files on that same origin to the API container.
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# ---- Runtime stage ----
FROM nginx:1.27-alpine AS runtime

# nginx.conf is a template, not a finished config: 10-ecosys-csp.sh renders it into
# /etc/nginx/conf.d/default.conf at container start so the CSP's connect-src can name this
# deployment's API origin instead of a hardcoded vendor domain (EC-P3-05b).
#
# It deliberately does NOT go in /etc/nginx/templates, the directory the image's own
# 20-envsubst-on-templates.sh processes: that script substitutes every environment
# variable it finds, which would destroy nginx's $host/$uri/$remote_addr. Ours passes
# envsubst a one-variable allow-list instead.
COPY nginx.conf /etc/nginx/ecosys/default.conf.template
COPY --chmod=0755 docker-entrypoint.d/10-ecosys-csp.sh /docker-entrypoint.d/10-ecosys-csp.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
