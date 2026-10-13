param([string]$DatabaseUrl)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$DatabaseUrl) { $DatabaseUrl = Read-Host 'Paste the EXTERNAL Database URL of the existing Ass1 database' }
$uri = [Uri]$DatabaseUrl
if ($uri.Scheme -notin @('postgres', 'postgresql') -or !$uri.Host -or !$uri.AbsolutePath.Trim('/')) { throw 'Provide the full PostgreSQL URL from Render.' }
$parts = $uri.UserInfo.Split(':', 2)
if ($parts.Count -ne 2) { throw 'The URL must contain the database username and password.' }
$backupDir = Join-Path $root '.tools/backups'
New-Item -ItemType Directory -Force $backupDir | Out-Null
$backupName = 'ass1-before-ass2-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.dump'
$sqlPath = Join-Path $root 'database/Assignment2_upgrade.sql'
$names = @('PGHOST','PGPORT','PGDATABASE','PGUSER','PGPASSWORD','PGSSLMODE')
$saved = @{}; foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:PGHOST = $uri.Host; $env:PGPORT = $(if ($uri.Port -gt 0) { $uri.Port } else { 5432 })
    $env:PGDATABASE = [Uri]::UnescapeDataString($uri.AbsolutePath.TrimStart('/'))
    $env:PGUSER = [Uri]::UnescapeDataString($parts[0]); $env:PGPASSWORD = [Uri]::UnescapeDataString($parts[1]); $env:PGSSLMODE = 'require'
    Write-Output '1/2: Backing up the existing database. The upgrade will stop if backup fails.'
    & docker run --rm -e PGHOST -e PGPORT -e PGDATABASE -e PGUSER -e PGPASSWORD -e PGSSLMODE --mount "type=bind,source=$backupDir,target=/backup" postgres:18-alpine pg_dump --format=custom --no-owner --no-acl "--file=/backup/$backupName"
    if ($LASTEXITCODE -ne 0) { throw 'Backup failed. No upgrade was applied. Check Docker Desktop, the external URL and database access.' }
    Write-Output ('Backup saved: ' + (Join-Path $backupDir $backupName))
    Write-Output '2/2: Applying the additive Assignment 2 upgrade in a transaction.'
    & docker run --rm -e PGHOST -e PGPORT -e PGDATABASE -e PGUSER -e PGPASSWORD -e PGSSLMODE --mount "type=bind,source=$sqlPath,target=/upgrade.sql,readonly" postgres:18-alpine psql -X -v ON_ERROR_STOP=1 -f /upgrade.sql
    if ($LASTEXITCODE -ne 0) { throw 'Upgrade failed and its transaction was rolled back. Keep the backup and inspect the SQL error.' }
    Write-Output 'Upgrade completed. Existing Ass1 rows were preserved. Use this same database URL for Ass2.'
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
