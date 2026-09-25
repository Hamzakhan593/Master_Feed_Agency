[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$BackupRoot,

    [string]$OffsiteDirectory = "",

    [int]$DailyRetentionDays = 14,
    [int]$WeeklyRetentionWeeks = 8,
    [int]$MonthlyRetentionMonths = 12,

    [string]$SqlCmdPath = "sqlcmd"
)

$ErrorActionPreference = "Stop"

function Invoke-SqlCmdStrict {
    param([string]$Query)

    & $SqlCmdPath -S $Server -d master -E -b -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed with exit code $LASTEXITCODE."
    }
}

function Remove-OldBackupFiles {
    param([string]$Path, [datetime]$OlderThan)

    if (-not (Test-Path $Path)) { return }

    Get-ChildItem -Path $Path -Filter "*.bak" -File |
        Where-Object { $_.LastWriteTime -lt $OlderThan } |
        Remove-Item -Force
}

$dailyDir = Join-Path $BackupRoot "Daily"
$weeklyDir = Join-Path $BackupRoot "Weekly"
$monthlyDir = Join-Path $BackupRoot "Monthly"

New-Item -ItemType Directory -Force -Path $dailyDir, $weeklyDir, $monthlyDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$safeDatabase = ($Database -replace '[^A-Za-z0-9_.-]', '_')
$backupFile = Join-Path $dailyDir "${safeDatabase}_${timestamp}.bak"
$sqlPath = $backupFile.Replace("'", "''")
$sqlDatabase = $Database.Replace("]", "]]" )

Write-Host "Creating SQL Server backup: $backupFile"

$backupSql = @"
BACKUP DATABASE [$sqlDatabase]
TO DISK = N'$sqlPath'
WITH COPY_ONLY, INIT, CHECKSUM, STATS = 10;
RESTORE VERIFYONLY
FROM DISK = N'$sqlPath'
WITH CHECKSUM;
"@

Invoke-SqlCmdStrict -Query $backupSql

if (-not (Test-Path $backupFile)) {
    throw "SQL Server reported success but the backup file is not visible at '$backupFile'. Ensure BackupRoot is a local/UNC path accessible to both SQL Server and this task."
}

$now = Get-Date

# Keep a separate weekly copy on Sunday.
if ($now.DayOfWeek -eq [DayOfWeek]::Sunday) {
    Copy-Item $backupFile (Join-Path $weeklyDir (Split-Path $backupFile -Leaf)) -Force
}

# Keep a separate monthly copy on the first calendar day of the month.
if ($now.Day -eq 1) {
    Copy-Item $backupFile (Join-Path $monthlyDir (Split-Path $backupFile -Leaf)) -Force
}

# Optional second copy on another disk/network share. This is intentionally a copy,
# not the primary SQL Server backup target.
if (-not [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    New-Item -ItemType Directory -Force -Path $OffsiteDirectory | Out-Null
    Copy-Item $backupFile (Join-Path $OffsiteDirectory (Split-Path $backupFile -Leaf)) -Force
}

Remove-OldBackupFiles -Path $dailyDir -OlderThan $now.AddDays(-$DailyRetentionDays)
Remove-OldBackupFiles -Path $weeklyDir -OlderThan $now.AddDays(-7 * $WeeklyRetentionWeeks)
Remove-OldBackupFiles -Path $monthlyDir -OlderThan $now.AddMonths(-$MonthlyRetentionMonths)

Write-Host "Backup and RESTORE VERIFYONLY completed successfully."
Write-Host "Backup file: $backupFile"
