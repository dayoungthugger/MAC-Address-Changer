using System;

namespace MacChanger
{
    internal static class MacUtil
    {
        private static readonly Random Rng = new Random();

        public static string Clean(string input)
        {
            if (input == null) return null;
            string t = input.Trim();
            char[] buf = new char[t.Length];
            int n = 0;
            for (int i = 0; i < t.Length; i++)
            {
                char c = Char.ToUpperInvariant(t[i]);
                if (c == ':' || c == '-' || c == '.' || c == ' ') continue;
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')) buf[n++] = c;
                else return null;
            }
            if (n != 12) return null;
            return new string(buf, 0, n);
        }

        public static string Format(string hex12)
        {
            if (hex12 == null || hex12.Length != 12) return "(unknown)";
            return hex12.Substring(0, 2) + ":" + hex12.Substring(2, 2) + ":" + hex12.Substring(4, 2) + ":" +
                   hex12.Substring(6, 2) + ":" + hex12.Substring(8, 2) + ":" + hex12.Substring(10, 2);
        }

        public static bool IsLocallyAdministered(string prefix6)
        {
            if (prefix6 == null || prefix6.Length < 2) return false;
            return prefix6[1] == '2' || prefix6[1] == '6' || prefix6[1] == 'A' || prefix6[1] == 'E';
        }

        private static string RandomHexDigits(int count)
        {
            char[] buf = new char[count];
            for (int i = 0; i < count; i++)
            {
                int v = Rng.Next(16);
                buf[i] = (v < 10) ? (char)('0' + v) : (char)('A' + (v - 10));
            }
            return new string(buf);
        }

        public static string GenerateRandom(string vendorPrefix6, bool forceLocal)
        {
            string prefix;
            if (!string.IsNullOrEmpty(vendorPrefix6))
            {
                prefix = vendorPrefix6.Trim().ToUpperInvariant();
                if (prefix.Length != 6) prefix = null;
            }
            else
            {
                prefix = VendorDb.RandomVendorPrefix();
            }
            if (prefix == null)
            {
                // nothing usable from the vendor db, stay locally administered
                prefix = new string[] { "02", "06", "0A", "0E" }[Rng.Next(4)] + RandomHexDigits(4);
            }

            string hex = prefix + RandomHexDigits(6);
            if (forceLocal) hex = "02" + hex.Substring(2);
            return hex;
        }
    }
}
