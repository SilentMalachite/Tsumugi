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

    # 単一ファイル発行でもネイティブライブラリはサイドカーとして残り、NOTICE と
    # NotoSansJP.LICENSE.txt も実行ファイルの隣に出力される。実行ファイルだけを
    # コピーすると起動に失敗するか、ライセンス欠落のまま配布される。
    Write-Host ""
    Write-Host "発行完了: $output/"
    Write-Host "配布するときは、実行ファイル単体ではなくこのディレクトリごとコピーしてください。"
}
finally {
    Pop-Location
}
