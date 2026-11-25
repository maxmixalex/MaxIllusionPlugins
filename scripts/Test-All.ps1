[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$Benchmark
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$previousBenchmark = $env:MAXIMUMUI_BENCHMARK
Push-Location $workspaceRoot
try {
    if ($Benchmark) { $env:MAXIMUMUI_BENCHMARK = '1' }
    if (-not $NoBuild) {
        & dotnet build MaxIllusionPlugins.sln -c $Configuration --nologo -m:1 -nodeReuse:false
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed (exit $LASTEXITCODE)." }
    }
    foreach ($suite in @('MaximumUI.Runner', 'MultiTeamH.Regression', 'AccessoryClothing.Regression', 'PluginCore.Regression')) {
        $command = @('run', '--project', "tests/$suite/$suite.csproj", '-c', $Configuration, '--no-build')
        if ($suite -eq 'MaximumUI.Runner') {
            $command += @('--', "bin/$Configuration/MaximumUI.Tests/MaximumUI.Tests.dll")
        }
        & dotnet @command
        if ($LASTEXITCODE -ne 0) { throw "$suite failed (exit $LASTEXITCODE)." }
    }
} finally {
    $env:MAXIMUMUI_BENCHMARK = $previousBenchmark
    Pop-Location
}
