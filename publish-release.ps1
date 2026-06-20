# UNKCLUB Tool — maintainer release helper (pack bundle, checklist, open GitHub release page).
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $repoRoot 'PreInstallTool\PreInstallTool.csproj'
$versionJson = Join-Path $repoRoot 'version.json'

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
}

[xml]$csproj = Get-Content -LiteralPath $projectFile
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Version not found in PreInstallTool.csproj'
}

$tag = "v$version"
$bundleScript = Join-Path $repoRoot 'PreInstallTool\pack-installers-bundle.ps1'

Write-Host ''
Write-Host '=== UNKCLUB Tool release checklist ===' -ForegroundColor Cyan
Write-Host "Version : $version"
Write-Host "Tag     : $tag"
Write-Host ''
Write-Host '[ ] PreInstallTool.csproj, version.json, app.manifest versions match'
Write-Host '[ ] Kurulum dosyalari/ payloads present (except UNKCLUB.exe in bundle)'
Write-Host '[ ] dotnet build Release passes'
Write-Host '[ ] Commit and push master'
Write-Host "[ ] git tag $tag && git push origin $tag"
Write-Host '[ ] Wait for CI to publish UNKCLUB Tool.exe'
Write-Host "[ ] Upload installers-bundle.zip and UNKCLUB.exe:"
Write-Host "    gh release upload $tag PreInstallTool/installers-bundle.zip UNKCLUB.exe --clobber"
Write-Host ''

Write-Host 'Packing installers-bundle.zip...' -ForegroundColor Yellow
& $bundleScript

$bundlePath = Join-Path $repoRoot 'PreInstallTool\installers-bundle.zip'
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "Bundle not found: $bundlePath"
}

$bundleSizeMb = [math]::Round((Get-Item -LiteralPath $bundlePath).Length / 1MB, 2)
Write-Host "Bundle ready: $bundlePath ($bundleSizeMb MB)" -ForegroundColor Green

if (Test-Path -LiteralPath $versionJson) {
    $manifest = Get-Content -LiteralPath $versionJson -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version) {
        Write-Warning "version.json ($($manifest.version)) does not match csproj ($version)."
    }
}

$releaseUrl = 'https://github.com/Unkclub777/UNKCLUB-Tool/releases/new?tag=' + [uri]::EscapeDataString($tag)
Write-Host "Opening release page: $releaseUrl" -ForegroundColor Cyan
Start-Process $releaseUrl
