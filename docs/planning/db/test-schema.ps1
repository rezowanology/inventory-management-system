# test-schema.ps1
# Loads ims_schema_postgres.sql into a throwaway database (ims_schema_test), runs
# ims_schema_tests.sql against it, then drops the database. Your `ims` database is not touched.
#
#   powershell -ExecutionPolicy Bypass -File .\db\test-schema.ps1
#
# Asks for the ims_user password; it is held in memory for this run only.

# 'Continue': PostgreSQL tools write notices to stderr, which Windows PowerShell 5.1 would
# otherwise treat as fatal. Failures are detected through $LASTEXITCODE instead.
$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$bin  = 'C:\Program Files\PostgreSQL\17\bin'
$log  = Join-Path $here 'schema-test-results.txt'

$conn = @('-h', 'localhost', '-p', '5432', '-U', 'ims_user')

# Ask for the password until it works (3 tries)
$connected = $false
for ($try = 1; $try -le 3 -and -not $connected; $try++) {
    $secure = Read-Host -Prompt 'ims_user password' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    $null = & "$bin\psql.exe" @conn -X -q -d ims -c 'SELECT 1' 2>&1
    if ($LASTEXITCODE -eq 0) { $connected = $true }
    else { Write-Host "That password was not accepted for ims_user (try $try of 3)." -ForegroundColor Yellow }
}
if (-not $connected) {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Write-Host 'Stopped. If you have forgotten the ims_user password, run setup-ims-database.ps1 again to set a new one.' -ForegroundColor Red
    exit 1
}

try {
    & "$bin\dropdb.exe" @conn --if-exists ims_schema_test
    & "$bin\createdb.exe" @conn ims_schema_test
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the test database (wrong password?).' }

    $env:PGOPTIONS = '--client-min-messages=warning'
    & "$bin\psql.exe" @conn -X -q -d ims_schema_test -v ON_ERROR_STOP=1 -f (Join-Path $here 'ims_schema_postgres.sql') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Loading the schema failed (see the error above).' }
    Remove-Item Env:PGOPTIONS

    # -o NUL hides the empty result tables; the ok/FAIL lines are NOTICEs on stderr
    & "$bin\psql.exe" @conn -X -q -d ims_schema_test -o NUL -f (Join-Path $here 'ims_schema_tests.sql') 2>&1 |
        ForEach-Object { ("$_" -replace '^psql:.*?NOTICE:\s+', '') } | Tee-Object -FilePath $log
    $fails = (Select-String -Path $log -Pattern '^FAIL ' -CaseSensitive).Count
    $oks   = (Select-String -Path $log -Pattern '^ok ' -CaseSensitive).Count
    Write-Host ''
    if ($fails -eq 0) { Write-Host "All $oks checks passed. Results saved to $log" -ForegroundColor Green }
    else { Write-Host "$fails check(s) FAILED, $oks passed. Results saved to $log" -ForegroundColor Red }
}
finally {
    $ErrorActionPreference = 'Continue'
    $null = & "$bin\dropdb.exe" @conn --if-exists ims_schema_test 2>&1
    Remove-Item Env:PGPASSWORD, Env:PGOPTIONS -ErrorAction SilentlyContinue
}
