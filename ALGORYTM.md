# Algorytm Surface-Loft v5

## 1. Co zostało porzucone

Kod C# nie używa:

- `IBody2.Operations2`,
- `SWBODYCUT`,
- sekwencji `CreateBodyFromCyl -> odejmij`,
- setek kolejnych Booleanów dla pozycji łożyska.

`CreateBodyFromCyl` jest użyte **wyłącznie raz** do zbudowania bazowego pełnego
walca bębna. Nie wykonuje operacji odejmowania.

## 2. Kinematyka

Dla kąta bębna `θ`:

- `er = (cos θ, sin θ, 0)` — lokalny kierunek radialny,
- `et = Z × er` — kierunek styczny,
- `Rp = R_bębna + R_łożyska`,
- `P = Rp·er + z_zawiasu·Z`,
- `a = cos(α)·er + sin(α)·Z`,
- `C = P + L·a`.

Współrzędna osiowa środka wynosi `Z(C) = z_zawiasu + L·sin(α)`.

Położenie zamknięte/dolne przyjmujemy jako `α_z = 270°`, a położenie
otwarte/uniesione jako `α_o = 180°`. „Wznos” opisuje wzrost `Z(C)`, mimo że
w tym przykładzie wartość kąta maleje. Dla postępu `s(u)` prawa ruchu:

- wznos: `α(u) = α_z + (α_o-α_z)·s(u)` — łożysko jest unoszone,
- postój wysoki: `α(u) = α_o` — maksymalna wysokość jest stała,
- powrót: `α(u) = α_o + (α_z-α_o)·s(u)` — łożysko jest opuszczane,
- postój niski: `α(u) = α_z`.

Oś łożyska `u(α)` obraca się wraz z ramieniem (pełne równanie poniżej).

Podane niżej `u` oznacza kierunek osi łożyska, nie postęp fazy. Dla stacji
szerokości `t`:

`C_t = C + t·u(α)`.

W punkcie `C_t` tworzony jest profil kołowy o promieniu:

`r_narzędzia = D_łożyska/2 + luz_promieniowy`.

Płaszczyzna profilu ma normalną `u(α)`.

## 3. Powierzchnia / loft

Dla jednej stacji `t` generator tworzy zestaw kolejnych zamkniętych profili
kołowych w szkicu 3D i przekazuje je do natywnego **Lofted Cut** SOLIDWORKS.

SOLIDWORKS interpoluje pomiędzy profilami powierzchniami loftowanymi/NURBS.
Loft jest zamknięty przez 360°.

To oznacza, że rdzeniem geometrii nie jest seria cylindrycznych Booleanów,
lecz jedna ciągła operacja loftowana dla danej stacji szerokości.

## 4. Skończona szerokość łożyska

Rolka ma szerokość, dlatego pojedynczy loft przez środki przekrojów nie
reprezentowałby całej szerokości.

Generator automatycznie tworzy kilka przesuniętych loftów dla:

`t ∈ [-B/2-luz, +B/2+luz]`.

Liczba stacji jest dobierana z tolerancji.

Dla promienia profilu `r`, odległości między sąsiednimi stacjami `s`
i dopuszczalnego niedomiaru `e`:

`e = r - sqrt(r² - (s/2)²)`

czyli:

`s_max = 2*sqrt(2*r*e - e²)`.

Dla typowej konfiguracji:

- r = 15.1 mm,
- pełna szerokość = 9.2 mm,
- e = 0.02 mm,

otrzymujemy około 7 stacji.

## 5. Profile kątowe

Profile nie są rozmieszczane co 0.5°.

Liczba przekrojów wynika z dwóch ograniczeń:

1. maksymalnego kroku bębna między profilami,
2. maksymalnej zmiany kąta ramienia między profilami.

Prawo 3-4-5 ma maksymalną pochodną 1.875, więc przejścia są automatycznie
zagęszczane bardziej niż odcinki dwell.

## 6. Otwór centralny

Otwór Ø53 jest wykonywany **na końcu**, również jako natywne otwarte
Lofted Cut przez dwa kołowe profile znajdujące się poza czołami bębna.

Nie ma `Operations2`.

## 7. Dlaczego są dwa tryby szkiców

Preferowany tryb:

- jeden szkic 3D na loft,
- wiele kołowych `SketchSegment`,
- wybór segmentów mark=1 w kolejności loftu.

Jeśli dana wersja SOLIDWORKS nie zaakceptuje wielu profili z jednego szkicu
3D, generator automatycznie tworzy ten sam loft z osobnych szkiców 3D.

To jest fallback organizacji profili — **nie fallback Booleanowy**.


# Korekta kinematyki v5.1

Wcześniejsze uproszczenie `BearingAxis = et` było błędne dla tego mechanizmu.

Definicje:

`er` — lokalny promień bębna  
`et = Z × er` — styczna oś zawiasu  
`α` — aktualny kąt ramienia

Ramię:

`a(α) = cos(α)·er + sin(α)·Z`

Oś łożyska:

`u(α) = sin(α)·er - cos(α)·Z`

Własności:

`a · u = 0`

`u · et = 0`

`a · et = 0`

Zatem trójka `(a, -et, u)` tworzy lokalny ortonormalny układ sztywno
obracający się wokół zawiasu.

Przekrój loftu ma teraz:

- środek: `C + t·u`,
- lokalne X: `a`,
- lokalne Y: `-et`,
- normalną: `u`.

To sprawia, że zarówno położenie środka, jak i orientacja płaszczyzny
łożyska wykonują właściwy ruch wahadłowy.
