[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [switch]$Force
)

$ErrorActionPreference = "Stop"
$temporaryDump = "/tmp/labtrack-restore.dump"
$resolvedBackupPath = (Resolve-Path -LiteralPath $BackupPath).Path

if (-not $Force) {
    $confirmation = Read-Host "This replaces matching database objects and data. Type RESTORE to continue"
    if ($confirmation -cne "RESTORE") {
        throw "Restore cancelled."
    }
}

docker compose cp $resolvedBackupPath "db:$temporaryDump"
if ($LASTEXITCODE -ne 0) { throw "Could not copy the backup into the database container." }

try {
    docker compose exec -T db sh -c 'pg_restore --clean --if-exists --no-owner -U "$POSTGRES_USER" -d "$POSTGRES_DB" /tmp/labtrack-restore.dump'
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL restore failed." }
}
finally {
    docker compose exec -T db rm -f $temporaryDump | Out-Null
}

Write-Output "Restore completed from $resolvedBackupPath"
