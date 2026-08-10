# Generator bębna krzywkowego Surface-Loft v5.1.1

To jest nowy generator C# / COM. Nie jest patchem v4.x.

## Zasada

Wersje v3/v4 budowały bieżnię przez setki dyskretnych pozycji walca i operacje
Boolean. To zostało usunięte.

v5 tworzy:

1. pełny bęben bazowy,
2. analityczną kinematykę ramienia i łożyska,
3. przekroje kołowe na rzeczywistych płaszczyznach łożyska,
4. zamknięte, natywne `Lofted Cut` przez te profile,
5. kilka automatycznie rozmieszczonych loftów dla skończonej szerokości rolki,
6. otwór centralny jako ostatni `Lofted Cut`.

W kodzie generatora nie ma `IBody2.Operations2` ani `SWBODYCUT`.

## Dlaczego C#

C# daje wygodniejsze:

- zarządzanie konfiguracją,
- walidację,
- matematykę kinematyki,
- logowanie,
- fallback API,
- rozbudowę o testy,
- komunikację COM z SOLIDWORKS.

Projekt używa late-bound COM (`dynamic`), więc **nie trzeba dodawać referencji
do konkretnych wersji `SolidWorks.Interop.*`**, aby skompilować projekt.

## Wymagania

- Windows x64,
- SOLIDWORKS Desktop,
- Visual Studio 2022,
- .NET Framework 4.8 Developer Pack.

## Budowanie

Otwórz:

`GeneratorBebnaKrzywkowego.SurfaceLoft.sln`

i zbuduj:

`Release | x64`.

Albo uruchom:

`scripts\Build-Release.ps1`

## Uruchomienie

Możesz:

1. uruchomić EXE bez argumentów — pojawi się wybór YAML,
2. albo przekazać YAML w linii poleceń:

```powershell
.\GeneratorBebnaKrzywkowego.SurfaceLoft.exe `
  .\konfiguracja_bebna_krzywkowego_MINIMALNA.yaml
```

Program łączy się z aktywnym SOLIDWORKS. Jeśli SOLIDWORKS nie działa,
uruchamia go przez COM. Następnie zawsze tworzy **nową część**, żeby nie
modyfikować przypadkowego aktywnego dokumentu.

## Minimalny YAML

```yaml
wersja_formatu: 2

bęben:
  średnica_zewnętrzna_mm: 156
  średnica_otworu_mm: 53
  długość_mm: 70

łożysko:
  średnica_zewnętrzna_mm: 30
  szerokość_mm: 9

ramię:
  długość_mm: 46.4
  oś_zawiasu_od_górnej_krawędzi_mm: 19.1
  kąt_zamknięcia_deg: 270 # ramię w dół, najniższe Z łożyska
  kąt_otwarcia_deg: 180   # łożysko uniesione

krzywka:
  wznos_deg: 120
  postój_wysoki_deg: 60
  powrót_deg: 120
