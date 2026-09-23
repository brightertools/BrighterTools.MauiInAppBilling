# Requires PowerShell 7 and the SDK in global.json.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolchain = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') | ConvertFrom-Json
Push-Location $root
try {
    if ((dotnet --version) -ne $toolchain.sdk) { throw "Install .NET SDK $($toolchain.sdk) first." }
    if (!$IsWindows -and !$IsMacOS) { throw 'Platform builds require Windows or macOS. Portable tests require only the SDK.' }
    dotnet workload install maui --version $toolchain.workloadSet
    if ($LASTEXITCODE -ne 0) { throw 'Workload installation failed.' }
    dotnet restore ./BrighterTools.MauiInAppBilling.slnx --configfile ./NuGet.config
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
} finally { Pop-Location }
