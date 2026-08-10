using System.Text;

namespace GeneratorBebnaKrzywkowego.SolidWorks
{
    public sealed class RaportGenerowania
    {
        public int LiczbaProfiliKatowych { get; set; }
        public int LiczbaStacjiSzerokosci { get; set; }
        public int LiczbaLoftowKrzywki { get; set; }
        public int LiczbaFallbackowSzkicow { get; set; }
        public bool OtworCentralnyWykonany { get; set; }

        public string DoTekstu()
        {
            var s = new StringBuilder();
            s.AppendLine("CAM_DRUM_FINAL utworzony.");
            s.AppendLine("Profile kątowe            : " + LiczbaProfiliKatowych);
            s.AppendLine("Stacje szerokości         : " + LiczbaStacjiSzerokosci);
            s.AppendLine("Natywne loft-cut krzywki  : " + LiczbaLoftowKrzywki);
            s.AppendLine("Fallback osobnych szkiców : " + LiczbaFallbackowSzkicow);
            s.AppendLine("Otwór centralny           : " +
                         (OtworCentralnyWykonany ? "TAK" : "brak / Ø0"));
            s.AppendLine();
            s.AppendLine("IBody2.Operations2: NIE UŻYWANE.");
            s.AppendLine("Sekwencyjne Booleany walców: NIE UŻYWANE.");
            return s.ToString().TrimEnd();
        }
    }
}
