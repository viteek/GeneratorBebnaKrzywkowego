# TYPE_E_ELEMENTNOTFOUND 0x8002802B — poprawka v5.0.2

## Objaw

Program dochodził do:

`SesjaSolidWorks.PolaczIUtworzNowaCzesc()`

i kończył się na:

`sw.Visible = true`

ze stosem:

`System.Dynamic.ComRuntimeHelpers.GetITypeInfoFromIDispatch`

`TYPE_E_ELEMENTNOTFOUND (0x8002802B)`.

## Przyczyna

To nie był błąd `ISldWorks.Visible`.

`Visible` jest normalną, oficjalną właściwością `ISldWorks`.

Problemem był sposób komunikacji v5.0.x: obiekt COM był trzymany jako
C# `dynamic`. Dynamic Runtime Binder próbował najpierw pobrać `ITypeInfo`
z IDispatch, a konkretna konfiguracja COM SOLIDWORKS nie zwróciła oczekiwanego
TypeInfo.

## Rozwiązanie

v5.0.2 jest early-bound:

```csharp
using SolidWorks.Interop.sldworks;

SldWorks sw;
ModelDoc2 model;
Modeler modeler;
PartDoc part;
SketchManager sketchManager;
Sketch sketch;
SketchSegment profile;
FeatureManager featureManager;
```

Dzięki temu nie ma `DynamicMetaObjectBinder`.

## Przygotowanie PIA

Uruchom:

```powershell
.\scripts\Przygotuj-SolidWorks-Interop.ps1
```

albo po prostu zbuduj projekt — `.csproj` uruchomi skrypt automatycznie, jeżeli
`lib\SolidWorks.Interop.sldworks.dll` jeszcze nie istnieje.

Skrypt szuka biblioteki w lokalnej instalacji:

`<SOLIDWORKS>\api\redist\SolidWorks.Interop.sldworks.dll`
