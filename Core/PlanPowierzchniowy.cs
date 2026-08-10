using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneratorBebnaKrzywkowego.Core
{
    public sealed class PlanPowierzchniowy
    {
        public IReadOnlyList<double> KatyProgramoweStopnie { get; private set; }
        public IReadOnlyList<double> PrzesunieciaPoSzerokosciMm { get; private set; }

        private PlanPowierzchniowy()
        {
        }

        public static PlanPowierzchniowy Zbuduj(KonfiguracjaGeneratora k)
        {
            var kin = new Kinematyka(k);

            return new PlanPowierzchniowy
            {
                KatyProgramoweStopnie = ZbudujKaty(k, kin),
                PrzesunieciaPoSzerokosciMm = ZbudujStacjeSzerokosci(k)
            };
        }

        private static IReadOnlyList<double> ZbudujKaty(
            KonfiguracjaGeneratora k,
            Kinematyka kin)
        {
            var wynik = new List<double>();

            double start = 0.0;

            DodajFaze(
                wynik,
                start,
                k.WznosStopnie,
                Math.Abs(k.SkokRamieniaStopnie),
                kin.MaksymalnaPochodnaPrawaRuchu(),
                k);

            start += k.WznosStopnie;

            DodajFaze(
                wynik,
                start,
                k.PostojWysokiStopnie,
                0.0,
                0.0,
                k);

            start += k.PostojWysokiStopnie;

            DodajFaze(
                wynik,
                start,
                k.PowrotStopnie,
                Math.Abs(k.SkokRamieniaStopnie),
                kin.MaksymalnaPochodnaPrawaRuchu(),
                k);

            start += k.PowrotStopnie;

            DodajFaze(
                wynik,
                start,
                k.PostojNiskiKoncowyStopnie,
                0.0,
                0.0,
                k);

            // Nie dodajemy profilu 360°, bo zamknięty loft łączy ostatni
            // profil z profilem 0°.
            return wynik
                .Where(x => x >= 0.0 && x < 360.0 - 1e-9)
                .Distinct(new DoubleComparer(1e-9))
                .OrderBy(x => x)
                .ToList();
        }

        private static void DodajFaze(
            List<double> wynik,
            double poczatek,
            double zakres,
            double zmianaRamienia,
            double maksPochodna,
            KonfiguracjaGeneratora k)
        {
            if (zakres <= 1e-12)
                return;

            int nOdKata = Math.Max(
                1,
                (int)Math.Ceiling(zakres / k.MaksymalnyKrokProfiliStopnie));

            int nOdRamienia = 1;
            if (zmianaRamienia > 1e-12)
            {
                nOdRamienia = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        zmianaRamienia * maksPochodna /
                        k.MaksymalnaZmianaRamieniaNaProfilStopnie));
            }

            int n = Math.Max(nOdKata, nOdRamienia);

            for (int i = 0; i < n; i++)
            {
                double phi = poczatek + zakres * i / n;
                wynik.Add(phi);
            }
        }

        private static IReadOnlyList<double> ZbudujStacjeSzerokosci(
            KonfiguracjaGeneratora k)
        {
            double r = k.PromienProfiluNarzedziaMm;
            double h = k.PolSzerokosciNarzedziaMm;
            double eps = k.TolerancjaSzerokosciMm;

            if (h <= 1e-12)
                return new[] { 0.0 };

            // Dla dwóch sąsiednich dysków o promieniu r, których środki są
            // oddalone o s wzdłuż osi rolki, maksymalny niedomiar pomiędzy
            // nimi wynosi:
            //
            // e = r - sqrt(r^2 - (s/2)^2)
            //
            // stąd:
            // s_max = 2 * sqrt(2*r*e - e^2)
            //
            // Używamy tego jako automatycznej kontroli przybliżenia
            // skończonej szerokości rolki przez kilka gładkich loftów.
            double epsEfektywne = Math.Min(eps, r * 0.95);
            double podPierwiastkiem =
                Math.Max(1e-12, 2.0 * r * epsEfektywne - epsEfektywne * epsEfektywne);

            double sMax = 2.0 * Math.Sqrt(podPierwiastkiem);

            int przedzialy = Math.Max(
                2,
                (int)Math.Ceiling((2.0 * h) / sMax));

            // Parzysta liczba przedziałów => nieparzysta liczba stacji
            // i dokładna stacja t=0.
            if ((przedzialy % 2) != 0)
                przedzialy++;

            double krok = 2.0 * h / przedzialy;

            var naturalne = new List<double>();
            for (int i = 0; i <= przedzialy; i++)
                naturalne.Add(-h + i * krok);

            // Kolejność tworzenia: środek, +, -, +, -...
            // Dzięki temu każdy następny loft rozszerza istniejącą bieżnię,
            // zamiast zaczynać od skrajnej warstwy.
            return naturalne
                .OrderBy(x => Math.Abs(x))
                .ThenByDescending(x => x)
                .ToList();
        }

        private sealed class DoubleComparer : IEqualityComparer<double>
        {
            private readonly double _eps;

            public DoubleComparer(double eps)
            {
                _eps = eps;
            }

            public bool Equals(double x, double y) => Math.Abs(x - y) <= _eps;
            public int GetHashCode(double obj) => 0;
        }
    }
}
