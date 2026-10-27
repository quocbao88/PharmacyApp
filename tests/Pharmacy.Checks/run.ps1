param(
    [string]$PostgresBin = 'C:/Program Files/PostgreSQL/18/bin'
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$testRoot = Join-Path $workspace ('artifacts/InventoryTests/run-' + [Guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $testRoot 'pgdata'
$started = $false

if (Get-NetTCPConnection -LocalPort 55439 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Test port 55439 is already in use. Stop the existing test server before running.'
}

New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    # Use a fresh cluster on loopback; never load application connection settings.
    & (Join-Path $PostgresBin 'initdb.exe') -D $dataPath -U pharmacy_test -A trust -E UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the isolated test cluster.' }

    & (Join-Path $PostgresBin 'pg_ctl.exe') -D $dataPath -l (Join-Path $testRoot 'server.log') -o '-p 55439 -h 127.0.0.1' -w start
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the isolated test cluster.' }
    $started = $true

    Push-Location $workspace
    try {
        $env:PHARMACY_TEST_PG_BIN = $PostgresBin
        dotnet run --project tests/Pharmacy.Checks/Pharmacy.Checks.csproj --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Inventory integration checks failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($started) {
        & (Join-Path $PostgresBin 'pg_ctl.exe') -D $dataPath -m fast -w stop
    }
}
