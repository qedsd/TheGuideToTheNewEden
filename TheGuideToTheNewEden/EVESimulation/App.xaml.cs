using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace EVESimulation
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (sender, e) =>
            {
                MessageBox.Show($"发生未处理的异常：\n{e.Exception.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Any(p => string.Equals(p, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var root = e.Args.Length > 1 ? e.Args[1] : Path.Combine(Path.GetTempPath(), "EVECombatSimTest");
                    SelfTest.Generate(root);
                    File.WriteAllText(Path.Combine(root, "selftest.ok"), "ok");
                }
                catch (Exception ex)
                {
                    File.WriteAllText(
                        Path.Combine(Path.GetTempPath(), "EVECombatSimTest.error.txt"), ex.ToString());
                }

                Shutdown();
                return;
            }

            if (e.Args.Any(p => string.Equals(p, "--importtest", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var dataPath = e.Args.Length > 1 ? e.Args[1] : WpfCharacterImporter.DefaultDataPath();
                    var characters = WpfCharacterImporter.Import(
                        dataPath,
                        WpfCharacterImporter.DefaultLogsPath(),
                        out var summary);
                    var lines = characters.Select(p => $"{p.Name} | {p.Id} | {p.Source}").ToList();
                    File.WriteAllText(
                        Path.Combine(Path.GetTempPath(), "wpf-import-test.txt"),
                        summary + "\n\n" + string.Join("\n", lines));
                }
                catch (Exception ex)
                {
                    File.WriteAllText(
                        Path.Combine(Path.GetTempPath(), "wpf-import-test.error.txt"), ex.ToString());
                }

                Shutdown();
                return;
            }

            new MainWindow().Show();
        }
    }
}
