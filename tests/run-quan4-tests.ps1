<#
.SYNOPSIS
  Chay bo test Quan4 (xUnit) theo nhom hoac ma case.

.EXAMPLE
  .\tests\run-quan4-tests.ps1
  .\tests\run-quan4-tests.ps1 -Area Geofence
  .\tests\run-quan4-tests.ps1 -Case Q4-PV-003
  .\tests\run-quan4-tests.ps1 -Case Q4-PV-003,Q4-PV-004
  .\tests\run-quan4-tests.ps1 -List

.NOTES
  Bien moi truong:
  - QUAN4_BASE_URL: backend URL, mac dinh http://localhost:5114
  - QUAN4_REQUIRE_BACKEND=true: fail neu backend/seed data khong san sang
  - QUAN4_BURST_COUNT=20: so request cho burst test
#>
param(
    [Parameter(Position = 0)]
    [string[]] $Case = @(),
    [ValidateSet('Api', 'PoiVisit', 'Geofence')]
    [string] $Area = '',
    [switch] $List
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..\VinhKhanh\Quan4TestSuite\Quan4TestSuite.csproj'

$ids = [System.Collections.Generic.List[string]]::new()
foreach ($c in $Case) {
    foreach ($p in ($c -split ',')) {
        $t = $p.Trim()
        if ($t.Length -gt 0) { [void]$ids.Add($t) }
    }
}

if ($List) {
    dotnet test $proj --list-tests
    Write-Host ""
    Write-Host "Ma case:"
    Write-Host "  Q4-API-001"
    Write-Host "  Q4-PV-001 .. Q4-PV-005"
    Write-Host "  Q4-GF-001 .. Q4-GF-005"
    exit $LASTEXITCODE
}

if ($ids.Count -gt 0) {
    $filter = ($ids | ForEach-Object { '(Quan4=' + $_ + ')' }) -join '|'
    dotnet test $proj --filter $filter
    exit $LASTEXITCODE
}

if ($Area.Length -gt 0) {
    dotnet test $proj --filter "Area=$Area"
    exit $LASTEXITCODE
}

dotnet test $proj
exit $LASTEXITCODE
