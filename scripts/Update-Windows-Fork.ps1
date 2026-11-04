[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $PSScriptRoot 'fork-build.local.json'
if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw 'Run Install Build Dependencies.bat first.'
}
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$node = $settings.NodePath
if (-not $node -or -not (Test-Path -LiteralPath $node)) { throw 'Configured Node executable is missing. Check scripts\fork-build.local.json.' }
$npm = Join-Path (Split-Path -Parent $node) 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path -LiteralPath $npm)) { throw "npm not found: $npm" }
$env:PATH = "$(Split-Path -Parent $node);$env:PATH"

Push-Location $repo
try {
    $status = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Git checkout.' }
    if ($status) { throw 'The checkout has local changes. Commit or otherwise resolve them before updating.' }
    & git pull --ff-only
    if ($LASTEXITCODE -ne 0) { throw 'Git could not update this branch. No build was started.' }
    Push-Location (Join-Path $repo 'UI\Web')
    try {
        & $node $npm ci --legacy-peer-deps
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
    } finally { Pop-Location }
    & (Join-Path $PSScriptRoot 'Build-Windows-Fork.ps1') -ForceRestore
    Write-Host "`nThe new package is separate from your running installation."
    Write-Host 'Stop Kavita, back up its config folder, then copy that config into the new package before starting it.'
} finally { Pop-Location }
