using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace T2medFon
{
    /// <summary>Plain assertions without a test framework; argument 1 is the built t2med-fon.exe.</summary>
    internal static class Tests
    {
        private static int failures;

        private static int Main(string[] args)
        {
            Test("Rufnummern aus tel:-Links", delegate
            {
                Eq("0761123456", Number.FromLink("tel:0761123456", "49"));
                Eq("0761123456", Number.FromLink("tel:+49761123456", "49"));
                Eq("0761123456", Number.FromLink("tel:+49 (0)761 12 34-56", "49"));
                Eq("0761123456", Number.FromLink("tel:+49761123456;phone-context=example.com", "49"));
                Eq("0761123456", Number.FromLink("TEL:%2B49%20761%20123456", "49"));
                Eq("0761123", Number.FromLink("callto://0049761123", "49"));
                Eq("0041441234567", Number.FromLink("tel:+41441234567", "49"));
                Eq("+49761123456", Number.FromLink("tel:+49761123456", ""));
                Eq("21", Number.FromLink("tel:21", "49"));
                Eq("0761123456", Number.FromLink("0761/123.456", "49"));
                Eq("076140254", Number.FromLink("tel:0761/40254", "49"));
            });
            Test("Ortsvorwahl ergänzen", delegate
            {
                Eq("076140254", Number.FromLink("tel:40254", "49", "0761"));
                Eq("07614025412", Number.FromLink("tel:40 254-12", "49", "0761"));
                Eq("030123456", Number.FromLink("tel:030 123456", "49", "0761"));
                Eq("0761123456", Number.FromLink("tel:+49 761 123456", "49", "0761"));
                Eq("0041441234567", Number.FromLink("tel:+41441234567", "49", "0761"));
                Eq("+41441234567", Number.FromLink("tel:+41441234567", "", "0761"));
                Eq("2154", Number.FromLink("tel:2154", "49", "0761"));
                Eq("112", Number.FromLink("tel:112", "49", "0761"));
                Eq("116117", Number.FromLink("tel:116117", "49", "0761"));
                Eq("40254", Number.FromLink("tel:40254", "49"));
            });
            Test("Ungültige Rufnummern", delegate
            {
                foreach (string link in new[] { "tel:", "tel:1", "tel:abc", "tel:0761&key=reboot", "tel:0761%0d%0a", "tel:++49761", "tel:0761#1", "sip:0761123" })
                    Rejects("Ungültige Rufnummer", delegate { Number.FromLink(link, "49"); });
            });
            Test("Konfiguration", delegate
            {
                Config config = Config.Parse("# Kommentar\r\nphone = http://192.168.1.50\r\n");
                Eq("http://192.168.1.50/", config.Phone.ToString());
                Eq("", config.Username); Eq("49", config.CountryCode); Eq("", config.AreaCode); Eq(true, config.TlsVerify); Eq(5, config.TimeoutSeconds);
                config = Config.Parse("phone = https://telefon.praxis.example:8443\nusername = admin\npassword = geheim\ncountry_code =\narea_code = 07633\ntls_verify = false\ntimeout_seconds = 9\ndial_path = /servlet?key=number={number}&outgoing_uri=21\n");
                Eq("admin", config.Username); Eq("geheim", config.Password); Eq("", config.CountryCode); Eq("07633", config.AreaCode); Eq(false, config.TlsVerify); Eq(9, config.TimeoutSeconds);
                Rejects("phone fehlt", delegate { Config.Parse("username = admin"); });
                Rejects("unbekannter Eintrag", delegate { Config.Parse("phone = http://192.168.1.50\nphon = x"); });
                Rejects("doppelt", delegate { Config.Parse("phone = http://192.168.1.50\nphone = http://192.168.1.51"); });
                Rejects("phone muss", delegate { Config.Parse("phone = ftp://192.168.1.50"); });
                Rejects("phone muss", delegate { Config.Parse("phone = http://admin:x@192.168.1.50"); });
                Rejects("phone muss", delegate { Config.Parse("phone = http://192.168.1.50/servlet"); });
                Rejects("dial_path", delegate { Config.Parse("phone = http://192.168.1.50\ndial_path = /servlet?key=number=0"); });
                Rejects("country_code", delegate { Config.Parse("phone = http://192.168.1.50\ncountry_code = +49"); });
                foreach (string areaCode in new[] { "761", "0761/", "00761", "0761234" })
                    Rejects("area_code", delegate { Config.Parse("phone = http://192.168.1.50\narea_code = " + areaCode); });
                Rejects("tls_verify", delegate { Config.Parse("phone = http://192.168.1.50\ntls_verify = nein"); });
                Rejects("nicht beides", delegate { Config.Parse("phone = http://192.168.1.50\npassword = a\npassword_dpapi = " + Protect("a")); });
                Rejects("Konfiguration fehlt", delegate { Config.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini")); });
            });
            Test("Passwort per DPAPI", delegate
            {
                Eq("gehéim", Config.Parse("phone = http://192.168.1.50\npassword_dpapi = " + Protect("gehéim")).Password);
                Rejects("entschlüsseln", delegate { Config.Parse("phone = http://192.168.1.50\npassword_dpapi = " + Convert.ToBase64String(Encoding.UTF8.GetBytes("klartext"))); });
                Rejects("entschlüsseln", delegate { Config.Parse("phone = http://192.168.1.50\npassword_dpapi = kein base64"); });
            });
            Test("Wählen ohne Anmeldung", delegate
            {
                using (MockPhone phone = new MockPhone("200 OK"))
                {
                    new Yealink(Config.Parse("phone = " + phone.Url)).Dial("0761123456");
                    Eq(1, phone.Requests.Count);
                    Contains(phone.Requests[0], "GET /servlet?key=number=0761123456 HTTP/1.1");
                    Eq(false, phone.Requests[0].Contains("Authorization:"));
                }
            });
            Test("Wählen mit Anmeldung", delegate
            {
                using (MockPhone phone = new MockPhone("401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"phone\"", "200 OK"))
                {
                    new Yealink(Config.Parse("phone = " + phone.Url + "\nusername = admin\npassword = geheim")).Dial("21");
                    Eq(2, phone.Requests.Count);
                    Contains(phone.Requests[1], "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:geheim")));
                }
            });
            Test("Fehler des Telefons", delegate
            {
                string[][] cases = {
                    new[] { "403 Forbidden", "Action URI allow IP List" },
                    new[] { "401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"phone\"", "Anmeldung abgelehnt" },
                    new[] { "302 Found\r\nLocation: https://127.0.0.1/", "leitet weiter" },
                    new[] { "500 Internal Server Error", "HTTP 500" },
                };
                foreach (string[] c in cases)
                    using (MockPhone phone = new MockPhone(c[0], c[0]))
                        Rejects(c[1], delegate { new Yealink(Config.Parse("phone = " + phone.Url + "\nusername = admin\npassword = falsch")).Dial("21"); });
                Rejects("nicht erreichbar", delegate { new Yealink(Config.Parse("phone = http://127.0.0.1:" + FreePort())).Dial("21"); });
            });
            if (args.Length > 0) Test("t2med-fon.exe", delegate
            {
                using (MockPhone phone = new MockPhone("200 OK", "200 OK"))
                {
                    string config = Path.Combine(Path.GetTempPath(), "t2med-fon-test-" + Guid.NewGuid() + ".ini");
                    File.WriteAllText(config, "phone = " + phone.Url + "\narea_code = 0761\n");
                    try
                    {
                        Eq(0, Run(args[0], "--no-dialog --config \"" + config + "\" \"tel:+49 761 123456\"").Item1);
                        Contains(phone.Requests[0], "GET /servlet?key=number=0761123456 ");
                        Eq(0, Run(args[0], "--no-dialog --config \"" + config + "\" \"tel:40 254\"").Item1);
                        Contains(phone.Requests[1], "GET /servlet?key=number=076140254 ");
                        Tuple<int, string> invalid = Run(args[0], "--no-dialog --config \"" + config + "\" \"tel:0761&key=reboot\"");
                        Eq(1, invalid.Item1);
                        Contains(invalid.Item2, "Ungültige Rufnummer");
                        Eq(2, phone.Requests.Count);
                        Eq(1, Run(args[0], "--no-dialog --config \"" + config + ".fehlt\" tel:21").Item1);
                        Eq(0, Run(args[0], "--no-dialog --version").Item1);
                    }
                    finally { File.Delete(config); }
                }
            });
            Console.WriteLine(failures == 0 ? "Alle Tests bestanden." : failures + " Test(s) fehlgeschlagen.");
            return failures == 0 ? 0 : 1;
        }

        private delegate void Body();

        private static void Test(string name, Body body)
        {
            try { body(); Console.WriteLine("ok   " + name); }
            catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
        }

        private static void Eq(object expected, object actual)
        {
            if (!Equals(expected, actual)) throw new Exception("erwartet " + expected + ", erhalten " + actual);
        }

        private static void Contains(string haystack, string needle)
        {
            if (!haystack.Contains(needle)) throw new Exception("\"" + needle + "\" fehlt in \"" + haystack + "\"");
        }

        private static void Rejects(string message, Body body)
        {
            try { body(); }
            catch (DialException e) { Contains(e.Message, message); return; }
            throw new Exception("keine Ablehnung, erwartet: " + message);
        }

        private static string Protect(string password)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Config.Entropy, DataProtectionScope.CurrentUser));
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static Tuple<int, string> Run(string exe, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo(exe, arguments) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.Default };
            using (Process process = Process.Start(info))
            {
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return Tuple.Create(process.ExitCode, error);
            }
        }
    }

    /// <summary>Loopback stand-in for the phone: answers each connection with the next canned status line and headers.</summary>
    internal sealed class MockPhone : IDisposable
    {
        public readonly List<string> Requests = new List<string>();
        private readonly Queue<string> responses;
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

        public MockPhone(params string[] responses)
        {
            this.responses = new Queue<string>(responses);
            listener.Start();
            new Thread(Serve) { IsBackground = true }.Start();
        }

        public string Url { get { return "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port; } }

        public void Dispose() { listener.Stop(); }

        private void Serve()
        {
            try
            {
                while (true)
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        NetworkStream stream = client.GetStream();
                        StringBuilder head = new StringBuilder();
                        while (!head.ToString().EndsWith("\r\n\r\n"))
                        {
                            int b = stream.ReadByte();
                            if (b < 0) break;
                            head.Append((char)b);
                        }
                        string response;
                        lock (Requests)
                        {
                            Requests.Add(head.ToString());
                            response = responses.Count > 0 ? responses.Dequeue() : "500 Keine Antwort vorgesehen";
                        }
                        byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 " + response + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        stream.Write(bytes, 0, bytes.Length);
                    }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
