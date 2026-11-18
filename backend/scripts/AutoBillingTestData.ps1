[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('seed', 'status', 'reset', 'refresh')]
    [string]$Action = 'status'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = 'backend/tools/AutoBillingTestSeeder'

Push-Location -LiteralPath $repositoryRoot
try {
    if ($Action -eq 'refresh') {
        dotnet run --project $projectPath -- reset
        if ($LASTEXITCODE -ne 0) { throw 'AutoBillingTestSeeder reset failed; seed was not started.' }
        dotnet run --project $projectPath -- seed
        if ($LASTEXITCODE -ne 0) { throw 'AutoBillingTestSeeder seed failed.' }
    }
    else {
        dotnet run --project $projectPath -- $Action
        if ($LASTEXITCODE -ne 0) { throw "AutoBillingTestSeeder $Action failed." }
    }
}
finally {
    Pop-Location
}
