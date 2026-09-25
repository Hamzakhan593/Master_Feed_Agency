[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$BackupRoot,

    [string]$OffsiteDirectory = "",

    [string]$TaskName = "MasterFeed-Daily-SqlBackup",

    [datetime]$At = ([datetime]::Today.AddHours(23).AddMinutes(30))
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
    throw "This helper registers a Windows Task Scheduler task and must be run on Windows."
}

$scriptPath = Join-Path $PSScriptRoot "Backup-Database.ps1"
if (-not (Test-Path $scriptPath)) {
    throw "Backup-Database.ps1 was not found next to this script."
}

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", ('"' + $scriptPath + '"'),
    "-Server", ('"' + $Server + '"'),
    "-Database", ('"' + $Database + '"'),
    "-BackupRoot", ('"' + $BackupRoot + '"')
)

if (-not [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    $arguments += @("-OffsiteDirectory", ('"' + $OffsiteDirectory + '"'))
}

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument ($arguments -join " ")
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Description "Master Feed Agency verified SQL Server backup" -Force | Out-Null

Write-Host "Scheduled task '$TaskName' registered for $($At.ToString('HH:mm')) daily."
Write-Host "Open Task Scheduler, review its Run As account and run it once manually before relying on it."
