# Loft profile selection fix — v5.1.1

## Objaw v5.1.0

```text
Loft 1/7, t = 0 mm
Jeden szkic 3D nie został zaakceptowany...
Fallback: każdy profil w osobnym szkicu 3D...
SOLIDWORKS nie utworzył zamkniętego Lofted Cut
```

## Błąd

Oba warianty kończyły się tą samą metodą:

`SketchSegment.Select2(..., mark: 1)`

Czyli do `InsertCutBlend` trafiały zaznaczone elementy szkicu, nie profile
szkiców w formie wymaganej przez API loftu.

## Poprawka

Każdy przekrój jest osobnym szkicem 3D.

Po zamknięciu szkicu generator pobiera jego `Feature`, nadaje nazwę:

`KRZYWKA_LOFT_01_PROFIL_001`

itd.

Następnie wybiera profile dokładnie jako typ:

`SKETCH`

przez `IModelDocExtension.SelectByID2`, mark=1.

Kolejność selekcji jest kolejnością sekcji loftu.

## Co powinno pojawić się w logu

```text
Utworzono 84 osobnych szkiców 3D; wybór profili jako SKETCH, mark=1.
Profile zaznaczone poprawnie: 84 (typ SKETCH, mark=1).
InsertCutBlend utworzył operację: ...
```

Jeżeli ostatnia linia zamiast tego nadal pokaże:

`InsertCutBlend zwróciło null`

to selekcja będzie już potwierdzona i następny problem będzie dotyczył
samej geometrii zamkniętego loftu, nie sposobu przekazania profili do API.
