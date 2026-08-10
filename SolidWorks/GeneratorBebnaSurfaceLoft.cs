using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeneratorBebnaKrzywkowego.Core;
using SolidWorks.Interop.sldworks;

namespace GeneratorBebnaKrzywkowego.SolidWorks
{
    /// <summary>
    /// Rdzeń Surface-Loft.
    ///
    /// Brak:
    /// - IBody2.Operations2,
    /// - SWBODYCUT,
    /// - sekwencji "walec pozycji -> Boolean".
    ///
    /// Krzywka jest tworzona przez natywne IFeatureManager.InsertCutBlend
    /// na profilach kołowych zorientowanych w przestrzeni 3D.
    ///
    /// v5.1.1:
    /// SOLIDWORKS API wymaga, aby profile loftu były wybierane jako profile
    /// szkiców (mark=1). Poprzednia wersja wybierała obiekty SketchSegment,
    /// przez co InsertCutBlend zwracał null mimo poprawnej geometrii.
    /// Ta wersja tworzy każdy przekrój w osobnym szkicu 3D i wybiera
    /// Feature szkicu przez IModelDocExtension.SelectByID2(..., "SKETCH").
    /// </summary>
    public sealed class GeneratorBebnaSurfaceLoft
    {
        private const double MmNaM = 0.001;
        private const double MaksymalnyZakresLoftuSegmentowegoStopnie = 45.0;
        private const double KrokSzukaniaPrzeciecStopnie = 0.25;
        private const double TolerancjaPromieniowaMm = 1e-5;

        private readonly SesjaSolidWorks _sesja;
        private readonly KonfiguracjaGeneratora _k;
        private readonly PlanPowierzchniowy _plan;
        private readonly Kinematyka _kin;

        private ModelDoc2 Model => _sesja.Model;
        private Modeler Modeler => _sesja.Modeler;
        private PartDoc Czesc => _sesja.Czesc;

        public GeneratorBebnaSurfaceLoft(
            SesjaSolidWorks sesja,
            KonfiguracjaGeneratora konfiguracja,
            PlanPowierzchniowy plan)
        {
            _sesja = sesja ?? throw new ArgumentNullException(nameof(sesja));
            _k = konfiguracja ?? throw new ArgumentNullException(nameof(konfiguracja));
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _kin = new Kinematyka(_k);
        }

