[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$SqlCmdPath = "sqlcmd"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    throw "Backup file was not found: $BackupFile"
}

$sqlPath = (Resolve-Path $BackupFile).Path.Replace("'", "''")
$query = "RESTORE VERIFYONLY FROM DISK = N'$sqlPath' WITH CHECKSUM;"

& $SqlCmdPath -S $Server -d master -E -b -Q $query
if ($LASTEXITCODE -ne 0) {
    throw "Backup verification failed with exit code $LASTEXITCODE."
}

Write-Host "Backup verification succeeded: $BackupFile"
