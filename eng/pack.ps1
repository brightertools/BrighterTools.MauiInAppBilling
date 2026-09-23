param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'Pack on Windows to include all four platform assemblies. Apple native acceptance still requires a Mac.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    dotnet test tests/Billing.Tests/Billing.Tests.csproj -c $Configuration -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Billing tests failed.' }
    foreach ($project in @('BrighterTools.MauiInAppBilling.Abstractions', 'BrighterTools.MauiInAppBilling')) {
        dotnet pack "src/$project/$project.csproj" -c $Configuration -o artifacts/packages
        if ($LASTEXITCODE -ne 0) { throw "Packing $project failed." }
    }
    & (Join-Path $PSScriptRoot 'verify-packages.ps1')
} finally { Pop-Location }
