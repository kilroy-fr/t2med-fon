using System;
using System.Text.RegularExpressions;

namespace T2medFon
{
    internal static class Number
    {
        /// <summary>Extracts a dialable number from a tel:/callto: link; anything else than digits and visual separators is rejected.</summary>
        public static string FromLink(string link, string countryCode, string areaCode = "")
        {
            string value = (link ?? "").Trim();
            foreach (string scheme in new[] { "tel:", "callto:" })
                if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) { value = value.Substring(scheme.Length); break; }
            if (value.StartsWith("//")) value = value.Substring(2);
            // RFC 3966 parameters such as ;phone-context= carry nothing to dial.
            int cut = value.IndexOfAny(new[] { ';', '?' });
            if (cut >= 0) value = value.Substring(0, cut);
            value = Uri.UnescapeDataString(value);
            if (value.Length > 80 || !Regex.IsMatch(value, @"^[0-9+ ().\/-]*\z")) throw Invalid(value);
            // "+49 (0)761 ..." writes the national trunk prefix that must not be dialed after the country code.
            if (value.StartsWith("+")) value = value.Replace("(0)", "");
            string number = Regex.Replace(value, @"[ ().\/-]", "");
            if (!Regex.IsMatch(number, @"^\+?[0-9]{2,20}\z")) throw Invalid(value);
            if (number.StartsWith("00")) number = "+" + number.Substring(2);
            if (number.StartsWith("+") && countryCode != "")
                number = number.StartsWith("+" + countryCode) ? "0" + number.Substring(countryCode.Length + 1) : "00" + number.Substring(1);
            // Local numbers often lack the area code; extensions (up to 3 digits) and special services (1xx) must stay as they are.
            if (areaCode != "" && Regex.IsMatch(number, @"^[2-9][0-9]{3,}\z")) number = areaCode + number;
            return number;
        }

        private static DialException Invalid(string value)
        {
            return new DialException("Ungültige Rufnummer: " + (value.Length > 40 ? value.Substring(0, 40) + "…" : value));
        }
    }
}
