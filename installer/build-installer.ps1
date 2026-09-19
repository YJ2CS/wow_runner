#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionPath = Join-Path $root 'version.json'
$versionDocument = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
$version = [string]$versionDocument.version
$versionParts = $version.Split('.')
$hasInvalidVersion = $versionParts.Count -ne 3
foreach ($part in $versionParts) {
    if ($part -notmatch '^[0-9]+$') {
        $hasInvalidVersion = $true
    }
}
if ($hasInvalidVersion) {
    throw "version.json 中的版本号无效：$version"
}

if ($Release) {
    $Configuration = 'Release'
    $nextPatch = ([int]$versionParts[2]) + 1
    $version = "$($versionParts[0]).$($versionParts[1]).$nextPatch"
    $versionDocument.version = $version
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($versionPath, (($versionDocument | ConvertTo-Json) + [Environment]::NewLine), $utf8NoBom)
}
$staging = Join-Path $PSScriptRoot 'staging'
$dist = Join-Path $PSScriptRoot 'dist'
$payload = Join-Path $PSScriptRoot 'payload.zip'
$target = Join-Path $dist 'WowRunner-Setup.exe'
$setupProject = Join-Path $PSScriptRoot 'WowRunner.Setup.csproj'

Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $dist -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $payload -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging, $dist | Out-Null

$publishArguments = @(
    'publish', (Join-Path $root 'WowRunner.csproj'),
    '--configuration', $Configuration,
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $staging,
    "-p:AppVersion=$version"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $payload -CompressionLevel Optimal

$setupArguments = @(
    'publish', $setupProject,
    '--configuration', $Configuration,
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $dist,
    "-p:PayloadPath=$payload",
    "-p:AppVersion=$version"
)
& dotnet @setupArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw '未生成预期的 WowRunner-Setup.exe。'
}

Remove-Item -LiteralPath $payload -Force -ErrorAction SilentlyContinue
Write-Output "版本：$version"
Write-Output "安装包已生成：$target"
