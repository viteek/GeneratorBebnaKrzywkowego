$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$libDir = Join-Path $projectRoot "lib"
$target = Join-Path $libDir "SolidWorks.Interop.sldworks.dll"

New-Item -ItemType Directory -Force -Path $libDir | Out-Null

if (Test-Path $target) {
    Write-Host "SOLIDWORKS Interop już przygotowany:" -ForegroundColor Green
    Write-Host $target
    exit 0
}

$candidates = New-Object System.Collections.Generic.List[string]

# 1. Jeżeli SOLIDWORKS działa, jego katalog EXE jest najpewniejszym źródłem.
try {
    $processes = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue
    foreach ($p in $processes) {
        try {
            $exe = $p.MainModule.FileName
            if ($exe) {
                $installDir = Split-Path $exe -Parent
                $candidates.Add((Join-Path $installDir "api\redist\SolidWorks.Interop.sldworks.dll"))
            }
        } catch {}
    }
} catch {}

# 2. Typowe katalogi instalacyjne SOLIDWORKS.
$roots = @(
    (Join-Path $env:ProgramFiles "SOLIDWORKS Corp"),
    (Join-Path $env:ProgramFiles "Dassault Systemes")
) | Where-Object { $_ -and (Test-Path $_) }

foreach ($root in $roots) {
    try {
        Get-ChildItem -Path $root -Filter "SolidWorks.Interop.sldworks.dll" -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\api\\redist\\" } |
            ForEach-Object { $candidates.Add($_.FullName) }
    } catch {}
}

# Usuń duplikaty.
$candidates = $candidates |
    Where-Object { $_ -and (Test-Path $_) } |
    Select-Object -Unique

if (-not $candidates -or $candidates.Count -eq 0) {
    Write-Host ""
    Write-Host "Nie znaleziono SolidWorks.Interop.sldworks.dll." -ForegroundColor Red
    Write-Host ""
    Write-Host "Biblioteka powinna znajdować się w:"
    Write-Host "  <katalog SOLIDWORKS>\api\redist\SolidWorks.Interop.sldworks.dll"
    Write-Host ""
    Write-Host "Możesz też skopiować ten plik ręcznie do:"
    Write-Host "  $libDir"
    exit 2
}

# Preferuj plik z katalogu działającej instalacji / najnowszy timestamp.
$chosen = $candidates |
    Get-Item |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Copy-Item -Path $chosen.FullName -Destination $target -Force

Write-Host ""
Write-Host "SOLIDWORKS Interop przygotowany." -ForegroundColor Green
Write-Host "Źródło : $($chosen.FullName)"
Write-Host "Cel    : $target"
