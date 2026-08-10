using GeneratorBebnaKrzywkowego.Core;
using Xunit;

namespace GeneratorBebnaKrzywkowego.Tests
{
    public sealed class KinematykaTests
    {
        private const double Tolerancja = 1e-9;

        [Fact]
        public void FazyMajaWlasciwyKierunekIWysokoscNaKoncach()
        {
            var kinematyka = new Kinematyka(UtworzKonfiguracje());

            PozycjaLozyska poczatekWznosu = kinematyka.Oblicz(0.0);
            PozycjaLozyska koniecWznosu = kinematyka.Oblicz(120.0);
            PozycjaLozyska koniecPostojuWysokiego = kinematyka.Oblicz(180.0);
            PozycjaLozyska koniecPowrotu = kinematyka.Oblicz(300.0);

            Assert.Equal(270.0, poczatekWznosu.KatRamieniaStopnie, Tolerancja);
            Assert.Equal(180.0, koniecWznosu.KatRamieniaStopnie, Tolerancja);
            Assert.True(koniecWznosu.SrodekLozyskaMm.Z > poczatekWznosu.SrodekLozyskaMm.Z);

            Assert.Equal(koniecWznosu.SrodekLozyskaMm.Z,
                koniecPostojuWysokiego.SrodekLozyskaMm.Z, Tolerancja);
            Assert.True(koniecPowrotu.SrodekLozyskaMm.Z <
                koniecPostojuWysokiego.SrodekLozyskaMm.Z);
            Assert.Equal(poczatekWznosu.SrodekLozyskaMm.Z,
                koniecPowrotu.SrodekLozyskaMm.Z, Tolerancja);
        }

        [Theory]
        [InlineData(120.0)]
        [InlineData(180.0)]
        [InlineData(300.0)]
        public void PozycjaJestCiaglaNaGranicachFaz(double granica)
        {
            var kinematyka = new Kinematyka(UtworzKonfiguracje());
            const double epsilon = 1e-7;

            Wektor3 przed = kinematyka.Oblicz(granica - epsilon).SrodekLozyskaMm;
            Wektor3 naGranicy = kinematyka.Oblicz(granica).SrodekLozyskaMm;
            Wektor3 po = kinematyka.Oblicz(granica + epsilon).SrodekLozyskaMm;

            AssertWektoryBliskie(przed, naGranicy, 1e-6);
            AssertWektoryBliskie(naGranicy, po, 1e-6);
        }

        [Fact]
        public void PozycjaJestCiaglaPrzyPrzejsciu360Do0Stopni()
        {
            var kinematyka = new Kinematyka(UtworzKonfiguracje());

            AssertWektoryBliskie(
                kinematyka.Oblicz(360.0).SrodekLozyskaMm,
                kinematyka.Oblicz(0.0).SrodekLozyskaMm,
                Tolerancja);
            AssertWektoryBliskie(
                kinematyka.Oblicz(360.0 - 1e-7).SrodekLozyskaMm,
                kinematyka.Oblicz(1e-7).SrodekLozyskaMm,
                1e-6);
        }

        private static KonfiguracjaGeneratora UtworzKonfiguracje() =>
            new KonfiguracjaGeneratora
            {
                SrednicaZewnetrznaBebnaMm = 156.0,
                DlugoscBebnaMm = 70.0,
                SrednicaZewnetrznaLozyskaMm = 30.0,
                DlugoscRamieniaMm = 46.4,
                OsZawiasuOdGornejKrawedziMm = 19.1,
                KatRamieniaZamknietegoStopnie = 270.0,
                KatRamieniaOtwartegoStopnie = 180.0,
                WznosStopnie = 120.0,
                PostojWysokiStopnie = 60.0,
                PowrotStopnie = 120.0,
                PrawoRuchu = PrawoRuchu.Wielomian345
            };

        private static void AssertWektoryBliskie(Wektor3 oczekiwany, Wektor3 rzeczywisty,
            double tolerancja)
        {
            Assert.InRange(System.Math.Abs(oczekiwany.X - rzeczywisty.X), 0.0, tolerancja);
            Assert.InRange(System.Math.Abs(oczekiwany.Y - rzeczywisty.Y), 0.0, tolerancja);
            Assert.InRange(System.Math.Abs(oczekiwany.Z - rzeczywisty.Z), 0.0, tolerancja);
        }
    }
}
