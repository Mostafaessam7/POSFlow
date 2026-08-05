<#
.SYNOPSIS
    Backs up the PosFlow SQL Server database and prunes backups older
    than -RetentionDays.

.DESCRIPTION
    Works against any SQL Server instance you control directly (a VM,
    an on-prem box, or the docker-compose 'sql' service) via sqlcmd.
    If you're on a managed database (Azure SQL, RDS SQL Server, ...)
    use that platform's own automated backup/point-in-time-recovery
    feature instead - it's more robust than this script and usually
    already running by default. This script is for the self-hosted
    case, where nothing else is doing it for you.

.PARAMETER ServerInstance
    e.g. "localhost" or "localhost,1433" for the docker-compose setup.

.PARAMETER Database
    Defaults to PosFlowDb.

.PARAMETER BackupDirectory
    Where .bak files are written. Must already exist and be writable
    by the SQL Server service account.

.PARAMETER RetentionDays
    Backups older than this are deleted after a successful new backup.
    Defaults to 30.

.EXAMPLE
    ./backup-database.ps1 -ServerInstance localhost -SaPassword $env:MSSQL_SA_PASSWORD -BackupDirectory C:\PosFlowBackups

.NOTES
    Schedule this with Windows Task Scheduler (daily) or a cron job
    calling `pwsh ./backup-database.ps1 ...` on Linux. Test a restore
    (see the RESTORE example below) at least once - an untested backup
    is not a backup.

    RESTORE EXAMPLE:
        RESTORE DATABASE PosFlowDb
        FROM DISK = 'C:\PosFlowBackups\PosFlowDb_2026-08-05_120000.bak'
        WITH REPLACE;
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,

    [string]$Database = "PosFlowDb",

    [Parameter(Mandatory = $true)]
    [string]$BackupDirectory,

    [Parameter(Mandatory = $true)]
    [string]$SaPassword,

    [string]$SaUsername = "sa",

    [int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupDirectory)) {
    throw "Backup directory '$BackupDirectory' does not exist - create it first."
}

$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$backupFile = Join-Path $BackupDirectory "${Database}_${timestamp}.bak"

Write-Host "Backing up $Database on $ServerInstance to $backupFile ..."

$backupSql = @"
BACKUP DATABASE [$Database]
TO DISK = N'$backupFile'
WITH COMPRESSION, CHECKSUM, INIT,
NAME = N'$Database full backup $timestamp';
"@

sqlcmd -S $ServerInstance -U $SaUsername -P $SaPassword -C -Q $backupSql

if ($LASTEXITCODE -ne 0) {
    throw "Backup failed (sqlcmd exit code $LASTEXITCODE)."
}

Write-Host "Backup complete: $backupFile"

$cutoff = (Get-Date).AddDays(-$RetentionDays)
$oldBackups = Get-ChildItem -Path $BackupDirectory -Filter "${Database}_*.bak" |
    Where-Object { $_.LastWriteTime -lt $cutoff }

foreach ($old in $oldBackups) {
    Write-Host "Pruning old backup: $($old.Name)"
    Remove-Item $old.FullName -Force
}

Write-Host "Done. $($oldBackups.Count) old backup(s) pruned (retention: $RetentionDays days)."
