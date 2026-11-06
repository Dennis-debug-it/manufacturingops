# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer-cached): copy shared props + every project file, keeping paths.
COPY Directory.Build.props ./
COPY src/Ecosys.Api/Ecosys.Api.csproj                       src/Ecosys.Api/
COPY src/Ecosys.Domain/Ecosys.Domain.csproj                src/Ecosys.Domain/
COPY src/Ecosys.Infrastructure/Ecosys.Infrastructure.csproj src/Ecosys.Infrastructure/
COPY src/Ecosys.Shared/Ecosys.Shared.csproj                src/Ecosys.Shared/
RUN dotnet restore src/Ecosys.Api/Ecosys.Api.csproj

# Copy the rest of the source and publish.
COPY src/ src/
RUN dotnet publish src/Ecosys.Api/Ecosys.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the compose healthcheck to hit /health; tzdata backs the configurable
# workspace time zone (TimeZoneInfo.FindSystemTimeZoneById) - without it zones resolve to UTC.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000 \
    Storage__LocalBasePath=/app/storage/uploads

COPY --from=build /app/publish ./

# Both of these paths are mount points for named volumes (see docker-compose.yml):
#   /app/storage/uploads   attachments (Storage__LocalBasePath)
#   /app/wwwroot/uploads   tenant branding logos, written by TenantController (EC-P2-12)
# Creating and chowning them at BUILD time is what makes running as non-root work:
# Docker copies the image's content *and its ownership* into a named volume the first
# time that volume is initialised, so the volume comes up owned by `app` rather than
# root. Deployments whose volumes already exist need a one-time chown - see
# "Upgrading an existing deployment" in docs/DEPLOYMENT.md.
RUN mkdir -p /app/storage/uploads /app/wwwroot/uploads/logos \
    && chown -R app:app /app/storage /app/wwwroot

# EC-P1-06b: run as the image's non-root `app` user (uid 1654). Port 5000 is
# unprivileged, so nothing in this container needs root.
USER app

EXPOSE 5000
ENTRYPOINT ["dotnet", "Ecosys.Api.dll"]
