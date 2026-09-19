#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
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
    '--output', $staging
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
    "-p:PayloadPath=$payload"
)
& dotnet @setupArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw '未生成预期的 WowRunner-Setup.exe。'
}

Remove-Item -LiteralPath $payload -Force -ErrorAction SilentlyContinue
Write-Output "安装包已生成：$target"
