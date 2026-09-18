param(
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [switch]$UseExistingConnection
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$connectionString = $env:ConnectionStrings__DefaultConnection
if (-not $connectionString) {
    $secretFile = Join-Path $env:APPDATA 'Microsoft/UserSecrets/OrganizationIntranet-Api/secrets.json'
    if (Test-Path -LiteralPath $secretFile) {
        $secrets = Get-Content -Raw -LiteralPath $secretFile | ConvertFrom-Json
        $connectionString = $secrets.'ConnectionStrings:DefaultConnection'
    }
}
if (-not $connectionString -and $UseExistingConnection) {
    $settings = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'appsettings.json') | ConvertFrom-Json
    $connectionString = $settings.ConnectionStrings.DefaultConnection
}
if (-not $connectionString) { throw 'Configure the API connection in environment/user secrets or explicitly select -UseExistingConnection.' }
$sql = Get-Content -Raw -LiteralPath $ScriptPath
if ($sql -match '(?im)^\s*GO\s*$') { throw 'Use a single SQL batch without GO separators.' }
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $result = New-Object System.Data.DataSet
    [void]$adapter.Fill($result)
    foreach ($table in $result.Tables) {
        $table | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
    }
    Write-Output 'Database script completed.'
}
catch {
    # Do not echo connection strings, SQL text or row values from database errors.
    $sqlException = $_.Exception
    while ($sqlException.InnerException) { $sqlException = $sqlException.InnerException }
    if ($sqlException -is [System.Data.SqlClient.SqlException]) {
        throw "Database script failed (SQL error $($sqlException.Number), line $($sqlException.LineNumber)). No connection details were logged."
    }
    throw 'Database script failed. No connection details were logged.'
}
finally { $connection.Dispose() }
