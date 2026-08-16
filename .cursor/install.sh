#!/usr/bin/env bash
# Idempotent Cloud Agent setup for DevHub (Angular frontend + .NET 8 backend).
# Safe to run repeatedly; installs the .NET 8 SDK if missing, then restores
# dependencies for both apps.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> Ensuring .NET 8 SDK is installed"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  sudo apt-get update -qq
  sudo apt-get install -y --no-install-recommends dotnet-sdk-8.0
fi
dotnet --version

echo "==> Disabling Angular CLI analytics prompt (non-interactive)"
export NG_CLI_ANALYTICS=false

echo "==> Restoring frontend dependencies (npm ci)"
cd "$REPO_ROOT/email-composer-frontend"
npm ci

echo "==> Restoring backend dependencies (dotnet restore)"
cd "$REPO_ROOT/email-composer-backend"
dotnet restore

echo "==> DevHub install complete"
