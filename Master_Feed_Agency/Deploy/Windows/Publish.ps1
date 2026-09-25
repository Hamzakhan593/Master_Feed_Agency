[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Output = ".\\publish"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\\..\\Master_Feed_Agency.csproj"
$project = [System.IO.Path]::GetFullPath($project)
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Output))

Write-Host "Publishing $project"
Write-Host "Output: $outputPath"

dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet publish $project -c $Configuration -o $outputPath --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Publish completed. Configure production secrets/environment on the server before starting the app."
