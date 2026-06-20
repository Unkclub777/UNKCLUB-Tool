# Creates embedded-bundle.zip via MSBuild (Unicode-safe; avoids path encoding issues in cmd).
$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir 'PreInstallTool.csproj'

dotnet msbuild $projectFile /t:PackEmbeddedResources /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /v:minimal

$bundlePath = Join-Path $projectDir 'embedded-bundle.zip'
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "embedded-bundle.zip olusturulamadi: $bundlePath"
}

Write-Host "embedded-bundle.zip hazir: $bundlePath ($((Get-Item -LiteralPath $bundlePath).Length) bytes)"
