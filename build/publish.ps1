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
}
finally {
    Pop-Location
}
