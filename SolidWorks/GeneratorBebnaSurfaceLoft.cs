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
                    nazwa))
                return false;

            uzytoFallbacku = true;
            return true;
        }

        private bool SprobujUtworzycSegmentoweLoftCut(
            IReadOnlyList<Feature> profile,
            IReadOnlyList<double> katyStopnie,
            string nazwa)
        {
            if (profile == null || katyStopnie == null ||
                profile.Count != katyStopnie.Count || profile.Count < 2)
                return false;

            int poczatek = 0;
            int numerSegmentu = 0;

            while (poczatek < profile.Count - 1)
            {
                int koniec = poczatek + 1;
                while (koniec + 1 < profile.Count &&
                       katyStopnie[koniec + 1] - katyStopnie[poczatek] <=
                       MaksymalnyZakresLoftuSegmentowegoStopnie + 1e-9)
                {
                    koniec++;
                }

                numerSegmentu++;
                List<Feature> segment = profile
                    .Skip(poczatek)
                    .Take(koniec - poczatek + 1)
                    .ToList();

                string nazwaSegmentu = nazwa + "_SEGMENT_" +
                    numerSegmentu.ToString("00");

                Console.WriteLine(
                    "    Segment " + numerSegmentu + ": " +
                    katyStopnie[poczatek].ToString("0.###", CultureInfo.InvariantCulture) +
                    "° -> " +
                    katyStopnie[koniec].ToString("0.###", CultureInfo.InvariantCulture) + "°");

                if (!SprobujUtworzycLoftCut(segment, false, nazwaSegmentu))
                    return false;

                // Wspólny profil sąsiednich segmentów eliminuje szczelinę
                // na granicy dwóch operacji.
                poczatek = koniec;
            }

            return true;
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
            string nazwa)
        {
            if (profileSzkicow == null || profileSzkicow.Count < 2)
                return false;

            Model.ClearSelection2(true);

            if (!ZaznaczProfileSzkicow(profileSzkicow))
            {
                Console.WriteLine("    Nie udało się zaznaczyć wszystkich profili szkiców.");
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

            Model.ClearSelection2(true);

            if (feature == null)
            {
                Console.WriteLine(
                    "    IFeatureManager.InsertCutBlend zwróciło null dla: " + nazwa);
                return false;
            }

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
                    Model.ClearSelection2(true);
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

                    Model.ClearSelection2(true);
                    return false;
                }

                pierwszy = false;
            }

            return true;
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
