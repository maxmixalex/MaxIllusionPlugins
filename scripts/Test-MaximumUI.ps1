[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $workspaceRoot 'src/MaximumUI.Tests/MaximumUI.Tests.csproj'

if (-not $NoBuild) {
    & dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "MaximumUI test build failed (exit $LASTEXITCODE)."
    }
}

$assemblyPath = Join-Path $workspaceRoot "bin/$Configuration/MaximumUI.Tests/MaximumUI.Tests.dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Test assembly not found: $assemblyPath"
}

# This existing harness is a class library with a static Main, not a test SDK project.
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$entry = $assembly.GetType('MaximumUI.Tests.Program').GetMethod(
    'Main', [Reflection.BindingFlags]'Public,NonPublic,Static')
if ($null -eq $entry) {
    throw 'MaximumUI test entry point was not found.'
}
$result = [int]$entry.Invoke($null, [object[]]@())
if ($result -ne 0) {
    throw "MaximumUI headless tests failed (exit $result)."
}
