#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> restore"
dotnet restore
echo "==> format verify (gate #2)"
dotnet format --verify-no-changes
echo "==> build warnings-as-errors (gate #1)"
dotnet build --no-restore -c Release
echo "==> test + coverage (gate #3, arch=gate#4, offline=gate#5)"
dotnet test --no-build -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
echo "==> CI OK"
