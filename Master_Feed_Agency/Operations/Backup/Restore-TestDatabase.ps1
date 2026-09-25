[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [Parameter(Mandatory = $true)]
    [string]$LogicalDataName,

    [Parameter(Mandatory = $true)]
    [string]$LogicalLogName,

    [Parameter(Mandatory = $true)]
    [string]$DataFilePath,

    [Parameter(Mandatory = $true)]
    [string]$LogFilePath,

    [string]$TargetDatabase = "MasterFeedAgencyDb_RestoreTest",

    [switch]$ReplaceExistingTestDatabase,

    [string]$SqlCmdPath = "sqlcmd"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    throw "Backup file was not found: $BackupFile"
}

if ($TargetDatabase -eq "MasterFeedAgencyDb") {
    throw "Safety stop: this restore helper must not target the production database name. Use a dedicated restore-test database or test SQL Server."
}

$sqlPath = (Resolve-Path $BackupFile).Path.Replace("'", "''")
$db = $TargetDatabase.Replace("]", "]]" )
$logicalData = $LogicalDataName.Replace("'", "''")
$logicalLog = $LogicalLogName.Replace("'", "''")
$dataPath = $DataFilePath.Replace("'", "''")
$logPath = $LogFilePath.Replace("'", "''")
$replace = if ($ReplaceExistingTestDatabase) { ", REPLACE" } else { "" }
$replaceFlag = if ($ReplaceExistingTestDatabase) { 1 } else { 0 }
$targetLiteral = $TargetDatabase.Replace("'", "''")

$query = @"
IF DB_ID(N'$targetLiteral') IS NOT NULL AND $replaceFlag = 0
BEGIN
    THROW 50001, 'Target restore-test database already exists. Use a different TargetDatabase or explicitly pass -ReplaceExistingTestDatabase.', 1;
END;

IF DB_ID(N'$targetLiteral') IS NOT NULL AND $replaceFlag = 1
BEGIN
    ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;

RESTORE DATABASE [$db]
FROM DISK = N'$sqlPath'
WITH MOVE N'$logicalData' TO N'$dataPath',
     MOVE N'$logicalLog' TO N'$logPath',
     RECOVERY$replace;

ALTER DATABASE [$db] SET MULTI_USER;
"@

& $SqlCmdPath -S $Server -d master -E -b -Q $query
if ($LASTEXITCODE -ne 0) {
    throw "Restore test failed with exit code $LASTEXITCODE."
}

Write-Host "Restore test completed. Verify the restored database '$TargetDatabase' in SSMS, then remove it after testing."
