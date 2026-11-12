$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$workspaceDataDir = Join-Path $repoRoot 'backend/.azurite'
$existingDataDir = Join-Path $env:LOCALAPPDATA 'RoomManagement/azurite'
$dataDir = if (Test-Path -LiteralPath (Join-Path $existingDataDir '__azurite_db_blob__.json')) {
    $existingDataDir
} else {
    $workspaceDataDir
}
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

function Test-BlobPort {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $attempt = $client.BeginConnect('127.0.0.1', 10000, $null, $null)
        if (-not $attempt.AsyncWaitHandle.WaitOne(500)) { return $false }
        $client.EndConnect($attempt)
        return $true
    }
    catch { return $false }
    finally { $client.Dispose() }
}

if (-not (Test-BlobPort)) {
    $azurite = (Get-Command azurite.cmd -ErrorAction Stop).Source
    Start-Process -FilePath $azurite -ArgumentList @('--silent', '--location', ('"' + $dataDir + '"')) -WindowStyle Hidden | Out-Null
    for ($attempt = 0; $attempt -lt 20 -and -not (Test-BlobPort); $attempt++) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-BlobPort)) { throw 'Azurite could not start on port 10000.' }
}

$azureCli = (Get-Command az.cmd -ErrorAction Stop).Source
$previousConfigDir = $env:AZURE_CONFIG_DIR
$env:AZURE_CONFIG_DIR = Join-Path $workspaceDataDir 'azure-cli'
New-Item -ItemType Directory -Path $env:AZURE_CONFIG_DIR -Force | Out-Null
try {
    & $azureCli storage container create --name room-images --connection-string 'UseDevelopmentStorage=true' --auth-mode key --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the room-images container in Azurite.' }
}
finally {
    $env:AZURE_CONFIG_DIR = $previousConfigDir
}

Write-Output 'Local image storage is ready: Azurite and room-images container.'
