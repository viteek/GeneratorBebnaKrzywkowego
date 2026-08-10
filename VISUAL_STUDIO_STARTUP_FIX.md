# Visual Studio — "Nie można uruchomić projektu startowego"

Jeżeli Visual Studio pokazuje:

`Nie można uruchomić debugowania. Nie można uruchomić projektu startowego`

oraz build ma status `Pominięta kompilacja`, sprawdź dwie rzeczy.

## 1. Projekt startowy

W Solution Explorer:

`GeneratorBebnaKrzywkowego.SurfaceLoft`
→ prawy przycisk
→ `Set as Startup Project / Ustaw jako projekt startowy`.

Nazwa projektu powinna być pogrubiona.

## 2. Configuration Manager

`Build / Kompiluj → Configuration Manager / Menedżer konfiguracji`

Ustaw:

- Active solution configuration: `Debug`
- Active solution platform: `x64`
- Project configuration: `Debug`
- Project platform: `x64`
- kolumna `Build / Kompiluj`: zaznaczona.

v5.0.1 dodaje do `.csproj` jawną listę platform `x64`, więc platforma nie
powinna być już pomijana przez Visual Studio.

## 3. Build bez debuggera

Uruchom PowerShell:

```powershell
.\scripts\Build-Debug.ps1
```

Jeżeli ten skrypt nie znajdzie MSBuild lub .NET Framework 4.8 targetingu,
trzeba doinstalować workload Visual Studio `.NET desktop development`
oraz targeting/developer pack .NET Framework 4.8.
