# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer-cached): copy shared props + every project file, keeping paths.
COPY Directory.Build.props ./
COPY src/Ecosys.Api/Ecosys.Api.csproj                       src/Ecosys.Api/
COPY src/Ecosys.Application/Ecosys.Application.csproj       src/Ecosys.Application/
COPY src/Ecosys.Domain/Ecosys.Domain.csproj                src/Ecosys.Domain/
COPY src/Ecosys.Infrastructure/Ecosys.Infrastructure.csproj src/Ecosys.Infrastructure/
COPY src/Ecosys.Platform/Ecosys.Platform.csproj            src/Ecosys.Platform/
COPY src/Ecosys.ServiceOps/Ecosys.ServiceOps.csproj        src/Ecosys.ServiceOps/
COPY src/Ecosys.Shared/Ecosys.Shared.csproj                src/Ecosys.Shared/
RUN dotnet restore src/Ecosys.Api/Ecosys.Api.csproj

# Copy the rest of the source and publish.
COPY src/ src/
RUN dotnet publish src/Ecosys.Api/Ecosys.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the compose healthcheck to hit /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as root so the mounted uploads volume is writable without extra chown steps.
# (Switch to the image's non-root `app` user once volume permissions are set up.)
USER root

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000 \
    Storage__LocalBasePath=/app/storage/uploads

RUN mkdir -p /app/storage/uploads
COPY --from=build /app/publish ./

EXPOSE 5000
ENTRYPOINT ["dotnet", "Ecosys.Api.dll"]