        public RaportGenerowania Generuj()
        {
            var raport = new RaportGenerowania
            {
                LiczbaProfiliKatowych = _plan.KatyProgramoweStopnie.Count,
                LiczbaStacjiSzerokosci = _plan.PrzesunieciaPoSzerokosciMm.Count
            };

            Console.WriteLine("1/3  Tworzenie pełnego bębna bazowego...");
            UtworzPelnyBebenBazowy();

            Console.WriteLine("2/3  Generowanie powierzchniowej bieżni loftowanej...");

            int numerStacji = 0;

            foreach (double tMm in _plan.PrzesunieciaPoSzerokosciMm)
            {
                numerStacji++;

                Console.WriteLine(
                    $"  Loft {numerStacji}/{_plan.PrzesunieciaPoSzerokosciMm.Count}, " +
                    $"t = {tMm.ToString("0.###", CultureInfo.InvariantCulture)} mm");

                bool uzytoFallbacku;

                bool ok = UtworzZamknieteWyciecieLoftowaneDlaStacji(
                    tMm,
                    "KRZYWKA_LOFT_" + numerStacji.ToString("00"),
                    out uzytoFallbacku);

                if (!ok)
                {
                    throw new InvalidOperationException(
                        "SOLIDWORKS nie utworzył zamkniętego Lofted Cut dla stacji szerokości " +
                        tMm.ToString("0.###", CultureInfo.InvariantCulture) + " mm." +
                        System.Environment.NewLine +
                        "Nie wykonano fallbacku opartego na Booleanach.");
                }

                raport.LiczbaLoftowKrzywki++;

                if (uzytoFallbacku)
                    raport.LiczbaFallbackowSzkicow++;
            }

            Console.WriteLine("3/3  Otwór centralny jako natywne otwarte Lofted Cut...");

            if (_k.SrednicaOtworuBebnaMm > 1e-9)
            {
                if (!UtworzOtworCentralnyLoftem())
                    throw new InvalidOperationException(
                        "Nie udało się utworzyć otworu centralnego jako Lofted Cut.");

                raport.OtworCentralnyWykonany = true;
            }

            Feature ostatnia = SafeLastFeature();
            TryRenameFeature(ostatnia, "CAM_DRUM_FINAL");

            try
            {
                Model.ClearSelection2(true);
                Model.ForceRebuild3(false);
                Model.GraphicsRedraw2();
            }
            catch
            {
                // Redraw nie wpływa na geometrię.
            }

            return raport;
        }

        private void UtworzPelnyBebenBazowy()
        {
            double r = _k.PromienBebnaMm * MmNaM;
            double l = _k.DlugoscBebnaMm * MmNaM;

            double[] daneWalca =
            {
                0.0, 0.0, -l / 2.0,
                0.0, 0.0, 1.0,
                r, l
            };

            Body2 body = Modeler.CreateBodyFromCyl(daneWalca) as Body2;

            if (body == null)
                throw new InvalidOperationException(
                    "IModeler.CreateBodyFromCyl nie utworzył bębna bazowego.");

            Feature feature = Czesc.CreateFeatureFromBody3(
                body,
                false,
                0) as Feature;

            if (feature == null)
                throw new InvalidOperationException(
                    "IPartDoc.CreateFeatureFromBody3 nie utworzyło operacji bębna.");

            TryRenameFeature(feature, "BEBEN_BAZOWY_PELNY");
        }

        private bool UtworzZamknieteWyciecieLoftowaneDlaStacji(
            double przesunieciePoSzerokosciMm,
            string nazwa,
            out bool uzytoFallbacku)
        {
            // v5.1.1 świadomie NIE używa wielu konturów z jednego szkicu 3D.
            //
            // Oficjalny InsertCutBlend oczekuje wybranych profili szkiców
            // z mark=1. Najbardziej jednoznaczny i stabilny model API to:
            // 1 zamknięty profil = 1 osobny szkic 3D = 1 Feature typu SKETCH.
            uzytoFallbacku = false;

            List<Feature> profile = UtworzProfileWOddzielnychSzkicach3D(
                przesunieciePoSzerokosciMm,
                nazwa);

            Console.WriteLine(
                "    Utworzono " + profile.Count +
                " osobnych szkiców 3D; wybór profili jako SKETCH, mark=1.");

            if (SprobujUtworzycLoftCut(profile, true, nazwa))
                return true;

            // Nie wszystkie wersje SOLIDWORKS potrafią utworzyć zamknięty
            // Lofted Cut, którego przekroje obchodzą pełne 360 stopni i
            // jednocześnie zmieniają orientację. InsertCutBlend zwraca wtedy
            // null mimo prawidłowej selekcji szkiców. Dzielimy więc dokładnie
            // tę samą obwiednię na zachodzące na siebie, otwarte odcinki.
            // Nadal są to wyłącznie natywne Lofted Cut - bez brył narzędziowych
            // i bez operacji Boolean.
            Console.WriteLine(
                "    Zamknięty loft nie został przyjęty; próba segmentowych " +
                "Lofted Cut (bez Booleanów).");

            Feature profilDomykajacy = UtworzProfileWOddzielnychSzkicach3D(
                przesunieciePoSzerokosciMm,
                nazwa + "_DOMKNIECIE",
                new[] { 360.0 }).Single();

            var profileZamknietejPetli = profile.Concat(
                new[] { profilDomykajacy }).ToList();
            var katyZamknietejPetli = _plan.KatyProgramoweStopnie.Concat(
                new[] { 360.0 }).ToList();

            if (!SprobujUtworzycSegmentoweLoftCut(
                    profileZamknietejPetli,
                    katyZamknietejPetli,
                    przesunieciePoSzerokosciMm,
                    nazwa))
                return false;

            uzytoFallbacku = true;
            return true;
        }

        private bool SprobujUtworzycSegmentoweLoftCut(
            IReadOnlyList<Feature> profile,
            IReadOnlyList<double> katyStopnie,
            double przesunieciePoSzerokosciMm,
            string nazwa)
        {
            if (profile == null || katyStopnie == null ||
                profile.Count != katyStopnie.Count || profile.Count < 2)
                return false;

            List<ProfilKatowy> profileKatowe = profile
                .Select((profil, indeks) => new ProfilKatowy
                {
                    KatStopnie = katyStopnie[indeks],
                    Profil = profil
                })
                .ToList();

            List<double> granice = ZnajdzGranicePrzecieciaObwiedni(
                przesunieciePoSzerokosciMm);
            if (granice.Count > 0)
            {
                Console.WriteLine(
                    "    Punkty wejścia/wyjścia obwiedni przez walec: " +
                    string.Join("°, ", granice.Select(Formatuj)) + "°.");
                double odsuniecie = ObliczOdsuniecieProfiliGranicznych(katyStopnie);
                var dodatkoweKaty = new List<double>();
                foreach (double granica in granice)
                {
                    DodajKatJesliNowy(dodatkoweKaty, granica - odsuniecie, profileKatowe);
                    DodajKatJesliNowy(dodatkoweKaty, granica, profileKatowe);
                    DodajKatJesliNowy(dodatkoweKaty, granica + odsuniecie, profileKatowe);
                }

                if (dodatkoweKaty.Count > 0)
                {
                    List<Feature> dodatkoweProfile = UtworzProfileWOddzielnychSzkicach3D(
                        przesunieciePoSzerokosciMm,
                        nazwa + "_GRANICA",
                        dodatkoweKaty);
                    profileKatowe.AddRange(dodatkoweKaty.Select((kat, indeks) =>
                        new ProfilKatowy { KatStopnie = kat, Profil = dodatkoweProfile[indeks] }));
                    profileKatowe = profileKatowe.OrderBy(x => x.KatStopnie).ToList();
                }
            }

            List<StanProfilu> stany = profileKatowe
                .Select(x => OkreslStanProfilu(x.KatStopnie, przesunieciePoSzerokosciMm))
                .ToList();
            List<ZakresIndeksow> zakresy = ZbudujZakresyPrzecinajace(stany);

            Console.WriteLine(
                "    Klasyfikacja obwiedni: wewnątrz=" +
                stany.Count(x => x == StanProfilu.Wewnatrz) + ", przecina=" +
                stany.Count(x => x == StanProfilu.Przecina) + ", na zewnątrz=" +
                stany.Count(x => x == StanProfilu.NaZewnatrz) + ".");

            if (zakresy.Count == 0)
            {
                if (stany.All(x => x == StanProfilu.NaZewnatrz))
                {
                    Console.WriteLine("    Obwiednia nie przecina bębna; pominięto Lofted Cut.");
                    return true;
                }

                Console.WriteLine(
                    "    Obwiednia jest całkowicie wewnętrzna. Zamknięty Lofted Cut " +
                    "został wcześniej odrzucony, więc nie tworzę niezweryfikowanej " +
                    "wewnętrznej wnęki segmentowej.");
                return false;
            }

            int numerSegmentu = 0;
            foreach (ZakresIndeksow zakres in zakresy)
            {
                int poczatek = zakres.Poczatek;
                while (poczatek < zakres.Koniec)
                {
                    int koniec = poczatek + 1;
                    while (koniec + 1 <= zakres.Koniec &&
                       profileKatowe[koniec + 1].KatStopnie - profileKatowe[poczatek].KatStopnie <=
                       MaksymalnyZakresLoftuSegmentowegoStopnie + 1e-9)
                        koniec++;

                    numerSegmentu++;
                    List<Feature> segment = profileKatowe
                        .Skip(poczatek)
                        .Take(koniec - poczatek + 1)
                        .Select(x => x.Profil)
                        .ToList();

                    string nazwaSegmentu = nazwa + "_SEGMENT_" +
                        numerSegmentu.ToString("00");

                    Console.WriteLine(
                        "    Segment " + numerSegmentu + ": " +
                        profileKatowe[poczatek].KatStopnie.ToString("0.###", CultureInfo.InvariantCulture) +
                        "° -> " +
                        profileKatowe[koniec].KatStopnie.ToString("0.###", CultureInfo.InvariantCulture) + "°");

                    DiagnostykaSegmentu diagnostyka = WypiszDiagnostykeSegmentu(
                        profileKatowe.Select(x => x.KatStopnie).ToList(),
                        poczatek,
                        koniec,
                        przesunieciePoSzerokosciMm);

                    if (!SprobujUtworzycLoftCut(
                            segment,
                            false,
                            nazwaSegmentu,
                            diagnostyka))
                        return false;

                    // Wspólny profil sąsiednich segmentów eliminuje szczelinę
                    // na granicy dwóch operacji.
                    poczatek = koniec;
                }
            }

            return true;
        }

        private List<double> ZnajdzGranicePrzecieciaObwiedni(double przesuniecieMm)
        {
            var wynik = new List<double>();
            double poprzedniKat = 0.0;
            DiagnostykaProfilu poprzedni = ObliczDiagnostykeProfilu(0.0, przesuniecieMm);

            for (double kat = KrokSzukaniaPrzeciecStopnie;
                 kat <= 360.0 + 1e-9;
                 kat += KrokSzukaniaPrzeciecStopnie)
            {
                double biezacyKat = Math.Min(kat, 360.0);
                DiagnostykaProfilu biezacy = ObliczDiagnostykeProfilu(biezacyKat, przesuniecieMm);
                DodajGraniceDlaFunkcji(wynik, poprzedniKat, biezacyKat,
                    poprzedni.MaksymalnaOdlegloscMm - _k.PromienBebnaMm,
                    biezacy.MaksymalnaOdlegloscMm - _k.PromienBebnaMm,
                    przesuniecieMm, true);
                DodajGraniceDlaFunkcji(wynik, poprzedniKat, biezacyKat,
                    poprzedni.MinimalnaOdlegloscMm - _k.PromienBebnaMm,
                    biezacy.MinimalnaOdlegloscMm - _k.PromienBebnaMm,
                    przesuniecieMm, false);
                poprzedniKat = biezacyKat;
                poprzedni = biezacy;
            }

            return wynik.Where(x => x > 1e-7 && x < 360.0 - 1e-7)
                .Distinct(new KatComparer(1e-5)).OrderBy(x => x).ToList();
        }

        private void DodajGraniceDlaFunkcji(List<double> wynik, double lewy, double prawy,
            double wartoscLewa, double wartoscPrawa, double przesuniecieMm, bool maksimum)
        {
            if (wartoscLewa * wartoscPrawa > 0.0)
                return;

            for (int i = 0; i < 32; i++)
            {
                double srodek = (lewy + prawy) / 2.0;
                DiagnostykaProfilu d = ObliczDiagnostykeProfilu(srodek, przesuniecieMm);
                double wartosc = (maksimum ? d.MaksymalnaOdlegloscMm : d.MinimalnaOdlegloscMm)
                    - _k.PromienBebnaMm;
                if (wartoscLewa * wartosc <= 0.0)
                {
                    prawy = srodek;
                    wartoscPrawa = wartosc;
                }
                else
                {
                    lewy = srodek;
                    wartoscLewa = wartosc;
                }
            }
            wynik.Add((lewy + prawy) / 2.0);
        }

        private StanProfilu OkreslStanProfilu(double kat, double przesuniecieMm)
        {
            DiagnostykaProfilu d = ObliczDiagnostykeProfilu(kat, przesuniecieMm);
            if (d.MaksymalnaOdlegloscMm < _k.PromienBebnaMm - TolerancjaPromieniowaMm)
                return StanProfilu.Wewnatrz;
            if (d.MinimalnaOdlegloscMm > _k.PromienBebnaMm + TolerancjaPromieniowaMm)
                return StanProfilu.NaZewnatrz;
            return StanProfilu.Przecina;
        }

        private static List<ZakresIndeksow> ZbudujZakresyPrzecinajace(IReadOnlyList<StanProfilu> stany)
        {
            var wynik = new List<ZakresIndeksow>();
            int i = 0;
            while (i < stany.Count)
            {
                if (stany[i] != StanProfilu.Przecina) { i++; continue; }
                int poczatekPrzeciecia = i;
                while (i + 1 < stany.Count && stany[i + 1] == StanProfilu.Przecina) i++;
                int koniecPrzeciecia = i;
                wynik.Add(new ZakresIndeksow
                {
                    Poczatek = Math.Max(0, poczatekPrzeciecia - 1),
                    Koniec = Math.Min(stany.Count - 1, koniecPrzeciecia + 1)
                });
                i++;
            }
            return wynik;
        }

        private static double ObliczOdsuniecieProfiliGranicznych(IReadOnlyList<double> katy)
        {
            double minKrok = katy.Zip(katy.Skip(1), (a, b) => b - a)
                .Where(x => x > 1e-9).DefaultIfEmpty(1.0).Min();
            return Math.Min(0.1, minKrok / 4.0);
        }

        private static void DodajKatJesliNowy(List<double> wynik, double kat,
            IReadOnlyList<ProfilKatowy> istniejace)
        {
            if (kat <= 0.0 || kat >= 360.0 ||
                istniejace.Any(x => Math.Abs(x.KatStopnie - kat) < 1e-7) ||
                wynik.Any(x => Math.Abs(x - kat) < 1e-7))
                return;
            wynik.Add(kat);
        }

        /*
         * Stary eksperymentalny wariant z wieloma konturami w jednym szkicu 3D
         * pozostaje całkowicie wyłączony. InsertCutBlend nie jest wywoływany
         * na SketchSegmentach.
         */
        private List<SketchSegment> UtworzProfileWJednymSzkicu3D(
            double przesunieciePoSzerokosciMm)
        {
            SketchManager skMgr = Model.SketchManager;
            var segmenty = new List<SketchSegment>();

            Model.ClearSelection2(true);
            skMgr.Insert3DSketch(true);

            Sketch szkic = skMgr.ActiveSketch;
            if (szkic == null)
                throw new InvalidOperationException(
                    "Nie udało się aktywować szkicu 3D.");

            try
            {
                skMgr.AddToDB = true;
                skMgr.DisplayWhenAdded = false;

                foreach (double phi in _plan.KatyProgramoweStopnie)
                {
                    SketchSegment segment = UtworzJedenProfil(
                        skMgr,
                        szkic,
                        phi,
                        przesunieciePoSzerokosciMm);

                    if (segment == null)
                    {
                        throw new InvalidOperationException(
                            "ISketchManager.CreateCircle zwróciło null dla kąta " +
                            phi.ToString("0.###", CultureInfo.InvariantCulture) + "°.");
                    }

                    segmenty.Add(segment);
                }
            }
            finally
            {
                skMgr.AddToDB = false;
                skMgr.DisplayWhenAdded = true;
                skMgr.Insert3DSketch(true);
            }

            return segmenty;
        }

        private List<Feature> UtworzProfileWOddzielnychSzkicach3D(
            double przesunieciePoSzerokosciMm,
            string prefiksNazwy,
            IEnumerable<double> katyStopnie = null)
        {
            SketchManager skMgr = Model.SketchManager;
            var profile = new List<Feature>();

            int indeks = 0;

            foreach (double phi in katyStopnie ?? _plan.KatyProgramoweStopnie)
            {
                indeks++;

                Model.ClearSelection2(true);
                skMgr.Insert3DSketch(true);

                Sketch szkic = skMgr.ActiveSketch;
                if (szkic == null)
                    throw new InvalidOperationException(
                        "Nie udało się aktywować osobnego szkicu 3D.");

                SketchSegment segment;

                try
                {
                    skMgr.AddToDB = true;
                    skMgr.DisplayWhenAdded = false;

                    segment = UtworzJedenProfil(
                        skMgr,
                        szkic,
                        phi,
                        przesunieciePoSzerokosciMm);
                }
                finally
                {
                    skMgr.AddToDB = false;
                    skMgr.DisplayWhenAdded = true;
                    skMgr.Insert3DSketch(true);
                }

                if (segment == null)
                    throw new InvalidOperationException(
                        "Nie udało się utworzyć profilu przy " +
                        phi.ToString("0.###", CultureInfo.InvariantCulture) + "°.");

                // Po zamknięciu szkicu 3D jego Feature jest ostatnią
                // operacją dodaną do drzewa FeatureManager.
                Feature featureSzkicu = SafeLastFeature();

                if (featureSzkicu == null)
                    throw new InvalidOperationException(
                        "Nie można pobrać Feature szkicu profilu przy " +
                        phi.ToString("0.###", CultureInfo.InvariantCulture) + "°.");

                string nazwaProfilu =
                    prefiksNazwy + "_PROFIL_" + indeks.ToString("000");

                TryRenameFeature(featureSzkicu, nazwaProfilu);

                profile.Add(featureSzkicu);
            }

            return profile;
        }

        private SketchSegment UtworzJedenProfil(
            SketchManager skMgr,
            Sketch szkic,
            double katProgramowyStopnie,
            double przesunieciePoSzerokosciMm)
        {
            PozycjaLozyska pozycja = _kin.Oblicz(katProgramowyStopnie);

            // Szerokość rolki odkładamy wzdłuż AKTUALNEJ osi łożyska.
            // Ta oś obraca się razem z wahadłem.
            Wektor3 centrumMm =
                pozycja.SrodekLozyskaMm +
                pozycja.KierunekOsiLozyska * przesunieciePoSzerokosciMm;

            Wektor3 ramie = pozycja.KierunekRamienia.Znormalizowany();
            Wektor3 osZawiasu = pozycja.KierunekOsiZawiasu.Znormalizowany();
            Wektor3 osLozyska = pozycja.KierunekOsiLozyska.Znormalizowany();

            // Lokalny układ profilu łożyska po poprawce v5.1:
            //
            // X = kierunek ramienia
            // Y = -oś zawiasu
            // N = oś łożyska
            //
            // Ponieważ:
            //   ramie x (-osZawiasu) = osLozyska
            //
            // płaszczyzna każdego koła obraca się SZTYWNO z ramieniem
            // wokół zawiasu. Przy starcie (180°) normalna profilu jest
            // równoległa do osi bębna, a przy 270° staje się radialna.
            Wektor3 x = ramie;
            Wektor3 y = -osZawiasu;

            Wektor3 c = centrumMm * MmNaM;
            double r = _k.PromienProfiluNarzedziaMm * MmNaM;
            Wektor3 p = c + x * r;

            bool ustawiono = szkic.SetWorkingPlaneOrientation(
                c.X, c.Y, c.Z,
                x.X, x.Y, x.Z,
                y.X, y.Y, y.Z,
                osLozyska.X, osLozyska.Y, osLozyska.Z);

            if (!ustawiono)
            {
                throw new InvalidOperationException(
                    "SOLIDWORKS odrzucił płaszczyznę roboczą profilu przy kącie " +
                    katProgramowyStopnie.ToString("0.###", CultureInfo.InvariantCulture) + "°.");
            }

            return skMgr.CreateCircle(
                c.X, c.Y, c.Z,
                p.X, p.Y, p.Z);
        }

        private bool SprobujUtworzycLoftCut(
            IReadOnlyList<Feature> profileSzkicow,
            bool zamkniety,
            string nazwa,
            DiagnostykaSegmentu diagnostykaSegmentu = null)
        {
            if (profileSzkicow == null || profileSzkicow.Count < 2)
                return false;

            Model.ClearSelection2(true);

            bool zaznaczonoWszystkie = ZaznaczProfileSzkicow(profileSzkicow);
            int liczbaZaznaczen = WypiszDiagnostykeZaznaczenia(profileSzkicow.Count);

            if (!zaznaczonoWszystkie || liczbaZaznaczen != profileSzkicow.Count)
            {
                Console.WriteLine(
                    "    BŁĄD SELEKCJI: oczekiwano " + profileSzkicow.Count +
                    " profili z markerem 1, rzeczywiście zaznaczono " +
                    liczbaZaznaczen + ".");
                Model.ClearSelection2(true);
                return false;
            }

            Console.WriteLine(
                "    Profile zaznaczone poprawnie: " +
                profileSzkicow.Count + " (typ SKETCH, mark=1).");

            FeatureManager fm = Model.FeatureManager;

            // Aktualne, oficjalne API lofted cut.
            Feature feature = fm.InsertCutBlend(
                zamkniety,
                true,
                false,
                1.0,
                0,
                0,
                false,
                0.0,
                0.0,
                0,
                false,
                true);

            if (feature == null)
            {
                Console.WriteLine(
                    "    IFeatureManager.InsertCutBlend zwróciło null dla: " + nazwa);

                // InsertCutBlend potrafi zwrócić null bez wyczyszczenia listy
                // wyboru. Zachowujemy ją aż do zebrania pełnej diagnostyki.
                WypiszDiagnostykeZaznaczenia(profileSzkicow.Count);
                WypiszPrzyczyneOdrzucenia(diagnostykaSegmentu);
                Model.ClearSelection2(true);
                return false;
            }

            Model.ClearSelection2(true);

            Console.WriteLine(
                "    InsertCutBlend utworzył operację: " + feature.Name);

            TryRenameFeature(feature, nazwa);
            return true;
        }

        private bool ZaznaczProfileSzkicow(
            IReadOnlyList<Feature> profileSzkicow)
        {
            bool pierwszy = true;
            ModelDocExtension ext = Model.Extension;

            foreach (Feature profil in profileSzkicow)
            {
                if (profil == null)
                {
                    return false;
                }

                string nazwa = profil.Name;

                // Zgodnie z dokumentacją loftu:
                // - obiekt wybieramy jako "SKETCH"
                // - profile mają selection mark = 1
                // - kolejność wyboru jest kolejnością sekcji loftu.
                bool ok = ext.SelectByID2(
                    nazwa,
                    "SKETCH",
                    0.0, 0.0, 0.0,
                    !pierwszy,
                    1,
                    null,
                    0);

                if (!ok)
                {
                    Console.WriteLine(
                        "    SelectByID2 nie zaznaczył profilu: " + nazwa);

                    return false;
                }

                pierwszy = false;
            }

            return true;
        }

        private int WypiszDiagnostykeZaznaczenia(int oczekiwanaLiczba)
        {
            SelectionMgr selectionManager = Model.SelectionManager as SelectionMgr;
            if (selectionManager == null)
            {
                Console.WriteLine("    BŁĄD SELEKCJI: SelectionManager jest niedostępny.");
                return 0;
            }

            int liczba = selectionManager.GetSelectedObjectCount2(1);
            Console.WriteLine(
                "    SelectionManager: marker=1, rzeczywista liczba zaznaczeń=" +
                liczba + ", oczekiwano=" + oczekiwanaLiczba + ".");

            for (int i = 1; i <= liczba; i++)
            {
                int typApi = selectionManager.GetSelectedObjectType3(i, 1);
                object zaznaczony = selectionManager.GetSelectedObject6(i, 1);
                string typRuntime = zaznaczony == null
                    ? "null"
                    : zaznaczony.GetType().FullName;

                Console.WriteLine(
                    "      Zaznaczenie " + i + ": typ API=" + typApi +
                    ", typ obiektu=" + typRuntime + ".");
            }

            return liczba;
        }

        private DiagnostykaSegmentu WypiszDiagnostykeSegmentu(
            IReadOnlyList<double> katyStopnie,
            int poczatek,
            int koniec,
            double przesunieciePoSzerokosciMm)
        {
            var diagnostyka = new DiagnostykaSegmentu
            {
                MinimalnaOdlegloscMm = double.PositiveInfinity,
                MaksymalnaOdlegloscMm = double.NegativeInfinity
            };

            Console.WriteLine(
                "      Kąt pierwszego profilu=" + Formatuj(katyStopnie[poczatek]) +
                "°, ostatniego=" + Formatuj(katyStopnie[koniec]) + "°.");

            for (int i = poczatek; i <= koniec; i++)
            {
                DiagnostykaProfilu profil = ObliczDiagnostykeProfilu(
                    katyStopnie[i],
                    przesunieciePoSzerokosciMm);

                diagnostyka.MinimalnaOdlegloscMm = Math.Min(
                    diagnostyka.MinimalnaOdlegloscMm,
                    profil.MinimalnaOdlegloscMm);
                diagnostyka.MaksymalnaOdlegloscMm = Math.Max(
                    diagnostyka.MaksymalnaOdlegloscMm,
                    profil.MaksymalnaOdlegloscMm);

                if (i == poczatek || i == koniec)
                {
                    Console.WriteLine(
                        "      Profil " + (i == poczatek ? "pierwszy" : "ostatni") +
                        ": środek=" + Formatuj(profil.SrodekMm) +
                        " mm, normalna=" + Formatuj(profil.Normalna) +
                        ", odległość od osi min~" +
                        Formatuj(profil.MinimalnaOdlegloscMm) + " mm, max~" +
                        Formatuj(profil.MaksymalnaOdlegloscMm) + " mm.");
                }
            }

            double promien = _k.PromienBebnaMm;
            diagnostyka.MozePrzeciacWalec =
                diagnostyka.MinimalnaOdlegloscMm <= promien + 1e-6 &&
                diagnostyka.MaksymalnaOdlegloscMm >= promien - 1e-6;

            Console.WriteLine(
                "      Obwiednia segmentu: odległość od osi min~" +
                Formatuj(diagnostyka.MinimalnaOdlegloscMm) + " mm, max~" +
                Formatuj(diagnostyka.MaksymalnaOdlegloscMm) +
                " mm; może przeciąć walec R=" + Formatuj(promien) + " mm: " +
                (diagnostyka.MozePrzeciacWalec ? "TAK" : "NIE") + ".");

            return diagnostyka;
        }

        private DiagnostykaProfilu ObliczDiagnostykeProfilu(
            double katStopnie,
            double przesunieciePoSzerokosciMm)
        {
            PozycjaLozyska pozycja = _kin.Oblicz(katStopnie);
            Wektor3 srodek = pozycja.SrodekLozyskaMm +
                pozycja.KierunekOsiLozyska * przesunieciePoSzerokosciMm;
            Wektor3 x = pozycja.KierunekRamienia.Znormalizowany();
            Wektor3 y = -pozycja.KierunekOsiZawiasu.Znormalizowany();
            double r = _k.PromienProfiluNarzedziaMm;
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;

            // Próbkowanie obwodu daje czytelną, niezależną od COM diagnostykę
            // szacunkową także dla profili nachylonych względem osi bębna.
            const int liczbaProbek = 360;
            for (int i = 0; i < liczbaProbek; i++)
            {
                double alfa = 2.0 * Math.PI * i / liczbaProbek;
                Wektor3 punkt = srodek +
                    x * (r * Math.Cos(alfa)) + y * (r * Math.Sin(alfa));
                double odleglosc = Math.Sqrt(punkt.X * punkt.X + punkt.Y * punkt.Y);
                min = Math.Min(min, odleglosc);
                max = Math.Max(max, odleglosc);
            }

            return new DiagnostykaProfilu
            {
                SrodekMm = srodek,
                Normalna = pozycja.KierunekOsiLozyska.Znormalizowany(),
                MinimalnaOdlegloscMm = min,
                MaksymalnaOdlegloscMm = max
            };
        }

        private void WypiszPrzyczyneOdrzucenia(DiagnostykaSegmentu diagnostyka)
        {
            if (diagnostyka == null)
            {
                Console.WriteLine("    ODRZUCENIE OPERACJI: loft nie został przyjęty.");
            }
            else if (diagnostyka.MaksymalnaOdlegloscMm < _k.PromienBebnaMm)
            {
                Console.WriteLine("    SEGMENT CAŁKOWICIE WEWNĘTRZNY względem walca bazowego.");
            }
            else if (diagnostyka.MinimalnaOdlegloscMm > _k.PromienBebnaMm)
            {
                Console.WriteLine("    SEGMENT CAŁKOWICIE ZEWNĘTRZNY względem walca bazowego.");
            }
            else
            {
                Console.WriteLine(
                    "    ODRZUCENIE OPERACJI MIMO PRZECIĘCIA BRYŁY: " +
                    "segment obejmuje promień walca bazowego.");
            }
        }

        private static string Formatuj(double wartosc) =>
            wartosc.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Formatuj(Wektor3 wektor) =>
            "(" + Formatuj(wektor.X) + ", " + Formatuj(wektor.Y) + ", " +
            Formatuj(wektor.Z) + ")";

        private sealed class DiagnostykaProfilu
        {
            public Wektor3 SrodekMm { get; set; }
            public Wektor3 Normalna { get; set; }
            public double MinimalnaOdlegloscMm { get; set; }
            public double MaksymalnaOdlegloscMm { get; set; }
        }

        private sealed class DiagnostykaSegmentu
        {
            public double MinimalnaOdlegloscMm { get; set; }
            public double MaksymalnaOdlegloscMm { get; set; }
            public bool MozePrzeciacWalec { get; set; }
        }

        private enum StanProfilu
        {
            Wewnatrz,
            Przecina,
            NaZewnatrz
        }

        private sealed class ProfilKatowy
        {
            public double KatStopnie { get; set; }
            public Feature Profil { get; set; }
        }

        private sealed class ZakresIndeksow
        {
            public int Poczatek { get; set; }
            public int Koniec { get; set; }
        }

        private sealed class KatComparer : IEqualityComparer<double>
        {
            private readonly double _tolerancja;

            public KatComparer(double tolerancja)
            {
                _tolerancja = tolerancja;
            }

            public bool Equals(double x, double y) => Math.Abs(x - y) <= _tolerancja;
            public int GetHashCode(double obj) => 0;
        }

        private bool UtworzOtworCentralnyLoftem()
        {
            double rMm = _k.SrednicaOtworuBebnaMm / 2.0;
            double zapasMm = Math.Max(2.0, _k.DlugoscBebnaMm * 0.02);

            double z1Mm = -_k.DlugoscBebnaMm / 2.0 - zapasMm;
            double z2Mm = +_k.DlugoscBebnaMm / 2.0 + zapasMm;

            var profile = new List<Feature>
            {
                UtworzOsobnyProfilOtworu("OTWOR_PROFIL_DOL", z1Mm, rMm),
                UtworzOsobnyProfilOtworu("OTWOR_PROFIL_GORA", z2Mm, rMm)
            };

            return SprobujUtworzycLoftCut(
                profile,
                false,
                "OTWOR_CENTRALNY");
        }

        private Feature UtworzOsobnyProfilOtworu(
            string nazwa,
            double zMm,
            double rMm)
        {
            SketchManager skMgr = Model.SketchManager;

            Model.ClearSelection2(true);
            skMgr.Insert3DSketch(true);

            Sketch szkic = skMgr.ActiveSketch;
            if (szkic == null)
                throw new InvalidOperationException(
                    "Nie udało się otworzyć szkicu 3D profilu otworu.");

            SketchSegment segment;

            try
            {
                skMgr.AddToDB = true;
                skMgr.DisplayWhenAdded = false;

                segment = UtworzProfilOtworu(skMgr, szkic, zMm, rMm);
            }
            finally
            {
                skMgr.AddToDB = false;
                skMgr.DisplayWhenAdded = true;
                skMgr.Insert3DSketch(true);
            }

            if (segment == null)
                throw new InvalidOperationException(
                    "Nie udało się utworzyć koła profilu otworu.");

            Feature feature = SafeLastFeature();
            if (feature == null)
                throw new InvalidOperationException(
                    "Nie można pobrać Feature szkicu otworu.");

            TryRenameFeature(feature, nazwa);
            return feature;
        }

        private SketchSegment UtworzProfilOtworu(
            SketchManager skMgr,
            Sketch szkic,
            double zMm,
            double rMm)
        {
            double z = zMm * MmNaM;
            double r = rMm * MmNaM;

            bool ok = szkic.SetWorkingPlaneOrientation(
                0.0, 0.0, z,
                1.0, 0.0, 0.0,
                0.0, 1.0, 0.0,
                0.0, 0.0, 1.0);

            if (!ok)
                throw new InvalidOperationException(
                    "Nie udało się ustawić płaszczyzny profilu otworu.");

            return skMgr.CreateCircle(
                0.0, 0.0, z,
                r, 0.0, z);
        }

        private Feature SafeLastFeature()
        {
            try
            {
                object obj = Model.Extension.GetLastFeatureAdded();
                return obj as Feature;
            }
            catch
            {
                try
                {
                    return Model.FeatureByPositionReverse(0) as Feature;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static void TryRenameFeature(Feature feature, string nazwa)
        {
            if (feature == null || string.IsNullOrWhiteSpace(nazwa))
                return;

            try
            {
                feature.Name = nazwa;
            }
            catch
            {
                // Nazwa nie jest krytyczna dla geometrii.
            }
        }
    }
}
