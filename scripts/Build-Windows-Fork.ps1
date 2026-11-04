[CmdletBinding()]
param(
    [string]$DotnetPath,
    [string]$NodePath,
    [switch]$SkipFrontend,
    [switch]$NoRestore,
    [switch]$ForceRestore
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$web = Join-Path $repo 'UI\Web'
$localSettingsPath = Join-Path $PSScriptRoot 'fork-build.local.json'
if (Test-Path -LiteralPath $localSettingsPath) {
    $localSettings = Get-Content -LiteralPath $localSettingsPath -Raw | ConvertFrom-Json
    if (-not $DotnetPath) { $DotnetPath = $localSettings.DotnetPath }
    if (-not $NodePath) { $NodePath = $localSettings.NodePath }
    if ($localSettings.DotnetCliHome) { $env:DOTNET_CLI_HOME = $localSettings.DotnetCliHome }
    if ($localSettings.NugetPackages) { $env:NUGET_PACKAGES = $localSettings.NugetPackages }
    if ($localSettings.NoRestore -and -not $ForceRestore) { $NoRestore = $true }
}
if (-not $DotnetPath) { $DotnetPath = 'dotnet' }
if (-not $NodePath) { $NodePath = 'node' }
$dotnet = (Get-Command $DotnetPath -ErrorAction Stop).Source
$node = (Get-Command $NodePath -ErrorAction Stop).Source
$dotnetRoot = Split-Path -Parent $dotnet
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$(Split-Path -Parent $node);$dotnetRoot;$env:PATH"
$outputRoot = Join-Path $repo '_output'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$package = Join-Path $outputRoot "Kavita-Fork-$stamp"
$publish = Join-Path $outputRoot "fork-publish-$stamp"

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program" }
}

function Get-LatestRuntimeDirectory([string]$Path) {
    $version = Get-ChildItem -LiteralPath $Path -Directory -ErrorAction Stop |
        Where-Object { $_.Name -match '^10\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $version) { throw "A .NET 10 runtime was not found in $Path" }
    return $version.FullName
}

if (Test-Path -LiteralPath $package) { throw "Package already exists: $package" }
if (Test-Path -LiteralPath $publish) { throw "Build directory already exists: $publish" }
if (-not (Test-Path -LiteralPath (Join-Path $web 'node_modules\@angular\cli\bin\ng.js'))) {
    throw 'Frontend dependencies are missing. Run npm ci in UI\Web first.'
}

$coreRuntime = Get-LatestRuntimeDirectory (Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App')
$aspRuntime = Get-LatestRuntimeDirectory (Join-Path $dotnetRoot 'shared\Microsoft.AspNetCore.App')
$hostRuntime = Get-LatestRuntimeDirectory (Join-Path $dotnetRoot 'host\fxr')

Push-Location $repo
try {
    if (-not $SkipFrontend) {
        Push-Location $web
        try {
            Invoke-Checked $node @('hash-localization.js')
            Invoke-Checked $node @('node_modules\@angular\cli\bin\ng.js', 'build', '--configuration', 'production')
            Invoke-Checked $node @('minify-json.js')
        } finally { Pop-Location }
    }
    $browser = Join-Path $web 'dist\browser'
    if (-not (Test-Path -LiteralPath (Join-Path $browser 'index.html'))) {
        throw 'No frontend build found. Run without -SkipFrontend.'
    }

    $publishArgs = @('publish', 'Kavita.Server\Kavita.Server.csproj', '-c', 'Release', '--output', $publish)
    if ($NoRestore) { $publishArgs += '--no-restore' }
    Invoke-Checked $dotnet $publishArgs

    New-Item -ItemType Directory -Path $package | Out-Null
    Get-ChildItem -LiteralPath $publish -Force | Where-Object { $_.Name -notin @('config', 'wwwroot') } |
        Copy-Item -Destination $package -Recurse -Force
    New-Item -ItemType Directory -Path (Join-Path $package 'wwwroot') | Out-Null
    Copy-Item -Path (Join-Path $browser '*') -Destination (Join-Path $package 'wwwroot') -Recurse -Force

    $privateDotnet = Join-Path $package 'dotnet'
    New-Item -ItemType Directory -Path $privateDotnet | Out-Null
    Copy-Item -LiteralPath $dotnet -Destination (Join-Path $privateDotnet 'dotnet.exe')
    foreach ($entry in @(
        @{ Source = $coreRuntime; Target = 'shared\Microsoft.NETCore.App' },
        @{ Source = $aspRuntime; Target = 'shared\Microsoft.AspNetCore.App' },
        @{ Source = $hostRuntime; Target = 'host\fxr' }
    )) {
        $destination = Join-Path $privateDotnet $entry.Target
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath $entry.Source -Destination $destination -Recurse
    }
    foreach ($name in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
        $source = Join-Path $dotnetRoot $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $privateDotnet }
    }

    $config = Join-Path $package 'config'
    New-Item -ItemType Directory -Path $config | Out-Null
    $settings = Get-Content -LiteralPath (Join-Path $repo 'Kavita.Server\config\appsettings.json') -Raw | ConvertFrom-Json
    $settings.Port = 5051
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $config 'appsettings-init.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Kavita-Fork.bat') -Destination (Join-Path $package 'Start Kavita Fork.bat')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Windows-Fork-README.txt') -Destination (Join-Path $package 'README.txt')
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $package 'LICENSE.txt')
    Write-Host "`nPackage ready: $package"
} finally { Pop-Location }
