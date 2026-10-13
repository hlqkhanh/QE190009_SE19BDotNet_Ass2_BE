param([Parameter(Mandatory)][string]$SourceUrl, [Parameter(Mandatory)][string]$DestinationUrl)
$ErrorActionPreference = 'Stop'
if ($SourceUrl -eq $DestinationUrl) { throw 'Source and destination must be different databases.' }
# Credentials are supplied as transient environment variables, never command strings.
foreach ($value in @($SourceUrl, $DestinationUrl)) { $uri = [Uri]$value; if ($uri.Scheme -notin @('postgres', 'postgresql')) { throw 'Use PostgreSQL URLs.' } }
$dump = Join-Path ([IO.Path]::GetTempPath()) ('tasktrack-ass1-' + [Guid]::NewGuid().ToString('N') + '.dump')
$source = [Uri]$SourceUrl; $destination = [Uri]$DestinationUrl
function Set-PgEnvironment([Uri]$uri) {
    $parts = $uri.UserInfo.Split(':',2)
    $env:PGHOST = $uri.Host; $env:PGPORT = $(if ($uri.Port -gt 0) { $uri.Port } else { 5432 })
    $env:PGDATABASE = $uri.AbsolutePath.TrimStart('/'); $env:PGUSER = [Uri]::UnescapeDataString($parts[0]); $env:PGPASSWORD = [Uri]::UnescapeDataString($parts[1]); $env:PGSSLMODE = 'require'
}
$saved = @{}; foreach ($name in @('PGHOST','PGPORT','PGDATABASE','PGUSER','PGPASSWORD','PGSSLMODE')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    Set-PgEnvironment $destination
    $count = & psql -X -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';"
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect destination.' }
    if ([int]$count -ne 0) { throw 'Destination public schema is not empty. Refusing to overwrite existing data.' }
    Set-PgEnvironment $source
    & pg_dump --format=custom --no-owner --no-acl --schema=public --file=$dump
    if ($LASTEXITCODE -ne 0) { throw 'Source backup failed.' }
    Set-PgEnvironment $destination
    & pg_restore --no-owner --no-acl --single-transaction --exit-on-error --dbname=$env:PGDATABASE $dump
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & psql -X -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot '../database/Assignment2_upgrade.sql')
    if ($LASTEXITCODE -ne 0) { throw 'Ass2 upgrade failed.' }
    Write-Output 'Copied Ass1 data and applied Ass2 upgrade. Source database was read only.'
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    if (Test-Path -LiteralPath $dump) { Remove-Item -LiteralPath $dump }
}
