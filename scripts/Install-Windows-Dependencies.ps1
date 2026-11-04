[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$web = Join-Path $repo 'UI\Web'
$settingsPath = Join-Path $PSScriptRoot 'fork-build.local.json'

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program" }
}

function Get-CompatibleNode {
    $command = Get-Command node.exe -ErrorAction SilentlyContinue
    if (-not $command) { return $null }
    $versionText = & $command.Source --version
    if ($LASTEXITCODE -ne 0 -or $versionText -notmatch '^v(\d+\.\d+\.\d+)') { return $null }
    $version = [version]$Matches[1]
    if (($version -ge [version]'22.22.3' -and $version -lt [version]'23.0.0') -or
        ($version -ge [version]'24.15.0' -and $version -lt [version]'25.0.0') -or
        $version -ge [version]'26.0.0') {
        $executable = & $command.Source -p 'process.execPath'
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $executable)) { return $executable }
    }
    return $null
}

function Get-CompatibleDotnet {
    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $command) { return $null }
    $sdks = & $command.Source --list-sdks
    if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^10\.\d+\.\d+\s' })) {
        $runtimes = & $command.Source --list-runtimes
        $coreRuntime = $runtimes | Where-Object { $_ -match '^Microsoft\.NETCore\.App 10\..*\[(.+)\]' } | Select-Object -First 1
        if ($coreRuntime -match '\[(.+)\]') {
            $root = Split-Path -Parent (Split-Path -Parent $Matches[1])
            $executable = Join-Path $root 'dotnet.exe'
            if (Test-Path -LiteralPath $executable) { return $executable }
        }
    }
    return $null
}

function Install-ScoopApp([string]$Name, [string]$Exe) {
    if (-not (Get-Command scoop -ErrorAction SilentlyContinue)) {
        throw "Scoop is required to install $Name. Install Scoop first, then rerun setup."
    }
    Write-Host "Installing $Name with Scoop..."
    Invoke-Checked 'scoop' @('install', $Name) | Out-Host
    $prefix = & scoop prefix ($Name -replace '^.*/', '')
    if ($LASTEXITCODE -ne 0 -or -not $prefix) { throw "Could not find the installed $Name directory." }
    $path = Join-Path ($prefix | Select-Object -Last 1) $Exe
    if (-not (Test-Path -LiteralPath $path)) { throw "Installed executable not found: $path" }
    return $path
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required. Install it with Scoop using: scoop install git'
}
if (Test-Path -LiteralPath $settingsPath) {
    throw "Local build settings already exist at $settingsPath. They have not been replaced. Remove or edit them yourself before rerunning setup."
}

$node = Get-CompatibleNode
if (-not $node) { $node = Install-ScoopApp 'nodejs-lts' 'node.exe' }
$dotnet = Get-CompatibleDotnet
if (-not $dotnet) {
    if (-not (Get-Command scoop -ErrorAction SilentlyContinue)) {
        throw 'Scoop is required to install the .NET 10 SDK.'
    }
    $scoopRoot = if ($env:SCOOP) { $env:SCOOP } else { Join-Path $env:USERPROFILE 'scoop' }
    if (-not (Test-Path -LiteralPath (Join-Path $scoopRoot 'buckets\versions'))) {
        Invoke-Checked 'scoop' @('bucket', 'add', 'versions')
    }
    $dotnet = Install-ScoopApp 'versions/dotnet10-sdk' 'dotnet.exe'
}
$npm = Join-Path (Split-Path -Parent $node) 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path -LiteralPath $npm)) { throw "npm was not found alongside Node at $npm" }
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$(Split-Path -Parent $node);$env:DOTNET_ROOT;$env:PATH"

Push-Location $web
try { Invoke-Checked $node @($npm, 'ci', '--legacy-peer-deps') }
finally { Pop-Location }
Push-Location $repo
try { Invoke-Checked $dotnet @('restore', 'Kavita.Server\Kavita.Server.csproj') }
finally { Pop-Location }

@{ DotnetPath = $dotnet; NodePath = $node } | ConvertTo-Json |
    Set-Content -LiteralPath $settingsPath -Encoding UTF8
Write-Host "`nDependencies are ready. Run Build Kavita Fork.bat to build your first package."
