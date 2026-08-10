# Kinematyka wahadła — v5.1

## Co było źle

W v5.0.x:

`oś łożyska = kierunek styczny et`

czyli oś łożyska pozostawała równoległa do osi zawiasu podczas całego
wychylenia ramienia.

To nie odpowiada mechanizmowi, w którym oś łożyska jest sztywno związana
z ramieniem i początkowo równoległa do osi bębna.

## Poprawny model

Oś zawiasu:

`h = et`

Ramię:

`a = cos(α) er + sin(α) Z`

Oś łożyska:

`u = sin(α) er - cos(α) Z`

Środek łożyska i jego współrzędna osiowa:

`C = P + L·a`

`Z(C) = z_zawiasu + L·sin(α)`

## Wznos, postój wysoki i powrót

Nazwy faz odnoszą się do wysokości łożyska, nie do znaku zmiany `α`.
Przyjmujemy `α_zamknięcia = 270°` (ramię w dół, minimum `Z`) oraz
`α_otwarcia = 180°` (łożysko uniesione). Dlatego podczas wznosu kąt
liczbowo maleje z 270° do 180°, ale `Z(C)` rośnie z `z_zawiasu-L` do
`z_zawiasu`.

Dla postępu `s(q)`, gdzie `s(0)=0` i `s(1)=1`:

- wznos: `α(q)=α_zamknięcia+(α_otwarcia-α_zamknięcia)·s(q)`,
- postój wysoki: `α=α_otwarcia`,
- powrót: `α(q)=α_otwarcia+(α_zamknięcia-α_otwarcia)·s(q)`,
- postój niski: `α=α_zamknięcia`.

W ten sposób koniec wznosu i początek postoju wysokiego, a także koniec
postoju i początek powrotu mają identyczne pozycje. Koniec cyklu przy 360°
łączy się z pozycją 0° w położeniu zamkniętym.

Dla `α = 180°`:

`a = -er`
`u = +Z`

Dla `α = 225°`:

`a = -(er + Z)/sqrt(2)`
`u = (-er + Z)/sqrt(2)`

Dla `α = 270°`:

`a = -Z`
`u = -er`

Oś łożyska wykonuje więc dokładnie 90° obrotu wokół osi zawiasu razem
z ramieniem.

## Wpływ na Surface-Loft

Każdy profil kołowy ma teraz płaszczyznę:

- X = kierunek ramienia,
- Y = minus oś zawiasu,
- normalna = aktualna oś łożyska.

Stacje szerokości są przesuwane:

`C_t = C + t*u`

a nie `C + t*et`.

To jest istotne: cały "pakiet" szerokości łożyska obraca się z wahadłem.
