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
$sedPath = Join-Path $PSScriptRoot 'WowRunner-Setup.sed'
$target = Join-Path $dist 'WowRunner-Setup.exe'

Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $staging, $dist | Out-Null

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

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.cmd') -Destination $staging
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'create-shortcut.vbs') -Destination $staging

$sourceFileLines = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $staging -File) {
    $sourceFileLines.Add("$($file.Name)=")
}

$sedLines = [System.Collections.Generic.List[string]]::new()
$sedLines.Add('[Version]')
$sedLines.Add('Class=IEXPRESS')
$sedLines.Add('SEDVersion=3')
$sedLines.Add('[Options]')
$sedLines.Add('PackagePurpose=InstallApp')
$sedLines.Add('ShowInstallProgramWindow=1')
$sedLines.Add('HideExtractAnimation=1')
$sedLines.Add('UseLongFileName=1')
$sedLines.Add('InsideCompressed=1')
$sedLines.Add('CAB_FixedSize=0')
$sedLines.Add('CAB_ResvCodeSigning=0')
$sedLines.Add('ReinstallMode=0')
$sedLines.Add("TargetName=$target")
$sedLines.Add('FriendlyName=WowRunner')
$sedLines.Add('AppLaunched=cmd.exe /d /c install.cmd')
$sedLines.Add('AdminQuietInstCmd=cmd.exe /d /c install.cmd')
$sedLines.Add('PostInstallCmd=<None>')
$sedLines.Add('SourceFiles=SourceFiles')
$sedLines.Add('[SourceFiles]')
$sedLines.Add("SourceFiles0=$staging")
$sedLines.Add('[SourceFiles0]')
foreach ($line in $sourceFileLines) {
    $sedLines.Add($line)
}

[System.IO.File]::WriteAllText($sedPath, ($sedLines -join [Environment]::NewLine), [System.Text.Encoding]::ASCII)
& iexpress.exe /N /Q $sedPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$deadline = [DateTime]::UtcNow.AddSeconds(300)
while (-not (Test-Path -LiteralPath $target -PathType Leaf) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw 'IExpress 未生成预期的安装包。'
}

Write-Output "安装包已生成：$target"
