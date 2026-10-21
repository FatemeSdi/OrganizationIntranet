# Starts Api, Portal and Admin for local development, each in its own PowerShell window.
# It uses the 'https' launch profile, which sets ASPNETCORE_ENVIRONMENT=Development; that is the only
# environment in which User Secrets (Api:ClientKey, Api:AdminClientKey, ConnectionStrings:DefaultConnection) are read.
# Works in Windows PowerShell 5.1 and PowerShell 7. Close a window to stop that application.
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($name in 'Api', 'Portal', 'Admin') {
    $project = Join-Path $root "src\OrganizationIntranet.$name"
    $command = "`$Host.UI.RawUI.WindowTitle = 'Intranet $name'; dotnet run --launch-profile https --project '$project'" + $(if ($NoBuild) { ' --no-build' } else { '' })
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $command
}
Write-Host 'Started Api (7232), Portal (7230) and Admin (7231). Close each window to stop it.'
