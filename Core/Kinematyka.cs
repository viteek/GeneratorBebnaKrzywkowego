using System;

namespace GeneratorBebnaKrzywkowego.Core
{
    public sealed class PozycjaLozyska
    {
        public double KatProgramowyStopnie { get; set; }
        public double KatGeometrycznyStopnie { get; set; }
        public double KatRamieniaStopnie { get; set; }

        public Wektor3 KierunekPromieniowy { get; set; }

        // Oś fizycznego zawiasu ramienia. Jest styczna do pozornego okręgu.
        public Wektor3 KierunekOsiZawiasu { get; set; }

        public Wektor3 KierunekOsiBebna { get; set; }
        public Wektor3 KierunekRamienia { get; set; }

        // Oś łożyska NIE jest już utożsamiana z osią zawiasu.
        // Jest sztywno związana z ramieniem i obraca się razem z nim
        // wokół osi zawiasu jak element wahadła.
        public Wektor3 KierunekOsiLozyska { get; set; }

        public Wektor3 PunktOsiZawiasuMm { get; set; }
        public Wektor3 SrodekLozyskaMm { get; set; }
    }

    public sealed class Kinematyka
    {
        private readonly KonfiguracjaGeneratora _k;

        private static readonly Wektor3 OsBebna = new Wektor3(0, 0, 1);

        public Kinematyka(KonfiguracjaGeneratora konfiguracja)
        {
            _k = konfiguracja ?? throw new ArgumentNullException(nameof(konfiguracja));
        }

        public PozycjaLozyska Oblicz(double katProgramowyStopnie)
        {
            double katProgramowy = NormalizujKatProgramowy(katProgramowyStopnie);
            double katGeometryczny =
                _k.KierunekObrotu * katProgramowy;

            double theta = Jednostki.StopnieDoRadianow(katGeometryczny);
            double alfaStopnie = ObliczKatRamienia(katProgramowy);
            double alfa = Jednostki.StopnieDoRadianow(alfaStopnie);

            var er = new Wektor3(Math.Cos(theta), Math.Sin(theta), 0.0);

            // Fizyczna oś zawiasu jest styczna do pozornego okręgu.
            var osZawiasu =
                Wektor3.IloczynWektorowy(OsBebna, er).Znormalizowany();

            var punktZawiasu =
                er * _k.PromienPozornegoOkreguOsiZawiasuMm +
                OsBebna * _k.PrzesuniecieOsiZawiasuZMm;

            // Kierunek ramienia porusza się po łuku w płaszczyźnie er-Z.
            var kierunekRamienia =
                (er * Math.Cos(alfa) + OsBebna * Math.Sin(alfa))
                .Znormalizowany();

            // KLUCZOWA POPRAWKA v5.1:
            //
            // Łożysko jest sztywno związane z ramieniem, więc jego oś musi
            // obracać się razem z całym zespołem wokół osi zawiasu.
            //
            // Dla alfa = 180°:
            //   ramię        = -er
            //   oś łożyska   = +Z  (równoległa do osi bębna)
            //
            // Dla alfa = 270°:
            //   ramię        = -Z
            //   oś łożyska   = -er (promieniowo w kierunku osi bębna)
            //
            // W każdym położeniu:
            //   oś_łożyska · ramię = 0
            //   oś_łożyska · oś_zawiasu = 0
            //
            // To jest dokładnie sztywny obrót wahadła wokół osZawiasu.
            var osLozyska =
                (er * Math.Sin(alfa) - OsBebna * Math.Cos(alfa))
                .Znormalizowany();

            var srodekLozyska =
                punktZawiasu +
                kierunekRamienia * _k.DlugoscRamieniaMm;

            return new PozycjaLozyska
            {
                KatProgramowyStopnie = katProgramowy,
                KatGeometrycznyStopnie = katGeometryczny,
                KatRamieniaStopnie = alfaStopnie,
                KierunekPromieniowy = er,
                KierunekOsiZawiasu = osZawiasu,
                KierunekOsiBebna = OsBebna,
                KierunekRamienia = kierunekRamienia,
                KierunekOsiLozyska = osLozyska,
                PunktOsiZawiasuMm = punktZawiasu,
                SrodekLozyskaMm = srodekLozyska
            };
        }

        public double ObliczKatRamienia(double katProgramowyStopnie)
        {
            double phi = NormalizujKatProgramowy(katProgramowyStopnie);

            double a0 = 0.0;
            double a1 = _k.WznosStopnie;
            double a2 = a1 + _k.PostojWysokiStopnie;
            double a3 = a2 + _k.PowrotStopnie;

            double start = _k.KatPoczatkowyRamieniaStopnie;
            double skok = _k.SkokRamieniaStopnie;
            double koniec = _k.KatKoncowyRamieniaStopnie;

            if (phi < a1)
            {
                double u = phi / _k.WznosStopnie;
                return start + skok * Postep(u);
            }

            if (phi < a2)
                return koniec;

            if (phi < a3)
            {
                double u = (phi - a2) / _k.PowrotStopnie;
                return koniec - skok * Postep(u);
            }

            return start;
        }

        public Wektor3 ObliczCentrumProfiluMm(
            double katProgramowyStopnie,
            double przesunieciePoSzerokosciMm)
        {
            PozycjaLozyska p = Oblicz(katProgramowyStopnie);

            return p.SrodekLozyskaMm +
                   p.KierunekOsiLozyska * przesunieciePoSzerokosciMm;
        }

        private double Postep(double u)
        {
            if (u < 0.0) u = 0.0;
            if (u > 1.0) u = 1.0;

            switch (_k.PrawoRuchu)
            {
                case PrawoRuchu.Cykloidalne:
                    return u - Math.Sin(2.0 * Math.PI * u) / (2.0 * Math.PI);

                case PrawoRuchu.Liniowe:
                    return u;

                default:
                    return 10.0 * Math.Pow(u, 3)
                         - 15.0 * Math.Pow(u, 4)
                         + 6.0 * Math.Pow(u, 5);
            }
        }

        public double MaksymalnaPochodnaPrawaRuchu()
        {
            switch (_k.PrawoRuchu)
            {
                case PrawoRuchu.Cykloidalne:
                    return 2.0;
                case PrawoRuchu.Liniowe:
                    return 1.0;
                default:
                    return 1.875;
            }
        }

        private static double NormalizujKatProgramowy(double kat)
        {
            double x = kat % 360.0;
            if (x < 0.0) x += 360.0;

            // Zachowanie dokładnej wartości 360 nie jest potrzebne,
            // bo loft zamknięty łączy ostatni profil z pierwszym.
            if (Math.Abs(x - 360.0) < 1e-10)
                x = 0.0;

            return x;
        }
    }
}
