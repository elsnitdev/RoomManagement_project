[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('seed', 'cleanup')]
    [string]$Action,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Server,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Database,

    [string]$LandlordUsername = 'rm_demo_landlord',
    [string]$TenantUsername = 'rm_demo_tenant_d',
    [string]$SqlUser,
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
$sqlcmd = Get-Command sqlcmd -CommandType Application -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw 'sqlcmd was not found in PATH. Install SQL Server Command Line Utilities.'
}

function Set-SqlVariableLiteral([string]$Sql, [string]$Name, [string]$Value) {
    if ($Value -match '[\x00-\x1f]') {
        throw "The value of @$Name must not contain control characters."
    }
    $pattern = "(?m)(^DECLARE @${Name}\s+[^=\r\n]+?=\s*)N'[^']*';"
    $literal = "N'" + $Value.Replace("'", "''") + "';"
    if (-not [regex]::IsMatch($Sql, $pattern)) {
        throw "The SQL script is missing the declaration for @$Name."
    }
    return [regex]::Replace($Sql, $pattern, {
        param($match)
        $match.Groups[1].Value + $literal
    })
}

$fileName = if ($Action -eq 'seed') { 'seed-monthly-billing-test.sql' } else { 'cleanup-monthly-billing-test.sql' }
$sql = Get-Content -LiteralPath (Join-Path $PSScriptRoot $fileName) -Raw -Encoding UTF8
$sql = Set-SqlVariableLiteral $sql 'ExpectedDatabase' $Database
if ($Action -eq 'seed') {
    $sql = Set-SqlVariableLiteral $sql 'LandlordUsername' $LandlordUsername
    $sql = Set-SqlVariableLiteral $sql 'TenantUsername' $TenantUsername
}

$sqlArguments = @('-S', $Server, '-d', $Database, '-b', '-x', '-f', '65001')
if ($SqlUser) {
    $sqlArguments += @('-U', $SqlUser)
} else {
    $sqlArguments += '-E'
}
if ($TrustServerCertificate) { $sqlArguments += '-C' }

$inputFile = New-TemporaryFile
try {
    [System.IO.File]::WriteAllText($inputFile.FullName, $sql, [System.Text.UTF8Encoding]::new($false))
    & $sqlcmd.Source @sqlArguments -i $inputFile.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd exited with code $LASTEXITCODE while running '$Action'."
    }
} finally {
    Remove-Item -LiteralPath $inputFile.FullName
}
