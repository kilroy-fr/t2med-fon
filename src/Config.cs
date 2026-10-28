using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace T2medFon
{
    internal sealed class Config
    {
        // Must match install.ps1, which encrypts the password for the current Windows user.
        public static readonly byte[] Entropy = Encoding.UTF8.GetBytes("t2med-fon");

        public Uri Phone;
        public string Username = "";
        public string Password = "";
        public string DialPath = "/servlet?key=number={number}";
        public string CountryCode = "49";
        public string AreaCode = "";
        public bool TlsVerify = true;
        public int TimeoutSeconds = 5;

        public static string DefaultPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "t2med-fon", "t2med-fon.ini");
        }

        public static Config Load(string path)
        {
            if (!File.Exists(path)) throw new DialException("Konfiguration fehlt: " + path + "\n\nBitte install.ps1 ausführen.");
            try { return Parse(File.ReadAllText(path)); }
            catch (DialException e) { throw new DialException("Fehler in " + path + ":\n" + e.Message); }
        }

        public static Config Parse(string text)
        {
            Config config = new Config();
            HashSet<string> seen = new HashSet<string>();
            int line = 0;
            foreach (string raw in text.Split('\n'))
            {
                line++;
                string entry = raw.Trim();
                if (entry == "" || entry.StartsWith("#") || entry.StartsWith(";")) continue;
                int eq = entry.IndexOf('=');
                if (eq < 0) throw new DialException("Zeile " + line + ": erwartet wird name = wert.");
                string key = entry.Substring(0, eq).Trim(), value = entry.Substring(eq + 1).Trim();
                if (!seen.Add(key)) throw new DialException("Zeile " + line + ": " + key + " ist doppelt angegeben.");
                if (key == "phone") config.Phone = PhoneUri(value);
                else if (key == "username") config.Username = value;
                else if (key == "password") config.Password = value;
                else if (key == "password_dpapi") config.Password = Decrypt(value);
                else if (key == "dial_path")
                {
                    if (!value.StartsWith("/") || !value.Contains("{number}")) throw new DialException("dial_path muss mit / beginnen und {number} enthalten.");
                    config.DialPath = value;
                }
                else if (key == "country_code")
                {
                    if (!Regex.IsMatch(value, @"^([1-9][0-9]{0,2})?\z")) throw new DialException("country_code muss eine Landesvorwahl ohne + sein, z. B. 49, oder leer.");
                    config.CountryCode = value;
                }
                else if (key == "area_code")
                {
                    if (!Regex.IsMatch(value, @"^(0[1-9][0-9]{1,4})?\z")) throw new DialException("area_code muss eine Ortsvorwahl mit führender 0 sein, z. B. 0761, oder leer.");
                    config.AreaCode = value;
                }
                else if (key == "tls_verify")
                {
                    if (value != "true" && value != "false") throw new DialException("tls_verify muss true oder false sein.");
                    config.TlsVerify = value == "true";
                }
                else if (key == "timeout_seconds")
                {
                    int seconds;
                    if (!int.TryParse(value, out seconds) || seconds < 1 || seconds > 60) throw new DialException("timeout_seconds muss zwischen 1 und 60 liegen.");
                    config.TimeoutSeconds = seconds;
                }
                else throw new DialException("Zeile " + line + ": unbekannter Eintrag " + key + ".");
            }
            if (config.Phone == null) throw new DialException("phone fehlt, z. B. phone = http://192.168.1.50");
            if (seen.Contains("password") && seen.Contains("password_dpapi")) throw new DialException("Nur password oder password_dpapi angeben, nicht beides.");
            return config;
        }

        private static Uri PhoneUri(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo != "" || uri.PathAndQuery != "/")
                throw new DialException("phone muss die Adresse des Telefons sein, z. B. http://192.168.1.50");
            return uri;
        }

        private static string Decrypt(string value)
        {
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser)); }
            catch (FormatException) { throw Undecryptable(); }
            catch (CryptographicException) { throw Undecryptable(); }
        }

        private static DialException Undecryptable()
        {
            return new DialException("Das Telefon-Passwort lässt sich nicht entschlüsseln. install.ps1 als dieser Windows-Benutzer erneut ausführen.");
        }
    }
}
