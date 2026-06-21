param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

$configPath = Join-Path $RepoRoot 'PreInstallTool\install-config.json'
$versionPath = Join-Path $RepoRoot 'version.json'

Assert-True (Test-Path -LiteralPath $configPath) "Missing install-config.json at $configPath"
Assert-True (Test-Path -LiteralPath $versionPath) "Missing version.json at $versionPath"

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
Assert-True ($null -ne $config.modes) 'install-config.json must define modes'
Assert-True ($config.modes.Count -ge 2) 'install-config.json must define at least two modes'

$requiredModes = @('first-install', 'error-fix')
foreach ($modeId in $requiredModes) {
    $mode = $config.modes | Where-Object { $_.id -eq $modeId }
    Assert-True ($null -ne $mode) "Missing required mode: $modeId"
    Assert-True ($mode.steps.Count -gt 0) "Mode '$modeId' must contain steps"
}

$manifest = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
foreach ($field in @('version', 'downloadUrl', 'installersBundleUrl', 'installersBundleVersion')) {
    Assert-True (-not [string]::IsNullOrWhiteSpace($manifest.$field)) "version.json missing required field: $field"
}

Assert-True ($manifest.downloadUrl -like '*UNKCLUB*Tool.exe*') 'downloadUrl should reference UNKCLUB Tool.exe'

Write-Host 'smoke-test.ps1 passed.'
