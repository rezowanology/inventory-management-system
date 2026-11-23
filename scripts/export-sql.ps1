<#
.SYNOPSIS
  Regenerates the SQL deliverables in docs/db/:
    schema.sql                        - every EF Core migration as one idempotent script (tables, constraints, triggers)
    inventory_with_sample_data.sql    - pg_dump of a FRESH database: migrated + seeded (users, master data, the 3-row sample record)

.DESCRIPTION
  * The connection string is read from the .NET user-secret of src/Ims.Api INSIDE this script and is never printed.
    The password is handed to psql / pg_dump through the PGPASSWORD environment variable of THIS process only
    (it is removed again at the end), so it is not on any command line.
  * The dump is made from a throw-away database called ims_export (created and dropped here), so your real "ims"
    database and its data are never touched or reset.
  * Stop the running API first (dotnet build cannot replace files that a running program holds).

.PARAMETER Verify
  After the dump, load it into a second throw-away database (ims_verify) with ON_ERROR_STOP and check that the sample
  record is there (3 rows in inventory_transactions), then drop it again.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/export-sql.ps1 -Verify
#>
param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exportDb = 'ims_export'
$verifyDb = 'ims_verify'
$dumpFile = Join-Path $root 'docs/db/inventory_with_sample_data.sql'
$schemaFile = 'docs/db/schema.sql'

function Assert-Ok([string]$what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)." }
}

# ---- 1. read the connection string from user-secrets (stays in memory, never printed) ----
$line = & dotnet user-secrets list --project src/Ims.Api | Where-Object { $_ -like 'ConnectionStrings:Ims*' } | Select-Object -First 1
if (-not $line) { throw 'The user-secret ConnectionStrings:Ims is not set. See the README (Setup).' }
$parts = @{}
foreach ($pair in (($line -split ' = ', 2)[1] -split ';')) {
    $kv = $pair -split '=', 2
    if ($kv.Count -eq 2) { $parts[$kv[0].Trim().ToLowerInvariant()] = $kv[1] }
}
$dbHost = if ($parts['host']) { $parts['host'] } else { 'localhost' }
$dbPort = if ($parts['port']) { $parts['port'] } else { '5432' }
$dbUser = $parts['username']
if (-not $dbUser -or -not $parts['password']) { throw 'The connection string needs Username and Password.' }
$pgArgs = @('-h', $dbHost, '-p', $dbPort, '-U', $dbUser)

$env:PGPASSWORD = $parts['password']   # this process (and its children) only
$parts = $null; $line = $null

try {
    # ---- 2. schema.sql: all migrations, idempotent (safe to run on an empty or an existing database) ----
    Write-Host 'Generating docs/db/schema.sql ...'
    & dotnet ef migrations script --idempotent --project src/Ims.Api -o $schemaFile
    Assert-Ok 'dotnet ef migrations script'

    # ---- 3. a fresh database: migrate + seed (the 3-row sample record from the brief; no demo data) ----
    Write-Host "Creating throw-away database $exportDb ..."
    & dropdb @pgArgs --if-exists $exportDb;  Assert-Ok 'dropdb'
    & createdb @pgArgs $exportDb;            Assert-Ok 'createdb'

    $env:IMS_DATABASE = $exportDb          # makes the app use this database NAME with the same host/user/password
    Write-Host 'Applying migrations ...'
    & dotnet ef database update --project src/Ims.Api
    Assert-Ok 'dotnet ef database update'
    Write-Host 'Seeding ...'
    & dotnet run --project src/Ims.Api -- seed
    Assert-Ok 'seed'
    Remove-Item Env:IMS_DATABASE

    # ---- 4. the dump: structure + data, no owners / privileges so it loads under any role ----
    Write-Host 'Dumping to docs/db/inventory_with_sample_data.sql ...'
    & pg_dump @pgArgs --no-owner --no-privileges --encoding=UTF8 -f $dumpFile $exportDb
    Assert-Ok 'pg_dump'

    # ---- 5. optional: prove that the dump loads into an empty database ----
    if ($Verify) {
        Write-Host "Verifying: loading the dump into $verifyDb ..."
        & dropdb @pgArgs --if-exists $verifyDb; Assert-Ok 'dropdb'
        & createdb @pgArgs $verifyDb;           Assert-Ok 'createdb'
        try {
            & psql @pgArgs -d $verifyDb -q -v ON_ERROR_STOP=1 -f $dumpFile
            Assert-Ok 'loading the dump'
            $rows = & psql @pgArgs -d $verifyDb -t -A -c 'select count(*) from inventory_transactions'
            Assert-Ok 'counting rows'
            Write-Host "inventory_transactions rows in the loaded copy: $rows (expected 3)"
            if ("$rows".Trim() -ne '3') { throw 'Verification failed: expected 3 sample rows.' }
        }
        finally {
            & dropdb @pgArgs --if-exists $verifyDb
        }
    }
    Write-Host 'Done: docs/db/schema.sql and docs/db/inventory_with_sample_data.sql are up to date.'
}
finally {
    & dropdb @pgArgs --if-exists $exportDb 2>$null
    Remove-Item Env:IMS_DATABASE -ErrorAction SilentlyContinue
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
