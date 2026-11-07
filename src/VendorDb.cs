using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MacChanger
{
    public static class VendorDb
    {
        private const string ResourceName = "OuiData";

        private static readonly Dictionary<string, string> PrefixToVendor =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> VendorPrefixes = new List<string>();
        private static readonly List<VendorItem> AllItems = new List<VendorItem>();
        private static readonly Random Rng = new Random();

        public static string SourceDescription = "";

        private static readonly string[] BuiltIn =
        {
            "00000C,Cisco Systems, Inc.",      "00044B,NVIDIA Corporation",
            "0004AC,IBM Corporation",          "000569,VMware, Inc.",
            "000C29,VMware, Inc.",             "001C14,VMware, Inc.",
            "005056,VMware, Inc.",             "00155D,Microsoft Corporation",
            "0003FF,Microsoft Corporation",    "000D3A,Microsoft Corporation",
            "001DD8,Microsoft Corporation",    "002248,Microsoft Corporation",
            "0050F2,Microsoft Corporation",    "080027,Oracle VirtualBox (PCS)",
            "525400,QEMU KVM virtual NIC",     "001C42,Parallels, Inc.",
            "00E04C,Realtek Semiconductor",    "001018,Broadcom Inc.",
            "00037F,Qualcomm Atheros",         "000C43,Ralink Technology (MediaTek)",
            "0018E7,MediaTek Inc.",            "0002B3,Intel Corporation",
            "001B21,Intel Corporate",          "0024D7,Intel Corporate",
            "000393,Apple Inc.",               "000502,Apple Inc.",
            "0010FA,Apple Inc.",               "0016CB,Apple Inc.",
            "F018F5,Apple Inc.",               "001B78,Hewlett-Packard Company",
            "001422,Dell Inc.",                "002170,Dell Inc.",
            "0023AE,Dell Inc.",                "B8CA3A,Dell Inc.",
            "001632,Samsung Electronics",      "001D25,Samsung Electronics",
            "002399,Samsung Electronics",      "002637,Samsung Electronics",
            "A80600,Samsung Electronics",      "001882,Huawei Technologies",
            "001E10,Huawei Technologies",      "00259E,Huawei Technologies Co.",
            "14CC20,TP-Link Technologies",     "50C7BF,TP-Link Technologies Co.",
            "00055D,D-Link Corporation",       "00179A,D-Link System Inc.",
            "00095B,NETGEAR Inc.",             "000FB5,NETGEAR Inc.",
            "001B2F,NETGEAR Inc.",             "001217,Cisco-Linksys, LLC",
            "000625,Cisco-Linksys, LLC",       "000475,3Com Corporation",
            "080020,Sun Microsystems",         "0002C9,Mellanox Technologies",
            "002590,Mellanox Technologies",    "B827EB,Raspberry Pi Foundation",
            "DCA632,Raspberry Pi Trading Ltd", "E45F01,Raspberry Pi Trading Ltd",
            "D83ADD,Raspberry Pi Foundation",  "001A11,Google, Inc.",
            "546009,Google, Inc."
        };

        public sealed class VendorItem
        {
            public string Name;
            public string Prefix;
            public override string ToString() { return Name; }
        }

        public static void Load()
        {
            PrefixToVendor.Clear();
            VendorPrefixes.Clear();
            AllItems.Clear();
            SourceDescription = "";

            if (PrefixToVendor.Count == 0)
            {
                try
                {
                    Assembly asm = Assembly.GetExecutingAssembly();
                    using (Stream s = asm.GetManifestResourceStream(ResourceName))
                    {
                        if (s != null)
                        {
                            AddLines(ReadAllLines(s));
                            SourceDescription = "built-in database";
                        }
                    }
                }
                catch (Exception) { }
            }
            if (PrefixToVendor.Count == 0)
            {
                foreach (string line in BuiltIn) AddLine(line);
                SourceDescription = "built-in fallback list";
            }

            foreach (KeyValuePair<string, string> kv in PrefixToVendor)
            {
                VendorPrefixes.Add(kv.Key);
            }
            VendorPrefixes.Sort(StringComparer.Ordinal);

            VendorItem anyItem = new VendorItem();
            anyItem.Name = "(Any vendor - full random)";
            anyItem.Prefix = null;
            AllItems.Add(anyItem);

            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kv in PrefixToVendor)
            {
                if (seen.Add(kv.Value)) names.Add(kv.Value);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                VendorItem it = new VendorItem();
                it.Name = name;
                it.Prefix = PrefixForName(name);
                AllItems.Add(it);
            }
        }

        public static IList<VendorItem> Search(string filter, int maxItems, out int totalMatches)
        {
            totalMatches = 0;
            List<VendorItem> result = new List<VendorItem>();
            result.Add(AllItems[0]);
            string match = (filter == null) ? "" : filter.Trim().ToLowerInvariant();
            for (int i = 1; i < AllItems.Count; i++)
            {
                VendorItem it = AllItems[i];
                bool isMatch =
                    match.Length == 0 ||
                    it.Name.ToLowerInvariant().Contains(match) ||
                    (it.Prefix != null && it.Prefix.ToLowerInvariant().Contains(match));
                if (!isMatch) continue;
                totalMatches++;
                if (result.Count < maxItems + 1) result.Add(it);
            }
            return result;
        }

        public static int EntryCount { get { return PrefixToVendor.Count; } }

        public static string RandomVendorPrefix()
        {
            if (VendorPrefixes.Count == 0) return null;
            return VendorPrefixes[Rng.Next(VendorPrefixes.Count)];
        }

        public static IList<VendorItem> Items { get { return AllItems; } }

        private static string PrefixForName(string name)
        {
            foreach (KeyValuePair<string, string> kv in PrefixToVendor)
            {
                if (string.Compare(kv.Value, name, StringComparison.OrdinalIgnoreCase) == 0) return kv.Key;
            }
            return null;
        }

        public static string Identify(string hex12)
        {
            if (string.IsNullOrEmpty(hex12) || hex12.Length < 6) return null;
            string vendor;
            if (PrefixToVendor.TryGetValue(hex12.Substring(0, 6), out vendor)) return vendor;
            return null;
        }

        private static void AddLines(IEnumerable<string> lines)
        {
            foreach (string l in lines) AddLine(l);
        }

        private static void AddLine(string line)
        {
            if (line == null) return;
            string t = line.Trim();
            if (t.Length < 8 || t[0] == '#') return;

            string prefix, name;
            if (t[2] == ':' || t[2] == '-')
            {
                prefix = t.Substring(0, 2) + t.Substring(3, 2) + t.Substring(6, 2);
                name = t.Substring(8).TrimStart('\t', ' ');
            }
            else if (t.Length >= 7 && t[6] == '\t')
            {
                prefix = t.Substring(0, 6);
                name = t.Substring(7).Trim();
            }
            else return;

            if (prefix.Length != 6 || !IsHex(prefix)) return;
            name = name.Trim();
            if (name.Length == 0 || name == "(hex)") return;
            if (name[0] == '(') name = name.Substring(name.IndexOf(')') + 1).Trim();

            PrefixToVendor[prefix] = name;
        }

        private static List<string> ReadAllLines(Stream stream)
        {
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(stream, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                {
                    if (c < 'A' || c > 'F') return false;
                }
            }
            return s.Length == 6;
        }
    }
}
