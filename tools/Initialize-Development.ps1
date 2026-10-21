# Creates the development client keys in the User Secrets of Api, Portal and Admin.
#   - Api:ClientKey      : one random key shared by all three applications.
#   - Api:AdminClientKey : a distinct random key shared by Api and Admin ONLY (never Portal).
# Existing valid keys are kept, so running it again changes nothing. Secrets are never printed.
# Works in Windows PowerShell 5.1 and PowerShell 7.
# IMPORTANT: run it from YOUR OWN terminal. A tool that runs in a sandboxed/packaged app (for example an AI assistant's shell)
# gets %APPDATA% redirected to a private folder, so Visual Studio would not see the files it writes.
param([switch]$UseExistingConnection)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$secretsRoot = Join-Path $env:APPDATA 'Microsoft\UserSecrets'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function New-RandomKey {
    $bytes = New-Object byte[] 48
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    [Convert]::ToBase64String($bytes)
}
function Read-Secrets([string]$file) {
    $values = [ordered]@{}
    if (Test-Path -LiteralPath $file) {
        $json = Get-Content -Raw -LiteralPath $file
        if ($json -and $json.Trim()) { foreach ($property in (ConvertFrom-Json $json).PSObject.Properties) { $values[$property.Name] = $property.Value } }
    }
    $values
}

$apiSecrets = Read-Secrets (Join-Path $secretsRoot 'OrganizationIntranet-Api\secrets.json')
$clientKey = $apiSecrets['Api:ClientKey']
if (-not $clientKey -or $clientKey.Length -lt 32) { $clientKey = New-RandomKey }
$adminClientKey = $apiSecrets['Api:AdminClientKey']
if (-not $adminClientKey -or $adminClientKey.Length -lt 32 -or $adminClientKey -eq $clientKey) { $adminClientKey = New-RandomKey }

foreach ($name in @('Api', 'Portal', 'Admin')) {
    $directory = Join-Path $secretsRoot "OrganizationIntranet-$name"
    $file = Join-Path $directory 'secrets.json'
    $values = Read-Secrets $file
    $values['Api:ClientKey'] = $clientKey
    if ($name -in @('Api', 'Admin')) { $values['Api:AdminClientKey'] = $adminClientKey }
    else { $values.Remove('Api:AdminClientKey') }
    if ($name -eq 'Api' -and $UseExistingConnection) {
        $legacySettings = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'appsettings.json') | ConvertFrom-Json
        if (-not $legacySettings.ConnectionStrings.DefaultConnection) { throw 'DefaultConnection is missing from the original appsettings.json.' }
        $values['ConnectionStrings:DefaultConnection'] = $legacySettings.ConnectionStrings.DefaultConnection
    }
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    [System.IO.File]::WriteAllText($file, ($values | ConvertTo-Json -Depth 10), $utf8NoBom)
    Write-Host "OK  $file"
}
Write-Host 'Development client keys configured. The administrative key is shared by Api and Admin only. Secrets were not printed.'
if (-not $UseExistingConnection) { Write-Host 'ConnectionStrings:DefaultConnection is read from the Api appsettings.json unless you also set it in the Api user secrets.' }
Write-Host 'Restart Api, Portal and Admin (and Visual Studio debugging sessions) so they pick up the keys.'
