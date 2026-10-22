param(
    [string]$TaskTrackProject = (Join-Path $PSScriptRoot '..\..\TaskTrackerManagementSystem\TaskTrackerManagementSystem'),
    [string]$ItsWebUi = (Join-Path $PSScriptRoot '..\..\ITServiceDesk\ITServiceDesk.WebUI'),
    [string]$ApiBaseUrl = 'https://localhost:7232/',
    [string]$TaskTrackSsoUrl = 'https://localhost:7275/Security/Account/Sso',
    [string]$ItsSsoUrl = 'http://localhost:62100/Account/Sso'
)
# Development only: creates fresh SSO keys (docs/sso.md) and stores each one on both sides.
#  - Intranet API user secrets: ApplicationIntegrations:{TASKS|ITSERVICEDESK}:SsoKey and :SsoUrl
#  - TaskTrack user secrets: IntranetSso:Key
#  - ITS appSettings.local.config (outside git): IntranetSso:Key and IntranetSso:ApiBaseUrl
# Keys are never printed. Run it in your own terminal so Visual Studio sees the same user secrets.
$ErrorActionPreference = 'Stop'
function New-SsoKey {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return ([Convert]::ToBase64String($bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}
function Set-Secret([string]$Project, [string]$Name, [string]$Value) {
    dotnet user-secrets set $Name $Value --project $Project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet user-secrets failed for $Name in $Project" }
}

$api = Join-Path $PSScriptRoot '..\src\OrganizationIntranet.Api'
foreach ($path in $api, $TaskTrackProject, $ItsWebUi) { if (-not (Test-Path $path)) { throw "Not found: $path" } }
$taskTrackKey = New-SsoKey
$itsKey = New-SsoKey

Set-Secret $api 'ApplicationIntegrations:TASKS:SsoKey' $taskTrackKey
Set-Secret $api 'ApplicationIntegrations:TASKS:SsoUrl' $TaskTrackSsoUrl
Set-Secret $api 'ApplicationIntegrations:ITSERVICEDESK:SsoKey' $itsKey
Set-Secret $api 'ApplicationIntegrations:ITSERVICEDESK:SsoUrl' $ItsSsoUrl
Set-Secret $TaskTrackProject 'IntranetSso:Key' $taskTrackKey

$config = Join-Path $ItsWebUi 'appSettings.local.config'
if (Test-Path $config) { $xml = [xml](Get-Content -Raw -Encoding UTF8 $config) }
else { $xml = [xml]'<?xml version="1.0" encoding="utf-8"?><appSettings></appSettings>' }
foreach ($pair in @(@('IntranetSso:ApiBaseUrl', $ApiBaseUrl), @('IntranetSso:Key', $itsKey))) {
    $node = $xml.appSettings.add | Where-Object { $_.key -eq $pair[0] }
    if (-not $node) { $node = $xml.CreateElement('add'); $node.SetAttribute('key', $pair[0]); [void]$xml.appSettings.AppendChild($node) }
    $node.SetAttribute('value', $pair[1])
}
$xml.Save((Resolve-Path $ItsWebUi).Path + '\appSettings.local.config')

'SSO development keys stored for TASKS and ITSERVICEDESK (values not shown).'
'Restart the Intranet API, TaskTrack and ITS so they read the new keys.'
