using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GeneratorBebnaKrzywkowego.Core
{
    /// <summary>
    /// Restrykcyjny parser podzbioru YAML wystarczającego dla konfiguracji
    /// generatora. Celowo brak list, kotwic i wieloliniowych bloków.
    /// Dzięki temu literówki w parametrach nie są cicho ignorowane.
    /// </summary>
    public static class ParserYaml
    {
        public static KonfiguracjaGeneratora Wczytaj(string sciezka)
        {
            if (!File.Exists(sciezka))
                throw new FileNotFoundException("Nie znaleziono pliku YAML.", sciezka);

            Dictionary<string, string> mapa = WczytajMape(sciezka);
            WalidujNieznaneKlucze(mapa);

            var k = new KonfiguracjaGeneratora
            {
                WersjaFormatu = PobierzInt(mapa, "wersja_formatu"),

                SrednicaZewnetrznaBebnaMm = PobierzDouble(mapa, "beben.srednica_zewnetrzna_mm"),
                SrednicaOtworuBebnaMm = PobierzDouble(mapa, "beben.srednica_otworu_mm"),
                DlugoscBebnaMm = PobierzDouble(mapa, "beben.dlugosc_mm"),

                SrednicaZewnetrznaLozyskaMm = PobierzDouble(mapa, "lozysko.srednica_zewnetrzna_mm"),
                SzerokoscLozyskaMm = PobierzDouble(mapa, "lozysko.szerokosc_mm"),

                DlugoscRamieniaMm = PobierzDouble(mapa, "ramie.dlugosc_mm"),
                OsZawiasuOdGornejKrawedziMm = PobierzDouble(mapa, "ramie.os_zawiasu_od_gornej_krawedzi_mm"),
                KatPoczatkowyRamieniaStopnie = PobierzDouble(mapa, "ramie.kat_poczatkowy_deg"),
                KatKoncowyRamieniaStopnie = PobierzDouble(mapa, "ramie.kat_koncowy_deg"),

                WznosStopnie = PobierzDouble(mapa, "krzywka.wznos_deg"),
                PostojWysokiStopnie = PobierzDouble(mapa, "krzywka.postoj_wysoki_deg"),
                PowrotStopnie = PobierzDouble(mapa, "krzywka.powrot_deg"),

                LuzPromieniowyMm = PobierzDoubleOpcjonalny(mapa, "zaawansowane.luz_promieniowy_mm", 0.10),
                LuzOsiowyMm = PobierzDoubleOpcjonalny(mapa, "zaawansowane.luz_osiowy_mm", 0.10),

                MaksymalnyKrokProfiliStopnie = PobierzDoubleOpcjonalny(
                    mapa, "zaawansowane.maksymalny_krok_profili_deg", 7.5),

                MaksymalnaZmianaRamieniaNaProfilStopnie = PobierzDoubleOpcjonalny(
                    mapa, "zaawansowane.maksymalna_zmiana_ramienia_na_profil_deg", 5.0),

                TolerancjaSzerokosciMm = PobierzDoubleOpcjonalny(
                    mapa, "zaawansowane.tolerancja_szerokosci_mm", 0.02),

                PreferujZwartySzkic3D = PobierzBoolOpcjonalny(
                    mapa, "zaawansowane.preferuj_zwarty_szkic_3d", true),

                PokazProfilePoWygenerowaniu = PobierzBoolOpcjonalny(
                    mapa, "zaawansowane.pokaz_profile_po_wygenerowaniu", false),
            };

            k.KierunekObrotu = PobierzKierunekOpcjonalny(
                mapa, "zaawansowane.kierunek_obrotu", +1);

            k.PrawoRuchu = PobierzPrawoRuchuOpcjonalne(
                mapa, "zaawansowane.prawo_ruchu", PrawoRuchu.Wielomian345);

            return k;
        }

        private static Dictionary<string, string> WczytajMape(string sciezka)
        {
            string[] linie = File.ReadAllLines(sciezka, new UTF8Encoding(false, true));
            var wynik = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sekcje = new string[32];

            for (int indeks = 0; indeks < linie.Length; indeks++)
            {
                string surowa = linie[indeks] ?? string.Empty;

                if (surowa.Contains("\t"))
                    throw BladYaml(indeks, "Tabulatory są zabronione; użyj spacji.");

                string bezKomentarza = UsunKomentarz(surowa);
                if (string.IsNullOrWhiteSpace(bezKomentarza))
                    continue;

                string tresc = bezKomentarza.Trim();
                if (tresc == "---" || tresc == "...")
                    continue;

                int spacje = LiczSpacjePoczatkowe(bezKomentarza);
                if ((spacje % 2) != 0)
                    throw BladYaml(indeks, "Wcięcie musi być wielokrotnością 2 spacji.");

                int poziom = spacje / 2;
                if (poziom >= sekcje.Length)
                    throw BladYaml(indeks, "Zbyt głębokie zagnieżdżenie YAML.");

                int dwukropek = ZnajdzDwukropek(tresc);
                if (dwukropek <= 0)
                    throw BladYaml(indeks, "Oczekiwano zapisu: klucz: wartość");

                string klucz = NormalizujKlucz(tresc.Substring(0, dwukropek));
                string wartosc = tresc.Substring(dwukropek + 1).Trim();

                if (string.IsNullOrWhiteSpace(klucz))
                    throw BladYaml(indeks, "Pusty klucz.");

                if (string.IsNullOrEmpty(wartosc))
                {
                    sekcje[poziom] = klucz;
                    for (int i = poziom + 1; i < sekcje.Length; i++)
                        sekcje[i] = null;
                    continue;
                }

                var czesci = new List<string>();
                for (int i = 0; i < poziom; i++)
                {
                    if (string.IsNullOrEmpty(sekcje[i]))
                        throw BladYaml(indeks, "Brak sekcji nadrzędnej dla wcięcia.");
                    czesci.Add(sekcje[i]);
                }
                czesci.Add(klucz);

                string sciezkaKlucza = string.Join(".", czesci);
                if (wynik.ContainsKey(sciezkaKlucza))
                    throw BladYaml(indeks, "Klucz występuje drugi raz: " + sciezkaKlucza);

                wynik.Add(sciezkaKlucza, OdkodujWartosc(wartosc));
            }

            return wynik;
        }

        private static void WalidujNieznaneKlucze(Dictionary<string, string> mapa)
        {
            string[] dozwolone =
            {
                "wersja_formatu",

                "beben.srednica_zewnetrzna_mm",
                "beben.srednica_otworu_mm",
                "beben.dlugosc_mm",

                "lozysko.srednica_zewnetrzna_mm",
                "lozysko.szerokosc_mm",

                "ramie.dlugosc_mm",
                "ramie.os_zawiasu_od_gornej_krawedzi_mm",
                "ramie.kat_poczatkowy_deg",
                "ramie.kat_koncowy_deg",

                "krzywka.wznos_deg",
                "krzywka.postoj_wysoki_deg",
                "krzywka.powrot_deg",

                "zaawansowane.luz_promieniowy_mm",
                "zaawansowane.luz_osiowy_mm",
                "zaawansowane.prawo_ruchu",
                "zaawansowane.kierunek_obrotu",
                "zaawansowane.maksymalny_krok_profili_deg",
                "zaawansowane.maksymalna_zmiana_ramienia_na_profil_deg",
                "zaawansowane.tolerancja_szerokosci_mm",
                "zaawansowane.preferuj_zwarty_szkic_3d",
                "zaawansowane.pokaz_profile_po_wygenerowaniu"
            };

            var zbior = new HashSet<string>(
                dozwolone.Select(NormalizujSciezke),
                StringComparer.OrdinalIgnoreCase);

            string[] nieznane = mapa.Keys
                .Where(x => !zbior.Contains(x))
                .OrderBy(x => x)
                .ToArray();

            if (nieznane.Length > 0)
                throw new InvalidOperationException(
                    "YAML zawiera nieznane parametry (literówka lub stary format):" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, nieznane.Select(x => " - " + x)));
        }

        private static int PobierzInt(Dictionary<string, string> mapa, string sciezka)
        {
            double v = PobierzDouble(mapa, sciezka);
            int n = checked((int)Math.Round(v));

            if (Math.Abs(v - n) > 1e-9)
                throw new InvalidOperationException(sciezka + " musi być liczbą całkowitą.");

            return n;
        }

        private static double PobierzDouble(Dictionary<string, string> mapa, string sciezka)
        {
            string klucz = NormalizujSciezke(sciezka);

            if (!mapa.TryGetValue(klucz, out string tekst))
                throw new InvalidOperationException("Brak wymaganego parametru YAML: " + sciezka);

            if (!SprobujDouble(tekst, out double v))
                throw new InvalidOperationException(
                    "Parametr nie jest liczbą: " + sciezka + " = " + tekst);

            return v;
        }

        private static double PobierzDoubleOpcjonalny(
            Dictionary<string, string> mapa, string sciezka, double domyslna)
        {
            string klucz = NormalizujSciezke(sciezka);
            if (!mapa.TryGetValue(klucz, out string tekst))
                return domyslna;

            if (!SprobujDouble(tekst, out double v))
                throw new InvalidOperationException(
                    "Parametr nie jest liczbą: " + sciezka + " = " + tekst);

            return v;
        }

        private static bool PobierzBoolOpcjonalny(
            Dictionary<string, string> mapa, string sciezka, bool domyslna)
        {
            string klucz = NormalizujSciezke(sciezka);
            if (!mapa.TryGetValue(klucz, out string tekst))
                return domyslna;

            string s = NormalizujKlucz(tekst);
            if (s == "true" || s == "tak" || s == "yes" || s == "1" || s == "wlaczone")
                return true;
            if (s == "false" || s == "nie" || s == "no" || s == "0" || s == "wylaczone")
                return false;

            throw new InvalidOperationException(
                "Niepoprawna wartość logiczna: " + sciezka + " = " + tekst);
        }

        private static int PobierzKierunekOpcjonalny(
            Dictionary<string, string> mapa, string sciezka, int domyslny)
        {
            string klucz = NormalizujSciezke(sciezka);
            if (!mapa.TryGetValue(klucz, out string tekst))
                return domyslny;

            string s = NormalizujKlucz(tekst);
            if (s == "1" || s == "dodatni" || s == "plus" || s == "zgodny")
                return +1;
            if (s == "-1" || s == "ujemny" || s == "minus" || s == "przeciwny")
                return -1;

            throw new InvalidOperationException(
                "Niepoprawny kierunek obrotu: " + tekst);
        }

        private static PrawoRuchu PobierzPrawoRuchuOpcjonalne(
            Dictionary<string, string> mapa, string sciezka, PrawoRuchu domyslne)
        {
            string klucz = NormalizujSciezke(sciezka);
            if (!mapa.TryGetValue(klucz, out string tekst))
                return domyslne;

            string s = NormalizujKlucz(tekst);
            if (s == "wielomian_3_4_5" || s == "wielomian_345" || s == "3_4_5")
                return PrawoRuchu.Wielomian345;
            if (s == "cykloidalne" || s == "cykloidalny" || s == "cycloidal")
                return PrawoRuchu.Cykloidalne;
            if (s == "liniowe" || s == "liniowy" || s == "linear")
                return PrawoRuchu.Liniowe;

            throw new InvalidOperationException(
                "Nieznane prawo ruchu: " + tekst);
        }

        private static bool SprobujDouble(string tekst, out double wynik)
        {
            string s = (tekst ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(
                s,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out wynik);
        }

        public static string NormalizujSciezke(string sciezka)
        {
            return string.Join(".",
                (sciezka ?? string.Empty)
                    .Split('.')
                    .Select(NormalizujKlucz));
        }

        public static string NormalizujKlucz(string tekst)
        {
            string s = (tekst ?? string.Empty).Trim().ToLowerInvariant();

            s = s
                .Replace("ą", "a")
                .Replace("ć", "c")
                .Replace("ę", "e")
                .Replace("ł", "l")
                .Replace("ń", "n")
                .Replace("ó", "o")
                .Replace("ś", "s")
                .Replace("ź", "z")
                .Replace("ż", "z")
                .Replace("-", "_")
                .Replace(" ", "_");

            while (s.Contains("__"))
                s = s.Replace("__", "_");

            return s;
        }

        private static int LiczSpacjePoczatkowe(string s)
        {
            int i = 0;
            while (i < s.Length && s[i] == ' ')
                i++;
            return i;
        }

        private static int ZnajdzDwukropek(string s)
        {
            bool pojedynczy = false;
            bool podwojny = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !podwojny) pojedynczy = !pojedynczy;
                else if (c == '"' && !pojedynczy) podwojny = !podwojny;
                else if (c == ':' && !pojedynczy && !podwojny) return i;
            }

            return -1;
        }

        private static string UsunKomentarz(string s)
        {
            bool pojedynczy = false;
            bool podwojny = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !podwojny) pojedynczy = !pojedynczy;
                else if (c == '"' && !pojedynczy) podwojny = !podwojny;
                else if (c == '#' && !pojedynczy && !podwojny) return s.Substring(0, i);
            }

            return s;
        }

        private static string OdkodujWartosc(string s)
        {
            s = (s ?? string.Empty).Trim();

            if (s.Length >= 2)
            {
                if ((s[0] == '"' && s[s.Length - 1] == '"') ||
                    (s[0] == '\'' && s[s.Length - 1] == '\''))
                {
                    s = s.Substring(1, s.Length - 2);
                }
            }

            return s.Trim();
        }

        private static Exception BladYaml(int indeksZeroBased, string komunikat)
        {
            return new InvalidOperationException(
                "YAML, linia " + (indeksZeroBased + 1) + ": " + komunikat);
        }
    }
}
