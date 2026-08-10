using System;
using System.Globalization;
using System.Text;

namespace GeneratorBebnaKrzywkowego.Core
{
    public enum PrawoRuchu
    {
        Wielomian345,
        Cykloidalne,
        Liniowe
    }

    public sealed class KonfiguracjaGeneratora
    {
        public int WersjaFormatu { get; set; } = 2;

        public double SrednicaZewnetrznaBebnaMm { get; set; }
        public double SrednicaOtworuBebnaMm { get; set; }
        public double DlugoscBebnaMm { get; set; }

        public double SrednicaZewnetrznaLozyskaMm { get; set; }
        public double SzerokoscLozyskaMm { get; set; }

        public double DlugoscRamieniaMm { get; set; }
        public double OsZawiasuOdGornejKrawedziMm { get; set; }
        public double KatPoczatkowyRamieniaStopnie { get; set; }
        public double KatKoncowyRamieniaStopnie { get; set; }

        public double WznosStopnie { get; set; }
        public double PostojWysokiStopnie { get; set; }
        public double PowrotStopnie { get; set; }

        // Ustawienia opcjonalne.
        public double LuzPromieniowyMm { get; set; } = 0.10;
        public double LuzOsiowyMm { get; set; } = 0.10;
        public PrawoRuchu PrawoRuchu { get; set; } = PrawoRuchu.Wielomian345;
        public int KierunekObrotu { get; set; } = +1;

        // Parametry loftu. To NIE jest sampling walców/Booleanów.
        public double MaksymalnyKrokProfiliStopnie { get; set; } = 7.5;
        public double MaksymalnaZmianaRamieniaNaProfilStopnie { get; set; } = 5.0;

        // Automatyczne przybliżenie skończonej szerokości rolki przez
        // kilka gładkich, zamkniętych loftów NURBS.
        public double TolerancjaSzerokosciMm { get; set; } = 0.02;

        // Zachowane dla zgodności YAML. v5.1.1 zawsze tworzy osobny szkic 3D
        // dla każdego profilu, ponieważ InsertCutBlend wybiera PROFILE SZKICÓW,
        // a nie niezależne SketchSegmenty z jednego szkicu.
        public bool PreferujZwartySzkic3D { get; set; } = false;

        public bool PokazProfilePoWygenerowaniu { get; set; } = false;

        // Wartości pochodne.
        public double PromienBebnaMm => SrednicaZewnetrznaBebnaMm / 2.0;
        public double PromienLozyskaMm => SrednicaZewnetrznaLozyskaMm / 2.0;

        // Zgodnie z geometrią użytkownika:
        // Rp = R_bębna + R_łożyska.
        public double PromienPozornegoOkreguOsiZawiasuMm =>
            PromienBebnaMm + PromienLozyskaMm;

        public double PrzesuniecieOsiZawiasuZMm =>
            DlugoscBebnaMm / 2.0 - OsZawiasuOdGornejKrawedziMm;

        public double SkokRamieniaStopnie =>
            KatKoncowyRamieniaStopnie - KatPoczatkowyRamieniaStopnie;

        public double PostojNiskiKoncowyStopnie =>
            360.0 - WznosStopnie - PostojWysokiStopnie - PowrotStopnie;

        public double PromienProfiluNarzedziaMm =>
            PromienLozyskaMm + LuzPromieniowyMm;

        public double PolSzerokosciNarzedziaMm =>
            SzerokoscLozyskaMm / 2.0 + LuzOsiowyMm;

        public void Waliduj()
        {
            if (WersjaFormatu != 2)
                throw new InvalidOperationException(
                    "Nieobsługiwana wersja_formatu YAML: " + WersjaFormatu +
                    ". Oczekiwana: 2.");

            WymagajDodatniej(SrednicaZewnetrznaBebnaMm, "bęben.średnica_zewnętrzna_mm");
            WymagajNieujemnej(SrednicaOtworuBebnaMm, "bęben.średnica_otworu_mm");
            WymagajDodatniej(DlugoscBebnaMm, "bęben.długość_mm");
            WymagajDodatniej(SrednicaZewnetrznaLozyskaMm, "łożysko.średnica_zewnętrzna_mm");
            WymagajDodatniej(SzerokoscLozyskaMm, "łożysko.szerokość_mm");
            WymagajDodatniej(DlugoscRamieniaMm, "ramię.długość_mm");

            if (SrednicaOtworuBebnaMm >= SrednicaZewnetrznaBebnaMm)
                throw new InvalidOperationException(
                    "Średnica otworu bębna musi być mniejsza od średnicy zewnętrznej.");

            if (OsZawiasuOdGornejKrawedziMm < 0 ||
                OsZawiasuOdGornejKrawedziMm > DlugoscBebnaMm)
                throw new InvalidOperationException(
                    "ramię.oś_zawiasu_od_górnej_krawędzi_mm musi mieścić się w długości bębna.");

            WymagajDodatniej(WznosStopnie, "krzywka.wznos_deg");
            WymagajNieujemnej(PostojWysokiStopnie, "krzywka.postój_wysoki_deg");
            WymagajDodatniej(PowrotStopnie, "krzywka.powrót_deg");

            if (PostojNiskiKoncowyStopnie < -1e-9)
                throw new InvalidOperationException(
                    "Suma faz krzywki przekracza 360°.");

            if (Math.Abs(SkokRamieniaStopnie) < 1e-9)
                throw new InvalidOperationException(
                    "Kąt początkowy i końcowy ramienia nie mogą być takie same.");

            WymagajNieujemnej(LuzPromieniowyMm, "zaawansowane.luz_promieniowy_mm");
            WymagajNieujemnej(LuzOsiowyMm, "zaawansowane.luz_osiowy_mm");
            WymagajDodatniej(MaksymalnyKrokProfiliStopnie,
                "zaawansowane.maksymalny_krok_profili_deg");
            WymagajDodatniej(MaksymalnaZmianaRamieniaNaProfilStopnie,
                "zaawansowane.maksymalna_zmiana_ramienia_na_profil_deg");
            WymagajDodatniej(TolerancjaSzerokosciMm,
                "zaawansowane.tolerancja_szerokosci_mm");

            if (KierunekObrotu != 1 && KierunekObrotu != -1)
                throw new InvalidOperationException(
                    "Kierunek obrotu musi mieć wartość +1 albo -1.");
        }

