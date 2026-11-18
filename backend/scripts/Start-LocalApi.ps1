$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$apiProject = Join-Path $repoRoot 'backend/src/RoomManagement.API'
$launchSettings = Get-Content -LiteralPath (Join-Path $apiProject 'Properties/launchSettings.json') -Raw | ConvertFrom-Json
foreach ($url in ($launchSettings.profiles.http.applicationUrl -split ';')) {
    $port = ([uri]$url).Port
    $listener = netstat -ano | Select-String -Pattern "^\s*TCP\s+(?:127\.0\.0\.1|0\.0\.0\.0|\[::1\]|\[::\]):${port}\s+\S+\s+LISTENING\s+(\d+)\s*$" |
        Select-Object -First 1
    if ($listener) {
        $ownerId = [int]$listener.Matches[0].Groups[1].Value
        $owner = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        $apiBin = (Join-Path $apiProject 'bin') + [IO.Path]::DirectorySeparatorChar
        if ($owner -and $owner.Path -and
            ([IO.Path]::GetFileName($owner.Path) -ieq 'RoomManagement.API.exe') -and
            $owner.Path.StartsWith($apiBin, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Output "Local API is already running at $url (PID $ownerId)."
            exit 0
        }
        $ownerLabel = if ($owner) { "$($owner.ProcessName), PID $ownerId" } else { "PID $ownerId" }
        [Console]::Error.WriteLine("Local API port $port is already in use ($ownerLabel). Stop the process using this port before running this script again.")
        exit 1
    }
}

& (Join-Path $PSScriptRoot 'Start-LocalImageStorage.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw 'Local image storage could not start.'
}

dotnet run --project $apiProject --launch-profile http
exit $LASTEXITCODE