```

Z tego automatycznie wynika m.in.:

- `R_bębna = 78 mm`,
- `r_łożyska = 15 mm`,
- `Rp = 78 + 15 = 93 mm`,
- średnica pozornego okręgu osi zawiasu = `186 mm`,
- `Z_zawiasu = 70/2 - 19.1 = +15.9 mm`,
- skok ramienia = `|180 - 270| = 90°`,
- końcowy niski postój = `360 - 120 - 60 - 120 = 60°`.

### Znaczenie faz ruchu

„Wznos” oznacza **unoszenie środka łożyska w osi Z**, a nie wzrost
liczbowej wartości kąta ramienia. Dla powyższej konwencji w fazie wznosu
`α` interpoluje od `α_zamknięcia = 270°` do `α_otwarcia = 180°`, więc maleje,
podczas gdy `Z(C)` rośnie. Postój wysoki utrzymuje `α_otwarcia` i maksymalne
`Z(C)`, a powrót interpoluje z powrotem do `α_zamknięcia` i opuszcza łożysko.

Dla znormalizowanego postępu prawa ruchu `s(u)`, `s(0)=0`, `s(1)=1`:

`α_wznos(u) = α_zamknięcia + (α_otwarcia - α_zamknięcia)·s(u)`

`α_powrót(u) = α_otwarcia + (α_zamknięcia - α_otwarcia)·s(u)`

oraz `Z(C) = Z_zawiasu + L·sin(α)`. W przykładzie daje to odpowiednio
`Z_zawiasu-L` w zamknięciu i `Z_zawiasu` w otwarciu.

## Ważne o dokładności

v5 jest powierzchniowym generatorem loftowanym, ale skończona szerokość
łożyska jest reprezentowana przez kilka przesuniętych gładkich loftów.
Liczba tych loftów jest liczona automatycznie z zadanej tolerancji
`tolerancja_szerokosci_mm`.

To jest jakościowo inny model niż v4:
nie ma setek zmian topologii od kolejnych walców.

## Test bezpieczeństwa architektury

Uruchom:

```powershell
.\scripts\Sprawdz-BrakBooleanow.ps1
```

Skrypt przeszukuje pliki `.cs` i zgłasza błąd, jeśli znajdzie bezpośrednie
wywołania `Operations2`, `SWBODYCUT` lub stary wzorzec cylinder-cut.

## Pierwszy test

Na początek użyj bez zmian:

`konfiguracja_bebna_krzywkowego_MINIMALNA.yaml`.

Po uruchomieniu w konsoli powinno być:

- liczba profili kątowych,
- automatyczna liczba stacji szerokości,
- `IBody2.Operations2: NIE UŻYWANE`,
- kolejne `KRZYWKA_LOFT_XX`.

Jeżeli konkretna wersja SOLIDWORKS nie zaakceptuje wielu profili w jednym
szkicu 3D, generator automatycznie przełączy się na osobne szkice profili.

## Struktura

- `Core/Konfiguracja.cs` — minimalne dane + wartości automatyczne,
- `Core/ParserYaml.cs` — parser YAML UTF-8,
- `Core/Kinematyka.cs` — matematyka mechanizmu,
- `Core/PlanPowierzchniowy.cs` — profile kątowe i szerokość,
- `SolidWorks/SesjaSolidWorks.cs` — COM,
- `SolidWorks/GeneratorBebnaSurfaceLoft.cs` — natywne profile 3D i Lofted Cut,
- `ALGORYTM.md` — opis matematyczny.


## Visual Studio: projekt startowy i platforma

v5.1.1 jawnie deklaruje `x64` jako platformę projektu i zawiera współdzielony
profil `GeneratorBebnaKrzywkowego.SurfaceLoft.slnLaunch`.

Po otwarciu rozwiązania:

1. otwórz plik `.sln`, nie sam folder,
2. ustaw na pasku `Debug | x64`,
3. w `Build > Configuration Manager` sprawdź, że przy projekcie zaznaczone jest `Build`,
4. jeżeli Visual Studio nie wybierze projektu automatycznie, kliknij projekt
   prawym przyciskiem i wybierz `Set as Startup Project`.

Do diagnostycznego builda bez F5 można uruchomić:

```powershell
.\scripts\Build-Debug.ps1
```


## v5.1.1 — ważna zmiana COM

v5.1.1/v5.1.1 używały `dynamic` do late-bound COM. Na testowanej instalacji
SOLIDWORKS obiekt z Running Object Table zwrócił przy pierwszym dostępie do
`Visible`:

`TYPE_E_ELEMENTNOTFOUND (0x8002802B)`

Błąd pochodził z `System.Dynamic.ComRuntimeHelpers.GetITypeInfoFromIDispatch`,
czyli z C# Dynamic Runtime Binder — geometria nie zdążyła się jeszcze rozpocząć.

v5.1.1 usuwa `dynamic` z całej warstwy SOLIDWORKS i korzysta z oficjalnego
`SolidWorks.Interop.sldworks.dll` (Primary Interop Assembly).

Biblioteka nie jest dołączona do paczki. Przy pierwszej kompilacji skrypt:

`scripts\Przygotuj-SolidWorks-Interop.ps1`

wyszukuje ją w lokalnej instalacji SOLIDWORKS, w `api\redist`, i kopiuje do
lokalnego `lib\`.

To odpowiada zalecanemu modelowi projektów .NET SOLIDWORKS API.


## v5.1.1 — poprawiona kinematyka łożyska na wahadle

v5.0.x miało błędne założenie:

`oś łożyska = oś zawiasu = kierunek styczny`

To zachowywało prawidłową styczność zawiasu po obwodzie bębna, ale nie
odtwarzało sztywnego obrotu całego zespołu ramienia i łożyska podczas
opadania ramienia.

v5.1.1 rozdziela trzy różne kierunki:

- `oś_zawiasu = et` — styczna do pozornego okręgu,
- `ramię = cos(α)·er + sin(α)·Z`,
- `oś_łożyska = sin(α)·er - cos(α)·Z`.

Dla aktualnej konfiguracji:

### α = 180°

- ramię: radialnie do środka,
- oś łożyska: `+Z`,
- oś łożyska jest równoległa do osi bębna.

### α = 270°

- ramię: w dół wzdłuż `-Z`,
- oś łożyska: `-er`,
- oś łożyska jest radialna i skierowana w stronę osi bębna.

Między tymi położeniami oś łożyska obraca się ciągle razem z ramieniem wokół
fizycznej osi zawiasu — dokładnie jak sztywne wahadło.

Również stacje szerokości rolki są teraz odkładane wzdłuż aktualnie obróconej
osi łożyska, a nie wzdłuż osi zawiasu.


## v5.1.1 — poprawka wyboru profili Lofted Cut

Log v5.1.0 pokazał, że geometria profili została utworzona, ale
`IFeatureManager.InsertCutBlend` zwracało `null`.

Przyczyna była w warstwie selekcji API:

v5.1.0 wybierało bezpośrednio `SketchSegment` (okręgi).

Dokumentacja SOLIDWORKS dla loftu wskazuje wybór **profili szkiców** przez
`IModelDocExtension.SelectByID2`, z selection mark `1`.

v5.1.1:

1. tworzy każdy kołowy przekrój w osobnym szkicu 3D,
2. pobiera `Feature` tego szkicu,
3. nadaje mu stabilną nazwę,
4. wybiera go przez:

```csharp
Model.Extension.SelectByID2(
    nazwaSzkicu,
    "SKETCH",
    0, 0, 0,
    append,
    1,
    null,
    0);
```

5. dopiero wtedy wykonuje `IFeatureManager.InsertCutBlend`.

Nie ma fallbacku Booleanowego.

Jeżeli dana wersja SOLIDWORKS odrzuci pełny zamknięty loft (mimo poprawnej
selekcji profili), generator automatycznie dzieli tę samą pętlę 360° na
odcinki do 45°. Sąsiednie odcinki współdzielą profil graniczny, a ostatni
kończy się jawnym profilem 360° pokrywającym się z profilem 0°. Każdy odcinek
jest nadal natywnym `Lofted Cut`; mechanizm naprawczy nie tworzy brył
narzędziowych i nie wykonuje operacji Boolean.
