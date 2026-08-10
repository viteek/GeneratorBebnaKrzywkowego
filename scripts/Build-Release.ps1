$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\GeneratorBebnaKrzywkowego.SurfaceLoft.csproj"

& (Join-Path $PSScriptRoot "Przygotuj-SolidWorks-Interop.ps1")
if ($LASTEXITCODE -ne 0) { throw "Nie udało się przygotować SOLIDWORKS Interop." }

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Nie znaleziono vswhere.exe. Zainstaluj Visual Studio 2022."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
    -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

if (-not $msbuild) {
    throw "Nie znaleziono MSBuild.exe."
}

& $msbuild $project /t:Restore,Build /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE -ne 0) {
    throw "Build zakończony błędem: $LASTEXITCODE"
}

Write-Host "Build OK"
Write-Host (Join-Path (Split-Path $project) "bin\Release\net48\GeneratorBebnaKrzywkowego.SurfaceLoft.exe")
