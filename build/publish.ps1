$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..")
try {
    $output = "artifacts/publish/win-x64"
    if (Test-Path $output) {
        Remove-Item -Recurse -Force $output
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null

    dotnet publish src/Tsumugi.App `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -o $output

    # $ErrorActionPreference は Windows PowerShell 5.1 / PowerShell 7.0-7.2 では
    # ネイティブコマンドに適用されない。終了コードを明示的に検査しないと、
    # 失敗したビルドの空／半端な出力を成功として配布しうる（publish.sh の set -e と対称）。
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
