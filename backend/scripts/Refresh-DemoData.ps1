$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
Push-Location -LiteralPath $repositoryRoot
try {
    dotnet run --project backend/tools/DemoDataTool -- reset
    if ($LASTEXITCODE -ne 0) { throw 'Demo reset failed; seed was not started.' }
    dotnet run --project backend/tools/DemoDataTool -- seed
    if ($LASTEXITCODE -ne 0) { throw 'Demo seed failed.' }
}
finally {
    Pop-Location
}
