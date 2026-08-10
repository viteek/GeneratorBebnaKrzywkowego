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
