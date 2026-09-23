param([string]$PackageDirectory = (Join-Path $PSScriptRoot '../artifacts/packages'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$repository = 'https://github.com/brightertools/BrighterTools.MauiInAppBilling'
foreach ($id in @('BrighterTools.MauiInAppBilling.Abstractions', 'BrighterTools.MauiInAppBilling')) {
    $path = Join-Path $PackageDirectory "$id.$version.nupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($path))
    try {
        $required = @("$id.nuspec", 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')
        if ($id.EndsWith('.Abstractions')) {
            $required += "lib/net10.0/$id.dll"
        } else {
            $required += @('native/AppleBilling/Billing.swift', 'native/AppleBilling/build.sh',
                'buildTransitive/BrighterTools.MauiInAppBilling.targets')
            foreach ($platform in @('android', 'ios', 'maccatalyst', 'windows')) {
                if (!($archive.Entries.FullName | Where-Object { $_ -like "lib/net10.0-$platform*/$id.dll" })) {
                    throw "$id is missing its $platform assembly."
                }
            }
        }
        foreach ($entry in $required) {
            if (!$archive.GetEntry($entry)) { throw "$id is missing $entry." }
        }
        $reader = [IO.StreamReader]::new($archive.GetEntry("$id.nuspec").Open())
        try { $metadata = ([xml]$reader.ReadToEnd()).package.metadata } finally { $reader.Dispose() }
        if ($metadata.id -ne $id -or $metadata.version -ne $version -or
            $metadata.license.InnerText -ne 'MIT' -or $metadata.license.type -ne 'expression' -or
            $metadata.projectUrl -ne $repository -or $metadata.repository.url -ne $repository) {
            throw "$id has incorrect package metadata."
        }
        if ($archive.Entries.FullName | Where-Object { $_ -match '(?i)\.(pfx|p12|p8|pem|key|keystore|jks|mobileprovision|otf|ttf)$' }) {
            throw "$id contains an unexpected signing or font asset."
        }
        if (!(Test-Path (Join-Path $PackageDirectory "$id.$version.snupkg"))) {
            throw "$id is missing its symbols package."
        }
        Write-Host "Verified $id $version."
    } finally { $archive.Dispose() }
}
