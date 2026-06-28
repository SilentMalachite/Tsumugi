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
echo "==> coverage threshold gate (gate #3 enforcement — floor=70%, raise over time; spec targets 100% in Phase 3)"
# Separate invocations avoid coverlet.collector / coverlet.msbuild conflict in the same run.
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Domain]*" \
  -p:Threshold=70 \
  -p:ThresholdType=line \
  -p:ThresholdStat=total
dotnet test tests/Tsumugi.Application.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Application]*" \
  -p:Threshold=70 \
  -p:ThresholdType=line \
  -p:ThresholdStat=total
echo "==> CI OK"
