# setup-ims-database.ps1
# Creates the PostgreSQL role `ims_user` (LOGIN, CREATEDB) and the database `ims` owned by it,
# for the Inventory Management System project. Safe to run again: an existing role keeps its
# database and only gets the new password.
#
# Run in PowerShell after installing PostgreSQL 17:
#   powershell -ExecutionPolicy Bypass -File .\setup-ims-database.ps1
#
# Passwords are typed by you, held only in memory for this run, and never written to disk.

$ErrorActionPreference = 'Stop'

function Read-Secret([string]$prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# 1. Find psql (PATH first, then the default PostgreSQL 17 folder)
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $default = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
    if (Test-Path $default) { $psql = $default }
    else { throw 'psql was not found. Install PostgreSQL 17 first (with Command Line Tools), then run this again.' }
}
Write-Host "Using $psql"

# 2. Check the server is running
$svc = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($svc -and $svc.Status -ne 'Running') {
    throw "The PostgreSQL service '$($svc.Name)' is $($svc.Status). Start it in Windows Services, then run this again."
}

# 3. Ask for passwords
$adminPw = Read-Secret 'Password of the postgres admin user (chosen during install)'
do {
    $appPw  = Read-Secret 'New password for the app user ims_user (8+ characters)'
    $appPw2 = Read-Secret 'Type the ims_user password again'
    $ok = ($appPw -eq $appPw2) -and ($appPw.Length -ge 8)
    if (-not $ok) { Write-Host 'Passwords did not match or were shorter than 8 characters. Try again.' -ForegroundColor Yellow }
} until ($ok)

$sql = @'
\getenv app_pw IMS_APP_PW
SELECT format('CREATE ROLE ims_user LOGIN CREATEDB PASSWORD %L', :'app_pw')
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ims_user') \gexec
SELECT format('ALTER ROLE ims_user LOGIN CREATEDB PASSWORD %L', :'app_pw')
 WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ims_user') \gexec
SELECT 'CREATE DATABASE ims OWNER ims_user ENCODING ''UTF8'' TEMPLATE template0'
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'ims') \gexec
'@

try {
    # 4. Create the role and database as the postgres admin
    $env:PGPASSWORD = $adminPw
    $env:IMS_APP_PW = $appPw
    $sql | & $psql -X -q -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d postgres
    if ($LASTEXITCODE -ne 0) { throw 'Creating the role or database failed (see the message above). A wrong postgres password is the usual cause.' }

    # 5. Prove the app user can connect
    $env:PGPASSWORD = $appPw
    & $psql -X -h localhost -p 5432 -U ims_user -d ims -c "SELECT current_user AS connected_as, current_database() AS database, rolcreatedb AS can_create_test_db FROM pg_roles WHERE rolname = current_user;"
    if ($LASTEXITCODE -ne 0) { throw 'ims_user could not connect to the ims database.' }

    Write-Host ''
    Write-Host 'Done. Database "ims" and user "ims_user" are ready.' -ForegroundColor Green
    Write-Host 'On Day 1, store the connection in .NET user-secrets (the README gives the exact command).'
}
finally {
    Remove-Item Env:PGPASSWORD, Env:IMS_APP_PW -ErrorAction SilentlyContinue
    $adminPw = $null; $appPw = $null; $appPw2 = $null
}
