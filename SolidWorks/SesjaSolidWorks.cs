using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace GeneratorBebnaKrzywkowego.SolidWorks
{
    /// <summary>
    /// Połączenie z SOLIDWORKS przez oficjalne Primary Interop Assembly.
    ///
    /// v5.0.0/v5.0.1 używały C# dynamic. Na części instalacji SOLIDWORKS
    /// obiekt COM zwracany przez ROT nie udostępnia TypeInfo w sposób,
    /// którego wymaga DynamicMetaObjectBinder. Skutkiem było:
    ///
    /// TYPE_E_ELEMENTNOTFOUND (0x8002802B)
    ///
    /// już przy sw.Visible = true.
    ///
    /// Ta wersja jest early-bound: żadnego dynamic.
    /// </summary>
    public sealed class SesjaSolidWorks : IDisposable
    {
        public SldWorks Aplikacja { get; }
        public ModelDoc2 Model { get; }
        public Modeler Modeler { get; }
        public PartDoc Czesc { get; }

        private readonly bool _uruchomionoNowaInstancje;

        private SesjaSolidWorks(
            SldWorks aplikacja,
            ModelDoc2 model,
            Modeler modeler,
            PartDoc czesc,
            bool uruchomionoNowaInstancje)
        {
            Aplikacja = aplikacja ?? throw new ArgumentNullException(nameof(aplikacja));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Modeler = modeler ?? throw new ArgumentNullException(nameof(modeler));
            Czesc = czesc ?? throw new ArgumentNullException(nameof(czesc));
            _uruchomionoNowaInstancje = uruchomionoNowaInstancje;
        }

        public static SesjaSolidWorks PolaczIUtworzNowaCzesc()
        {
            SldWorks sw = null;
            bool nowaInstancja = false;

            try
            {
                object aktywna = Marshal.GetActiveObject("SldWorks.Application");
                sw = aktywna as SldWorks;

                if (sw == null)
                    throw new InvalidCastException(
                        "Aktywny obiekt SldWorks.Application nie implementuje interfejsu PIA SldWorks.");
            }
            catch (COMException)
            {
                sw = UtworzNowaInstancje();
                nowaInstancja = true;
            }

            // Oficjalne API udostępnia Visible oraz UserControl na ISldWorks.
            // UserControl = true dodatkowo przekazuje sesję użytkownikowi,
            // dzięki czemu aplikacja nie powinna zamknąć się po zakończeniu EXE.
            sw.Visible = true;
            sw.UserControl = true;

            object nowyDokument = sw.NewPart();

            ModelDoc2 model =
                nowyDokument as ModelDoc2 ??
                sw.ActiveDoc as ModelDoc2;

            if (model == null)
                throw new InvalidOperationException(
                    "SOLIDWORKS nie utworzył nowej części. " +
                    "Sprawdź domyślny szablon części.");

            PartDoc czesc = model as PartDoc;
            if (czesc == null)
                throw new InvalidOperationException(
                    "Nowy dokument nie udostępnia IPartDoc.");

            Modeler modeler = sw.GetModeler() as Modeler;
            if (modeler == null)
                throw new InvalidOperationException(
                    "ISldWorks.GetModeler() nie zwróciło IModeler.");

            return new SesjaSolidWorks(
                sw,
                model,
                modeler,
                czesc,
                nowaInstancja);
        }

        private static SldWorks UtworzNowaInstancje()
        {
            Type typ = Type.GetTypeFromProgID("SldWorks.Application");

            if (typ == null)
                throw new InvalidOperationException(
                    "Nie znaleziono zarejestrowanego COM ProgID SldWorks.Application.");

            object instance = Activator.CreateInstance(typ);

            SldWorks sw = instance as SldWorks;
            if (sw == null)
                throw new InvalidCastException(
                    "Utworzony COM SldWorks.Application nie daje się rzutować na SolidWorks.Interop.sldworks.SldWorks.");

            return sw;
        }

        public void Dispose()
        {
            // Celowo nie zamykamy dokumentu ani SOLIDWORKS.
            // UserControl = true przekazuje sesję użytkownikowi.
        }
    }
}
