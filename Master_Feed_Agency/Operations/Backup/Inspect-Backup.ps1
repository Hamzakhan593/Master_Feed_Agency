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
$query = "RESTORE FILELISTONLY FROM DISK = N'$sqlPath';"

& $SqlCmdPath -S $Server -d master -E -b -Q $query
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect backup. sqlcmd exit code: $LASTEXITCODE."
}
