using System;

namespace MacChanger
{
    public sealed class AdapterInfo
    {
        public string Description;

        public string ConnectionName;

        public string NetCfgInstanceId;

        public int WmiIndex = -1;

        public string PnpDeviceId;

        public bool IsVirtual;

        public string RegistrySubkeyName;

        public string CurrentMac;

        public string OriginalMac;

        public string RegistryMac;
        public bool IsWireless;

        public bool Enabled;

        public bool StateKnown;

        public int OperStatus;

        public bool MediaConnected;

        public override string ToString()
        {
            string name = ConnectionName;
            if (string.IsNullOrEmpty(name)) name = "(no connection name)";
            string s = name + "  -  " + Description + "  [" + StatusText() + "]" +
                   (IsVirtual ? " [virtual]" : "");
            if (WmiIndex < 0 && RegistrySubkeyName != null) s += " [registry-only]";
            return s;
        }

        public bool IsUp()
        {
            return Enabled && OperStatus == 1;
        }

        public string StatusText()
        {
            if (StateKnown)
            {
                if (OperStatus == 4 || OperStatus == 6) return "not present";
                if (!Enabled) return "disabled";
                if (OperStatus == 1) return "up";
                if (OperStatus == 7) return "media disconnected";
                if (OperStatus == 2) return "disconnected";
            }
            if (!Enabled) return "disabled";
            return string.IsNullOrEmpty(CurrentMac) ? "no media" : "enabled";
        }

        public string StateText()
        {
            if (StateKnown)
            {
                if (OperStatus == 4 || OperStatus == 6) return "Not present (device not started)";
                if (!Enabled) return "Disabled (in Network Connections)";
                if (OperStatus == 1) return "Up / connected";
                if (OperStatus == 7) return "Media disconnected (lower layer down)";
                if (OperStatus == 2) return "Disconnected (enabled, no link)";
            }
            if (!Enabled) return "Disabled (in Network Connections)";
            return string.IsNullOrEmpty(CurrentMac) ? "Media disconnected" : "Up / connected";
        }

        public bool Matches(string filter)
        {
            if (filter == null) return true;
            string f = filter.Trim();
            if (f.Length == 0) return true;

            if (Has(ConnectionName, f) || Has(Description, f) || Has(StatusText(), f)) return true;
            if (Has(StateText(), f) || Has(RegistrySubkeyName, f)) return true;
            if (IsVirtual && Has("virtual", f)) return true;
            if (IsWireless && (Has("wireless", f) || Has("wifi", f) || Has("wi-fi", f))) return true;

            string hex = Digits(f);
            if (hex.Length >= 4 &&
                (Has(CurrentMac, hex) || Has(OriginalMac, hex) || Has(RegistryMac, hex))) return true;
            return false;
        }

        private static bool Has(string text, string part)
        {
            return text != null && part.Length > 0 &&
                   text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Digits(string text)
        {
            if (text == null) return "";
            char[] buf = new char[text.Length];
            int n = 0;
            foreach (char c in text)
            {
                if (c == ':' || c == '-' || c == '.' || c == ' ' || c == '\t') continue;
                buf[n++] = Char.ToUpperInvariant(c);
            }
            return new string(buf, 0, n);
        }

        public string RegistryKeyPath()
        {
            return AdapterService.BaseKey + "\\" + RegistrySubkeyName;
        }

        public bool IsChangeable()
        {
            return !string.IsNullOrEmpty(NetCfgInstanceId) && !string.IsNullOrEmpty(RegistrySubkeyName);
        }
    }
}
