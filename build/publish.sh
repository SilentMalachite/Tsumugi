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

# 単一ファイル発行でもネイティブライブラリはサイドカーとして残り、NOTICE と
# NotoSansJP.LICENSE.txt も実行ファイルの隣に出力される。実行ファイルだけを
# コピーすると起動に失敗するか、ライセンス欠落のまま配布される。
echo
echo "発行完了: artifacts/publish/osx-arm64/"
echo "配布するときは、実行ファイル単体ではなくこのディレクトリごとコピーしてください。"
