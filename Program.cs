using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GeneratorBebnaKrzywkowego.Core;
using GeneratorBebnaKrzywkowego.SolidWorks;

namespace GeneratorBebnaKrzywkowego
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                string sciezkaYaml = args != null && args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : WybierzPlikYaml();

                if (string.IsNullOrWhiteSpace(sciezkaYaml))
                    return;

                Console.WriteLine(new string('=', 100));
                Console.WriteLine("Generator bębna krzywkowego Surface-Loft v5.1.1");
                Console.WriteLine("YAML: " + sciezkaYaml);

                KonfiguracjaGeneratora konfiguracja = ParserYaml.Wczytaj(sciezkaYaml);
                konfiguracja.Waliduj();

                Console.WriteLine();
                Console.WriteLine(konfiguracja.ZbudujPodsumowanie());

                var plan = PlanPowierzchniowy.Zbuduj(konfiguracja);

                Console.WriteLine();
                Console.WriteLine("PLAN POWIERZCHNIOWY:");
                Console.WriteLine("Profile kątowe              : " + plan.KatyProgramoweStopnie.Count);
                Console.WriteLine("Stacje szerokości łożyska   : " + plan.PrzesunieciaPoSzerokosciMm.Count);
                Console.WriteLine("Łączne przekroje loftów     : " +
                                  (plan.KatyProgramoweStopnie.Count * plan.PrzesunieciaPoSzerokosciMm.Count));
                Console.WriteLine("Silnik geometrii            : natywne Lofted Cut / powierzchnie NURBS");
                Console.WriteLine("IBody2.Operations2          : NIE UŻYWANE");
                Console.WriteLine("Per-pose cylinder Boolean   : NIE UŻYWANE");
                Console.WriteLine();

                DialogResult decyzja = MessageBox.Show(
                    konfiguracja.ZbudujPodsumowanie() +
                    Environment.NewLine + Environment.NewLine +
                    "Profile kątowe: " + plan.KatyProgramoweStopnie.Count +
                    Environment.NewLine +
                    "Stacje szerokości: " + plan.PrzesunieciaPoSzerokosciMm.Count +
                    Environment.NewLine + Environment.NewLine +
                    "Generator utworzy NOWĄ część SOLIDWORKS." +
                    Environment.NewLine +
                    "Kontynuować?",
                    "Generator bębna krzywkowego Surface-Loft v5.1.1",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (decyzja != DialogResult.Yes)
                    return;

                using (SesjaSolidWorks sesja = SesjaSolidWorks.PolaczIUtworzNowaCzesc())
                {
                    var generator = new GeneratorBebnaSurfaceLoft(sesja, konfiguracja, plan);
                    RaportGenerowania raport = generator.Generuj();

                    Console.WriteLine();
                    Console.WriteLine(new string('=', 100));
                    Console.WriteLine("GOTOWE");
                    Console.WriteLine(raport.DoTekstu());

                    MessageBox.Show(
                        raport.DoTekstu(),
                        "Generator Surface-Loft v5.1.1",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("BŁĄD:");
                Console.WriteLine(ex);

                MessageBox.Show(
                    ex.Message +
                    Environment.NewLine + Environment.NewLine +
                    "Pełna diagnostyka została wypisana w konsoli.",
                    "Generator Surface-Loft — błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string WybierzPlikYaml()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Wybierz konfigurację bębna krzywkowego";
                dialog.Filter = "YAML (*.yaml;*.yml)|*.yaml;*.yml|Wszystkie pliki (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                return dialog.ShowDialog() == DialogResult.OK
                    ? dialog.FileName
                    : null;
            }
        }
    }
}
