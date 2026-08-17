#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf artifacts/publish/osx-arm64
mkdir -p artifacts/publish/osx-arm64

dotnet publish src/Tsumugi.App \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o artifacts/publish/osx-arm64
