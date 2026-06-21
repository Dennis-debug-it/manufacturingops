# Ecosys ServiceOps — Manual cleanup steps
# Run this script from the repo root on Windows.
# These deletions could not be performed by the Linux sandbox due to NTFS permissions.

Write-Host "=== Ecosys Manual Cleanup ===" -ForegroundColor Cyan

# 1. Remove the stale Next.js frontend skeleton
# This directory is excluded from Git via backend/.gitignore (added 2026-06-03).
# Deleting it removes ~250 MB of stale node_modules and a spec that targets an old API.
$staleDir = Join-Path $PSScriptRoot "backend\frontend"
if (Test-Path $staleDir) {
    Write-Host "Removing stale legacy frontend at $staleDir ..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $staleDir
    Write-Host "  Done." -ForegroundColor Green
} else {
    Write-Host "backend\frontend\ already gone — skipping." -ForegroundColor Gray
}

# 2. Remove the stale old-domain Hosted service stubs
# Both files are wrapped in #if NEVER so they do not compile, but they should be deleted
# to avoid confusion. The active services are PmSchedulerWorker and SlaEnforcementWorker
# in Ecosys.Infrastructure\Services\.
$staleHosted = @(
    "backend\src\Ecosys.Infrastructure\Hosted\PreventiveMaintenanceHostedService.cs",
    "backend\src\Ecosys.Infrastructure\Hosted\SlaMonitorHostedService.cs"
)
foreach ($file in $staleHosted) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        Write-Host "Removing stale hosted stub: $file ..." -ForegroundColor Yellow
        Remove-Item -Force $fullPath
        Write-Host "  Done." -ForegroundColor Green
    } else {
        Write-Host "$file already gone — skipping." -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== Cleanup complete. Run 'git status' to confirm nothing tracked was lost. ===" -ForegroundColor Cyan