        public string ZbudujPodsumowanie()
        {
            var ci = CultureInfo.GetCultureInfo("pl-PL");
            var s = new StringBuilder();

            s.AppendLine("PARAMETRY KONSTRUKCJI:");
            s.AppendLine($"Bęben OD / ID [mm]        : {SrednicaZewnetrznaBebnaMm.ToString("0.###", ci)} / {SrednicaOtworuBebnaMm.ToString("0.###", ci)}");
            s.AppendLine($"Długość bębna [mm]        : {DlugoscBebnaMm.ToString("0.###", ci)}");
            s.AppendLine($"Łożysko D x W [mm]        : {SrednicaZewnetrznaLozyskaMm.ToString("0.###", ci)} x {SzerokoscLozyskaMm.ToString("0.###", ci)}");
            s.AppendLine($"Długość ramienia [mm]     : {DlugoscRamieniaMm.ToString("0.###", ci)}");
            s.AppendLine();

            s.AppendLine("LICZONE AUTOMATYCZNIE:");
            s.AppendLine($"Promień bębna R [mm]      : {PromienBebnaMm.ToString("0.###", ci)}");
            s.AppendLine($"Promień łożyska r [mm]    : {PromienLozyskaMm.ToString("0.###", ci)}");
            s.AppendLine($"Promień osi zawiasu Rp     : {PromienPozornegoOkreguOsiZawiasuMm.ToString("0.###", ci)} mm (= R + r)");
            s.AppendLine($"Średnica okręgu osi zaw.   : {(2.0 * PromienPozornegoOkreguOsiZawiasuMm).ToString("0.###", ci)} mm");
            s.AppendLine($"Oś zawiasu Z [mm]          : {PrzesuniecieOsiZawiasuZMm.ToString("0.###", ci)}");
            s.AppendLine($"Ruch ramienia [deg]        : {KatPoczatkowyRamieniaStopnie.ToString("0.###", ci)} -> {KatKoncowyRamieniaStopnie.ToString("0.###", ci)}");
            s.AppendLine($"Końcowy niski postój [deg] : {PostojNiskiKoncowyStopnie.ToString("0.###", ci)}");
            s.AppendLine("Oś łożyska przy 180°       : równoległa do osi bębna (+Z)");
            s.AppendLine("Oś łożyska przy 270°       : radialna, w kierunku osi bębna");
            s.AppendLine("Model ruchu                : sztywne wahadło wokół osi zawiasu");
            s.AppendLine();

            s.AppendLine("SILNIK SURFACE-LOFT:");
            s.AppendLine($"Promień profilu [mm]       : {PromienProfiluNarzedziaMm.ToString("0.###", ci)}");
            s.AppendLine($"Połowa szerokości [mm]     : {PolSzerokosciNarzedziaMm.ToString("0.###", ci)}");
            s.AppendLine($"Maks. krok profili [deg]   : {MaksymalnyKrokProfiliStopnie.ToString("0.###", ci)}");
            s.AppendLine($"Maks. Δ ramienia/profil    : {MaksymalnaZmianaRamieniaNaProfilStopnie.ToString("0.###", ci)} deg");
            s.AppendLine($"Tol. szerokości [mm]       : {TolerancjaSzerokosciMm.ToString("0.###", ci)}");

            return s.ToString().TrimEnd();
        }

        private static void WymagajDodatniej(double wartosc, string nazwa)
        {
            if (!(wartosc > 0.0) || double.IsNaN(wartosc) || double.IsInfinity(wartosc))
                throw new InvalidOperationException(nazwa + " musi być > 0.");
        }

        private static void WymagajNieujemnej(double wartosc, string nazwa)
        {
            if (wartosc < 0.0 || double.IsNaN(wartosc) || double.IsInfinity(wartosc))
                throw new InvalidOperationException(nazwa + " musi być >= 0.");
        }
    }
}
