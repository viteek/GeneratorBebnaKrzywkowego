$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$cs = Get-ChildItem $root -Recurse -Filter *.cs

$zakazane = @(
    "Operations2",
    "SWBODYCUT",
    "BooleanCutRobust",
    "BuildCamDrumByAdaptiveCuts",
    "CreateBearingPoseBodyExtraEps"
)

$bledy = @()

foreach ($plik in $cs) {
    foreach ($wzorzec in $zakazane) {
        $trafienia = Select-String -Path $plik.FullName -Pattern $wzorzec -SimpleMatch
        foreach ($t in $trafienia) {
            # Komentarz w pliku GeneratorBebnaSurfaceLoft zawiera nazwę Operations2
            # jako deklarację architektury; ignorujemy linie komentarza.
            $trim = $t.Line.TrimStart()
            if ($trim.StartsWith("//") -or $trim.StartsWith("///")) {
                continue
            }

            # String diagnostyczny "IBody2.Operations2: NIE UŻYWANE" również
            # nie jest wywołaniem API.
            if ($t.Line -match "NIE UŻYWANE") {
                continue
            }

            $bledy += "$($plik.Name):$($t.LineNumber): $($t.Line.Trim())"
        }
    }
}

if ($bledy.Count -gt 0) {
    Write-Host "WYKRYTO STARY / BOOLEANOWY RDZEŃ:" -ForegroundColor Red
    $bledy | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}

Write-Host "OK: brak bezpośrednich operacji Boolean w kodzie C#." -ForegroundColor Green
Write-Host "Silnik: profile 3D -> natywne Lofted Cut."
