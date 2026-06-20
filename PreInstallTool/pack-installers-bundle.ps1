# Creates installers-bundle.zip for GitHub Release upload (Unicode-safe MSBuild target).
$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir 'PreInstallTool.csproj'

dotnet msbuild $projectFile /t:PackInstallersBundle /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /v:minimal

$bundlePath = Join-Path $projectDir 'installers-bundle.zip'
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "installers-bundle.zip olusturulamadi: $bundlePath"
}

Write-Host "installers-bundle.zip hazir: $bundlePath ($((Get-Item -LiteralPath $bundlePath).Length) bytes)"
