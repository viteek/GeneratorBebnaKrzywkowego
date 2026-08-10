$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\GeneratorBebnaKrzywkowego.SurfaceLoft.csproj"

& (Join-Path $PSScriptRoot "Przygotuj-SolidWorks-Interop.ps1")
if ($LASTEXITCODE -ne 0) { throw "Nie udało się przygotować SOLIDWORKS Interop." }

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Nie znaleziono vswhere.exe. Visual Studio Installer nie jest zainstalowany."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
    -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

if (-not $msbuild) {
    throw "Nie znaleziono MSBuild.exe. Doinstaluj workload '.NET desktop development'."
}

Write-Host "MSBuild: $msbuild"
Write-Host "Project: $project"
Write-Host "Configuration: Debug | Platform: x64"

& $msbuild $project /t:Restore,Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build zakończony błędem: $LASTEXITCODE"
}

$exe = Join-Path (Split-Path $project) "bin\Debug\net48\GeneratorBebnaKrzywkowego.SurfaceLoft.exe"
if (-not (Test-Path $exe)) {
    throw "Build zakończył się, ale nie znaleziono EXE: $exe"
}

Write-Host ""
Write-Host "BUILD OK" -ForegroundColor Green
Write-Host $exe
