using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("t2med-fon")]
[assembly: AssemblyDescription("Wählt tel:-Links über das Yealink-Telefon am Arbeitsplatz")]
[assembly: AssemblyVersion(T2medFon.Program.Version + ".0")]

namespace T2medFon
{
    internal sealed class DialException : Exception
    {
        public DialException(string message) : base(message) { }
    }

    internal static class Program
    {
        public const string Version = "2.0.0";

        [STAThread]
        private static int Main(string[] args)
        {
            bool dialog = true;
            string configPath = Config.DefaultPath();
            string link = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--no-dialog") dialog = false;
                else if (args[i] == "--config" && i + 1 < args.Length) configPath = args[++i];
                else if (args[i] == "--version") return Report("t2med-fon " + Version, false, dialog);
                else if (link == null) link = args[i];
                else return Report("Zu viele Argumente.", true, dialog);
            }
            if (link == null) return Report("t2med-fon " + Version + " wählt tel:-Links über das Telefon am Arbeitsplatz.\n\nAufruf: t2med-fon.exe \"tel:0761123456\"", true, dialog);
            try
            {
                Config config = Config.Load(configPath);
                new Yealink(config).Dial(Number.FromLink(link, config.CountryCode, config.AreaCode));
                return 0;
            }
            catch (DialException e) { return Report(e.Message, true, dialog); }
            catch (Exception e) { return Report("Unerwarteter Fehler: " + e.Message, true, dialog); }
        }

        // Launched by the tel: handler there is no console, so errors must appear as a dialog.
        private static int Report(string message, bool error, bool dialog)
        {
            if (dialog) MessageBox.Show(message, "t2med-fon", MessageBoxButtons.OK, error ? MessageBoxIcon.Error : MessageBoxIcon.Information);
            else if (error) Console.Error.WriteLine(message);
            else Console.WriteLine(message);
            return error ? 1 : 0;
        }
    }
}
