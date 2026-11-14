[CmdletBinding()]
param(
    [string]$BackupPath
)

$ErrorActionPreference = "Stop"
$temporaryDump = "/tmp/labtrack-backup.dump"

if ([string]::IsNullOrWhiteSpace($BackupPath)) {
    $backupDirectory = Join-Path $PSScriptRoot "backups"
    $BackupPath = Join-Path $backupDirectory ("labtrack-{0}.dump" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
}

$resolvedBackupPath = [System.IO.Path]::GetFullPath($BackupPath)
$backupDirectory = Split-Path -Parent $resolvedBackupPath
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

docker compose exec -T db sh -c 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/labtrack-backup.dump'
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL backup failed." }

try {
    docker compose cp "db:$temporaryDump" $resolvedBackupPath
    if ($LASTEXITCODE -ne 0) { throw "Could not copy the database backup out of the container." }
}
finally {
    docker compose exec -T db rm -f $temporaryDump | Out-Null
}

Write-Output "Backup written to $resolvedBackupPath"
