# Adds Windows Defender exclusions for build/publish output and project folders.
# Run before dotnet publish to reduce false positives during compilation and packaging.
# Requires elevation for machine-wide exclusions (Add-MpPreference).

param(
    [string]$ProjectRoot = $PSScriptRoot
)

$ErrorActionPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Get-Location).Path
}

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path

$paths = @(
    $ProjectRoot,
    (Join-Path $ProjectRoot 'PreInstallTool'),
    (Join-Path $ProjectRoot 'PreInstallTool\bin'),
    (Join-Path $ProjectRoot 'Kurulum dosyalari'),
    (Join-Path $ProjectRoot 'Kurulum dosyaları'),
    (Join-Path $ProjectRoot 'Dagitim'),
    (Join-Path $ProjectRoot 'Dagitim\UNKCLUB-Tool'),
    (Join-Path $ProjectRoot 'publish')
) | Sort-Object -Unique

$processes = @(
    'PreInstallTool.exe',
    'UNKCLUB Tool.exe',
    'UNKCLUB.exe'
)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Warning 'Yonetici yetkisi yok. Defender istisnalari kismen uygulanabilir veya atlanabilir.'
}

if (Get-Command Add-MpPreference -ErrorAction SilentlyContinue) {
    Add-MpPreference -ExclusionPath $paths -ErrorAction SilentlyContinue | Out-Null
    Add-MpPreference -ExclusionProcess $processes -ErrorAction SilentlyContinue | Out-Null
    Write-Host 'Defender istisnalari eklendi:'
    $paths | ForEach-Object { Write-Host "  $_" }
    $processes | ForEach-Object { Write-Host "  [process] $_" }
}
else {
    Write-Warning 'Add-MpPreference bulunamadi. Windows Defender yuklu olmayabilir.'
}

Get-ChildItem -Path (Join-Path $ProjectRoot 'PreInstallTool\bin') -Recurse -File -ErrorAction SilentlyContinue |
    Unblock-File -ErrorAction SilentlyContinue

Get-ChildItem -Path (Join-Path $ProjectRoot 'Kurulum dosyaları') -Recurse -File -ErrorAction SilentlyContinue |
    Unblock-File -ErrorAction SilentlyContinue

exit 0
