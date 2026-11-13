$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
& (Join-Path $PSScriptRoot 'Start-LocalImageStorage.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw 'Local image storage could not start.'
}

dotnet run --project (Join-Path $repoRoot 'backend/src/RoomManagement.API') --launch-profile http
