param([switch]$UseExistingConnection)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$secretsRoot = Join-Path $env:APPDATA 'Microsoft/UserSecrets'
$apiSecretsFile = Join-Path $secretsRoot 'OrganizationIntranet-Api/secrets.json'
$apiSecrets = @{}
if (Test-Path -LiteralPath $apiSecretsFile) { $apiSecrets = Get-Content -Raw -LiteralPath $apiSecretsFile | ConvertFrom-Json -AsHashtable }
$clientKey = $apiSecrets['Api:ClientKey']
if (-not $clientKey) {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $clientKey = [Convert]::ToBase64String($bytes)
}
$adminClientKey = $apiSecrets['Api:AdminClientKey']
if (-not $adminClientKey -or $adminClientKey.Length -lt 32 -or $adminClientKey -eq $clientKey) {
    $adminBytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($adminBytes)
    $adminClientKey = [Convert]::ToBase64String($adminBytes)
}
foreach ($name in @('Api', 'Portal', 'Admin')) {
    $directory = Join-Path $secretsRoot "OrganizationIntranet-$name"
    $file = Join-Path $directory 'secrets.json'
    $values = @{}
    if (Test-Path -LiteralPath $file) { $values = Get-Content -Raw -LiteralPath $file | ConvertFrom-Json -AsHashtable }
    $values['Api:ClientKey'] = $clientKey
    if ($name -in @('Api', 'Admin')) { $values['Api:AdminClientKey'] = $adminClientKey }
    else { [void]$values.Remove('Api:AdminClientKey') }
    if ($name -eq 'Api' -and $UseExistingConnection) {
        $legacySettings = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'appsettings.json') | ConvertFrom-Json
        if (-not $legacySettings.ConnectionStrings.DefaultConnection) { throw 'DefaultConnection is missing from the original appsettings.json.' }
        $values['ConnectionStrings:DefaultConnection'] = $legacySettings.ConnectionStrings.DefaultConnection
    }
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $values | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding utf8
}
Write-Host 'Development client keys configured. Administrative key is shared by API and Admin only. Secrets were not printed.'
if (-not $UseExistingConnection) { Write-Host 'Configure ConnectionStrings:DefaultConnection in the API user secrets before starting.' }
