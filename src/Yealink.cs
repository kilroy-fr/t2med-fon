using System;
using System.Net;

namespace T2medFon
{
    /// <summary>Dials through the Action URI of a Yealink desk phone.</summary>
    internal sealed class Yealink
    {
        private readonly Config config;

        public Yealink(Config config) { this.config = config; }

        public Uri DialUri(string number)
        {
            return new Uri(config.Phone, config.DialPath.Replace("{number}", Uri.EscapeDataString(number)));
        }

        public void Dial(string number)
        {
            Uri uri = DialUri(number);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AllowAutoRedirect = false;
            request.KeepAlive = false;
            request.Proxy = null; // the phone sits in the practice LAN, never behind a proxy
            request.Timeout = request.ReadWriteTimeout = config.TimeoutSeconds * 1000;
            if (config.Username != "")
            {
                // Firmware versions differ between Basic and Digest; the phone's challenge picks one.
                NetworkCredential credential = new NetworkCredential(config.Username, config.Password);
                CredentialCache cache = new CredentialCache();
                cache.Add(config.Phone, "Basic", credential);
                cache.Add(config.Phone, "Digest", credential);
                request.Credentials = cache;
            }
            if (!config.TlsVerify) request.ServerCertificateValidationCallback = delegate { return true; };
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) Check((int)response.StatusCode);
            }
            catch (WebException e)
            {
                HttpWebResponse response = e.Response as HttpWebResponse;
                if (response != null) using (response) Check((int)response.StatusCode);
                string phone = config.Phone.Authority;
                if (e.Status == WebExceptionStatus.Timeout) throw new DialException("Das Telefon (" + phone + ") antwortet nicht. Ist es eingeschaltet und im Netz?");
                if (e.Status == WebExceptionStatus.TrustFailure) throw new DialException("Das Zertifikat des Telefons (" + phone + ") wird nicht anerkannt. Für das selbst signierte Zertifikat des Telefons tls_verify = false setzen.");
                throw new DialException("Das Telefon (" + phone + ") ist nicht erreichbar: " + e.Message);
            }
        }

        private static void Check(int status)
        {
            if (status >= 200 && status < 300) return;
            if (status == 401) throw new DialException("Das Telefon hat die Anmeldung abgelehnt. Benutzer und Passwort prüfen (install.ps1 erneut ausführen).");
            if (status == 403) throw new DialException("Das Telefon nimmt keine Wählbefehle von diesem PC an. In der Weboberfläche des Telefons unter Features → Remote Control die IP-Adresse dieses PCs bei „Action URI allow IP List“ eintragen.");
            if (status >= 300 && status < 400) throw new DialException("Das Telefon leitet weiter, statt zu wählen (HTTP " + status + "). Adresse prüfen, z. B. https:// statt http://.");
            throw new DialException("Das Telefon hat den Wählbefehl abgelehnt (HTTP " + status + ").");
        }
    }
}
