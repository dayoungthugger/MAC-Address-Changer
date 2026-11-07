using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MacChanger
{
    public static class AdapterService
    {
        public const string BaseKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        private static readonly string OriginalsFile =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MacChanger",
                "original-macs.txt");

        public static List<AdapterInfo> EnumerateAdapters(bool includeVirtual)
        {
            return EnumerateAdapters(includeVirtual, false);
        }

        public static List<AdapterInfo> EnumerateAdapters(bool includeVirtual, bool upOnly)
        {
            RegistryMaps reg = ReadRegistryMaps();
            StateMaps states = LoadAdapterStates();
            Dictionary<int, string> guidByIndex = LoadAdapterGuidsByIndex();
            List<AdapterInfo> result = new List<AdapterInfo>();

            ManagementObjectSearcher searcher = null;
            ManagementObjectCollection found = null;
            try
            {
                searcher = new ManagementObjectSearcher("root\\cimv2",
                    "SELECT * FROM Win32_NetworkAdapter");
                found = searcher.Get();
            }
            catch (Exception ex)
            {
                if (searcher != null) searcher.Dispose();
                throw new ApplicationException("Unable to query the network adapters via WMI: " + ex.Message, ex);
            }

            foreach (ManagementObject mo in found)
            {
                try
                {
                    AdapterInfo a = FromWmi(mo, reg, states, guidByIndex, includeVirtual);
                    if (a != null) result.Add(a);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mo.Dispose();
                }
            }
            searcher.Dispose();

            if (includeVirtual) AddRegistryOnlyAdapters(result, reg);

            MergeOriginals(result);

            if (upOnly) result.RemoveAll(delegate(AdapterInfo a) { return !a.IsUp(); });

            result.Sort(delegate(AdapterInfo x, AdapterInfo y)
            {
                int cx = string.IsNullOrEmpty(x.ConnectionName) ? 9 : 0;
                int cy = string.IsNullOrEmpty(y.ConnectionName) ? 9 : 0;
                if (cx != cy) return cx - cy;
                return string.CompareOrdinal(x.ToString(), y.ToString());
            });
            return result;
        }

        private static void AddRegistryOnlyAdapters(List<AdapterInfo> result, RegistryMaps reg)
        {
            Dictionary<string, RegistryEntry> leftovers = new Dictionary<string, RegistryEntry>(StringComparer.Ordinal);
            foreach (RegistryEntry e in reg.ByGuid.Values)
            {
                if (e != null && !e.Claimed) leftovers[e.SubkeyName] = e;
            }
            foreach (RegistryEntry e in reg.ByMatchingDeviceId.Values)
            {
                if (e != null && !e.Claimed && !leftovers.ContainsKey(e.SubkeyName)) leftovers[e.SubkeyName] = e;
            }

            foreach (RegistryEntry e in leftovers.Values)
            {
                AdapterInfo a = new AdapterInfo();
                a.Description = e.DriverDesc;
                a.ConnectionName = e.ConnectionName;
                a.NetCfgInstanceId = e.Guid;
                a.WmiIndex = -1;
                a.RegistrySubkeyName = e.SubkeyName;
                a.RegistryMac = e.NetworkAddress;
                a.IsVirtual = true;
                a.IsWireless =
                    (e.DriverDesc != null &&
                     (e.DriverDesc.IndexOf("wireless", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      e.DriverDesc.IndexOf("802.11", StringComparison.OrdinalIgnoreCase) >= 0));
                result.Add(a);
            }
        }

        private static Dictionary<int, string> LoadAdapterGuidsByIndex()
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            ManagementObjectSearcher s = null;
            ManagementObjectCollection col = null;
            try
            {
                s = new ManagementObjectSearcher("root\\cimv2",
                    "SELECT Index, SettingID FROM Win32_NetworkAdapterConfiguration");
                col = s.Get();
                foreach (ManagementObject mo in col)
                {
                    try
                    {
                        int index = Convert.ToInt32(mo["Index"]);
                        string guid = mo["SettingID"] as string;
                        if (guid != null && !map.ContainsKey(index)) map[index] = guid;
                    }
                    catch (Exception) { }
                    finally { mo.Dispose(); }
                }
            }
            catch (Exception) { }
            finally
            {
                if (s != null) s.Dispose();
            }
            return map;
        }

        private sealed class RegistryMaps
        {
            public readonly Dictionary<string, RegistryEntry> ByGuid =
                new Dictionary<string, RegistryEntry>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, RegistryEntry> ByMatchingDeviceId =
                new Dictionary<string, RegistryEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class AdapterState
        {
            public bool AdminUp;
            public int OperStatus;
            public bool MediaConnected;
        }

        private sealed class StateMaps
        {
            public readonly Dictionary<string, AdapterState> ByGuid =
                new Dictionary<string, AdapterState>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, AdapterState> ByDeviceId =
                new Dictionary<string, AdapterState>(StringComparer.OrdinalIgnoreCase);

            public readonly Dictionary<string, AdapterState> ByAlias =
                new Dictionary<string, AdapterState>(StringComparer.OrdinalIgnoreCase);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MibIfRow2
        {
            public ulong InterfaceLuid;
            public uint InterfaceIndex;
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Description;
            public uint PhysicalAddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PhysicalAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PermanentPhysicalAddress;
            public uint Mtu;
            public uint Type;
            public uint TunnelType;
            public uint MediaType;
            public uint PhysicalMediumType;
            public uint AccessType;
            public uint DirectionType;
            public byte InterfaceAndOperStatusFlags;
            public uint OperStatus;
            public uint AdminStatus;
            public uint MediaConnectState;
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int GetIfEntry2(IntPtr row);

        private const int IfRowBufferSize = 1600;

        private static void LoadInterfaceStates(StateMaps maps)
        {
            IntPtr buf = Marshal.AllocHGlobal(IfRowBufferSize);
            try
            {
                for (int index = 0; index <= 255; index++)
                {
                    for (int i = 0; i < IfRowBufferSize; i++) Marshal.WriteByte(buf, i, 0);
                    // the index follows the 8-byte LUID
                    Marshal.WriteInt32(buf, 8, index);
                    if (GetIfEntry2(buf) != 0) continue;

                    MibIfRow2 r = (MibIfRow2)Marshal.PtrToStructure(buf, typeof(MibIfRow2));
                    AdapterState st = new AdapterState();
                    st.AdminUp = (r.AdminStatus == 1);
                    st.OperStatus = (int)r.OperStatus;
                    st.MediaConnected = (r.MediaConnectState == 1);

                    maps.ByGuid[CleanGuid(r.InterfaceGuid.ToString("B"))] = st;
                    if (!string.IsNullOrEmpty(r.Alias)) maps.ByAlias[r.Alias] = st;
                }
            }
            catch (Exception) { }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        private static StateMaps LoadAdapterStates()
        {
            StateMaps maps = new StateMaps();
            ManagementObjectSearcher s = null;
            try
            {
                s = new ManagementObjectSearcher("root\\StandardCimv2",
                    "SELECT InterfaceGuid, DeviceID, InterfaceAdminStatus, InterfaceOperationalStatus," +
                    " MediaConnectState FROM MSFT_NetAdapter");
                foreach (ManagementObject mo in s.Get())
                {
                    try
                    {
                        AdapterState st = ReadState(mo);
                        string g = mo["InterfaceGuid"] as string;
                        string dev = mo["DeviceID"] as string;
                        if (!string.IsNullOrEmpty(g)) maps.ByGuid[CleanGuid(g)] = st;
                        if (!string.IsNullOrEmpty(dev)) maps.ByDeviceId[dev.Trim().ToUpperInvariant()] = st;
                    }
                    catch (Exception) { }
                    finally { mo.Dispose(); }
                }
            }
            catch (Exception) { }
            finally
            {
                if (s != null) s.Dispose();
            }

            LoadInterfaceStates(maps);

            ManagementObjectSearcher ips = null;
            try
            {
                ips = new ManagementObjectSearcher("root\\StandardCimv2",
                    "SELECT InterfaceAlias, ConnectionState FROM MSFT_NetIPInterface");
                foreach (ManagementObject mo in ips.Get())
                {
                    try
                    {
                        string alias = mo["InterfaceAlias"] as string;
                        if (string.IsNullOrEmpty(alias)) continue;
                        bool connected = (Convert.ToUInt32(mo["ConnectionState"]) == 1);

                        AdapterState st;
                        if (!maps.ByAlias.TryGetValue(alias, out st))
                        {
                            st = new AdapterState();
                            st.AdminUp = true;
                            st.OperStatus = connected ? 1 : 2;
                            st.MediaConnected = connected;
                            maps.ByAlias[alias] = st;
                        }
                        else if (connected)
                        {
                            st.OperStatus = 1;
                            st.MediaConnected = true;
                        }
                    }
                    catch (Exception) { }
                    finally { mo.Dispose(); }
                }
            }
            catch (Exception) { }
            finally
            {
                if (ips != null) ips.Dispose();
            }
            return maps;
        }

        private static AdapterState ReadState(ManagementObject mo)
        {
            AdapterState st = new AdapterState();
            st.AdminUp = (Convert.ToUInt32(mo["InterfaceAdminStatus"]) == 1);
            st.OperStatus = Convert.ToInt32(mo["InterfaceOperationalStatus"]);
            st.MediaConnected = (Convert.ToUInt32(mo["MediaConnectState"]) == 1);
            return st;
        }

        private static void ApplyState(AdapterInfo a, AdapterState st)
        {
            if (a == null || st == null) return;
            a.StateKnown = true;
            a.Enabled = st.AdminUp;
            a.OperStatus = st.OperStatus;
            a.MediaConnected = st.MediaConnected;
        }

        private static AdapterState LoadAdapterState(AdapterInfo a)
        {
            if (a == null) return null;
            string filter = null;
            if (!string.IsNullOrEmpty(a.NetCfgInstanceId))
                filter = "InterfaceGuid = '" + a.NetCfgInstanceId.Replace("'", "") + "'";
            else if (!string.IsNullOrEmpty(a.PnpDeviceId))
                filter = "DeviceID = '" + a.PnpDeviceId.Replace("'", "") + "'";
            if (filter == null) return null;

            ManagementObjectSearcher s = null;
            try
            {
                s = new ManagementObjectSearcher("root\\StandardCimv2",
                    "SELECT InterfaceAdminStatus, InterfaceOperationalStatus, MediaConnectState" +
                    " FROM MSFT_NetAdapter WHERE " + filter);
                foreach (ManagementObject mo in s.Get())
                {
                    try { return ReadState(mo); }
                    finally { mo.Dispose(); }
                }
            }
            catch (Exception) { }
            finally
            {
                if (s != null) s.Dispose();
            }
            return null;
        }

        private static bool GetBool(ManagementObject mo, string prop)
        {
            object v = null;
            try { v = mo[prop]; } catch (Exception) { }
            return (v is bool) && (bool)v;
        }

        private static string GetString(ManagementObject mo, string prop)
        {
            object v = null;
            try { v = mo[prop]; } catch (Exception) { }
            return (v is string) ? (string)v : null;
        }

        private static bool LooksVirtual(string text)
        {
            if (text == null) return false;
            string t = text.ToLowerInvariant();
            return
                t.IndexOf("wan miniport", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("miniport", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("local area connection", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("wi-fi direct", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("wifi direct", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("hosted network", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("virtual", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("vethernet", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("loopback", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("km-test", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("tap-", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("tap adapter", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("wintun", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("npcap", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("hyper-v", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("vmware", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("virtualbox", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("npf_", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("bridge", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("multiplexor", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("checkpoint", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("openvpn", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("wireguard", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("zerotier", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("kernel debug", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("tunnel", StringComparison.Ordinal) >= 0;
        }

        private static bool AlwaysSkipped(string name)
        {
            if (name == null) return false;
            string t = name.ToLowerInvariant();
            return
                t.IndexOf("teredo", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("isatap", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("6to4", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("ip-https", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("iphttps", StringComparison.Ordinal) >= 0;
        }

        private static AdapterInfo FromWmi(ManagementObject mo, RegistryMaps reg, StateMaps states,
                                           Dictionary<int, string> guidByIndex, bool includeVirtual)
        {
            string name = GetString(mo, "Name");
            if (string.IsNullOrEmpty(name)) return null;

            string type = GetString(mo, "AdapterType");

            int wmiIndex = -1;
            try { wmiIndex = Convert.ToInt32(mo["Index"]); } catch (Exception) { }

            string guid = null;
            if (wmiIndex >= 0 && guidByIndex.ContainsKey(wmiIndex)) guid = guidByIndex[wmiIndex];

            string pnpId = GetString(mo, "PNPDeviceID");

            RegistryEntry re = null;
            if (!string.IsNullOrEmpty(guid))
            {
                if (!reg.ByGuid.TryGetValue(CleanGuid(guid), out re) && pnpId != null)
                {
                    reg.ByMatchingDeviceId.TryGetValue(pnpId.Trim().ToUpperInvariant(), out re);
                }
            }
            else if (pnpId != null)
            {
                reg.ByMatchingDeviceId.TryGetValue(pnpId.Trim().ToUpperInvariant(), out re);
            }

            RegistryEntry re2 = (re != null) ? re : null;

            string conn = GetString(mo, "NetConnectionID");
            if (string.IsNullOrEmpty(conn) && re2 != null) conn = re2.ConnectionName;

            bool pseudoType = (type != null && (type == "Tunnel" || type == "WAN"));

            bool virtualByName = pseudoType || LooksVirtual(name) || LooksVirtual(type) ||
                                 LooksVirtual(conn);

            if (AlwaysSkipped(name)) return null;
            if (virtualByName && !includeVirtual) return null;

            AdapterState st = null;
            if (!string.IsNullOrEmpty(guid)) states.ByGuid.TryGetValue(CleanGuid(guid), out st);
            if (st == null && pnpId != null) states.ByDeviceId.TryGetValue(pnpId.Trim().ToUpperInvariant(), out st);
            if (st == null && !string.IsNullOrEmpty(conn)) states.ByAlias.TryGetValue(conn, out st);
            if (st == null && virtualByName)
            {
                st = new AdapterState();
                st.AdminUp = true;
                st.OperStatus = 2;
            }

            string mac = MacUtil.Clean(GetString(mo, "MACAddress"));

            AdapterInfo a = new AdapterInfo();
            a.Description = name;
            a.ConnectionName = conn;
            a.CurrentMac = mac;
            a.IsWireless =
                (type != null && type.IndexOf("wireless", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (name.IndexOf("wireless", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (name.IndexOf("802.11", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (conn != null && (conn.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  conn.IndexOf("WLAN", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                (re2 != null && re2.DriverDesc != null &&
                 (re2.DriverDesc.IndexOf("wireless", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  re2.DriverDesc.IndexOf("802.11", StringComparison.OrdinalIgnoreCase) >= 0));
            object netEnabled = null;
            try { netEnabled = mo["NetEnabled"]; } catch (Exception) { }

            a.Enabled = (netEnabled is bool) ? (bool)netEnabled : true;
            a.NetCfgInstanceId = guid;
            a.PnpDeviceId = pnpId;
            a.WmiIndex = wmiIndex;
            a.IsVirtual = virtualByName;
            ApplyState(a, st);
            if (re2 != null)
            {
                re2.Claimed = true;
                a.RegistrySubkeyName = re2.SubkeyName;
                a.RegistryMac = re2.NetworkAddress;
            }
            return a;
        }

        private sealed class RegistryEntry
        {
            public string SubkeyName;
            public string DriverDesc;
            public string NetworkAddress;
            public string Guid;
            public string ConnectionName;

            public bool Claimed;
        }

        private const string ConnectionKey =
            @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        private static string ReadConnectionName(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    ConnectionKey + "\\" + guid + "\\Connection", false))
                {
                    if (k == null) return null;
                    return k.GetValue("Name") as string;
                }
            }
            catch (Exception) { return null; }
        }

        private static string CleanGuid(string guid)
        {
            if (guid == null) return null;
            return guid.Replace("{", "").Replace("}", "").Trim().ToUpperInvariant();
        }

        private static RegistryMaps ReadRegistryMaps()
        {
            RegistryMaps maps = new RegistryMaps();
            using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(BaseKey, false))
            {
                if (baseKey == null) return maps;
                foreach (string sub in baseKey.GetSubKeyNames())
                {
                    if (sub == null || sub.Length != 4 || !IsAllDigits(sub)) continue;
                    using (RegistryKey k = baseKey.OpenSubKey(sub, false))
                    {
                        if (k == null) continue;
                        RegistryEntry e = new RegistryEntry();
                        e.SubkeyName = sub;
                        e.DriverDesc = (k.GetValue("DriverDesc") as string) ?? "(unknown adapter)";

                        object g = null;
                        try { g = k.GetValue("NetCfgInstanceId"); } catch (Exception) { }
                        object m = null;
                        try { m = k.GetValue("MatchingDeviceId"); } catch (Exception) { }
                        object n = null;
                        try { n = k.GetValue("NetworkAddress"); } catch (Exception) { }

                        e.NetworkAddress = (n is string) ? (string)n : null;
                        e.Guid = (g is string) ? (string)g : null;
                        e.ConnectionName = ReadConnectionName(e.Guid);

                        object ndt = null;
                        try { ndt = k.GetValue("*NdisDeviceType"); } catch (Exception) { }

                        bool loopbackish = (ndt is int && (int)ndt == 1);
                        if (g is string && !string.IsNullOrEmpty((string)g))
                        {
                            if (loopbackish) maps.ByGuid[CleanGuid((string)g)] = null;
                            else maps.ByGuid[CleanGuid((string)g)] = e;
                        }
                        if (m is string && !string.IsNullOrEmpty((string)m))
                        {
                            string key = ((string)m).Trim().ToUpperInvariant();
                            if (loopbackish) maps.ByMatchingDeviceId[key] = null;
                            else maps.ByMatchingDeviceId[key] = e;
                        }
                    }
                }
            }
            return maps;
        }

        private static bool IsAllDigits(string s)
        {
            foreach (char c in s)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        private static RegistryKey OpenAdapterKey(AdapterInfo a, bool writable)
        {
            if (a == null || !a.IsChangeable()) return null;
            return Registry.LocalMachine.OpenSubKey(BaseKey + "\\" + a.RegistrySubkeyName, writable);
        }

        public static void RefreshAdapter(AdapterInfo a)
        {
            if (a == null) return;

            if (a.WmiIndex >= 0)
            {
                string query = "SELECT * FROM Win32_NetworkAdapter WHERE Index = " + a.WmiIndex.ToString();
                ManagementObjectSearcher s = new ManagementObjectSearcher("root\\cimv2", query);
                ManagementObjectCollection col = s.Get();
                foreach (ManagementObject mo in col)
                {
                    try
                    {
                        string mac = MacUtil.Clean(GetString(mo, "MACAddress"));
                        if (mac != null) a.CurrentMac = mac;
                        object en = null;
                        try { en = mo["NetEnabled"]; } catch (Exception) { }
                        if (en is bool) a.Enabled = (bool)en;
                    }
                    finally { mo.Dispose(); }
                }
                s.Dispose();
            }

            ApplyState(a, LoadAdapterState(a));

            using (RegistryKey k = OpenAdapterKey(a, false))
            {
                if (k != null)
                {
                    a.RegistryMac = k.GetValue("NetworkAddress", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (a.RegistryMac != null) a.RegistryMac = a.RegistryMac.Trim().ToUpperInvariant();
                }
            }
        }

        private static Dictionary<string, string> LoadOriginals()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(OriginalsFile))
                {
                    foreach (string line in File.ReadAllLines(OriginalsFile))
                    {
                        string t = line.Trim();
                        int tab = t.IndexOf('\t');
                        if (tab <= 0) continue;
                        map[t.Substring(0, tab)] = t.Substring(tab + 1);
                    }
                }
            }
            catch (Exception) { }
            return map;
        }

        private static void MergeOriginals(List<AdapterInfo> adapters)
        {
            Dictionary<string, string> existing = LoadOriginals();
            bool changed = false;

            foreach (AdapterInfo a in adapters)
            {
                if (!a.IsChangeable()) continue;
                if (string.IsNullOrEmpty(a.CurrentMac) || string.IsNullOrEmpty(a.NetCfgInstanceId)) continue;

                string firstSeen;
                if (existing.TryGetValue(a.NetCfgInstanceId, out firstSeen))
                {
                    a.OriginalMac = firstSeen;
                }
                else
                {
                    existing[a.NetCfgInstanceId] = a.CurrentMac;
                    changed = true;
                }
            }
            if (!changed) return;

            try
            {
                string dir = Path.GetDirectoryName(OriginalsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in existing) lines.Add(kv.Key + "\t" + kv.Value);
                File.WriteAllLines(OriginalsFile, lines.ToArray());
            }
            catch (Exception) { }
        }

        public sealed class OperationResult
        {
            public bool Success;
            public bool Verified;
            public bool DriverRejected;
            public string Message;
            public AdapterInfo Adapter;
        }

        public static OperationResult ApplyMac(AdapterInfo a, string hex12,
                                               bool persistent, bool restartNow, Action<string> log)
        {
            OperationResult r = new OperationResult();
            r.Adapter = a;

            using (RegistryKey k = OpenAdapterKey(a, true))
            {
                if (k == null)
                {
                    r.Message = "Registry key for this adapter could not be opened for writing.";
                    return r;
                }
                k.SetValue("NetworkAddress", hex12, RegistryValueKind.String);
                if (log != null) log("Registry: NetworkAddress set to " + MacUtil.Format(hex12) +
                                     (persistent ? " (persistent)" : " (temporary)"));
            }

            if (!restartNow)
            {
                a.RegistryMac = hex12;
                r.Success = true;
                r.Verified = false;
                r.Message = "Value written. It takes effect the next time the adapter is disabled/" +
                            "enabled, its driver reloads or the computer restarts.";
                if (log != null) log(r.Message);
                return r;
            }

            if (!a.Enabled)
            {
                if (!persistent)
                {
                    ClearNetworkAddress(a);
                    r.Message = "Adapter is disabled. Enable it first, then apply a non-persistent change.";
                    if (log != null) log("ERROR: " + r.Message);
                    return r;
                }
                r.Success = true;
                r.Verified = true;
                r.Message = "Adapter is disabled. The address will apply automatically the next time it is enabled.";
                a.RegistryMac = hex12;
                if (log != null) log(r.Message);
                return r;
            }

            try
            {
                RestartAdapter(a, log);
            }
            finally
            {
                if (!persistent)
                {
                    ClearNetworkAddress(a);
                    if (log != null) log("Registry override deleted again - change is temporary," +
                                         " it reverts on reboot or adapter restart.");
                    a.RegistryMac = null;
                }
            }

            RefreshAdapter(a);
            if (!string.IsNullOrEmpty(a.CurrentMac) && string.CompareOrdinal(a.CurrentMac, hex12) == 0)
            {
                r.Success = true;
                r.Verified = true;
            }
            else
            {
                r.Success = true;
                r.DriverRejected = true;
                r.Verified = false;
            }
            return r;
        }

        public static OperationResult RestoreMac(AdapterInfo a, bool restartNow, Action<string> log)
        {
            OperationResult r = new OperationResult();
            r.Adapter = a;

            bool hadOverride = ClearNetworkAddress(a);
            a.RegistryMac = null;

            if (!hadOverride && a.Enabled)
            {
                RefreshAdapter(a);
                r.Success = true;
                r.Verified = true;
                r.Message = "No software override was set - the adapter already uses its factory address.";
                if (log != null) log(r.Message);
                return r;
            }

            if (log != null) log("Registry override deleted.");

            if (!restartNow || !a.Enabled)
            {
                RefreshAdapter(a);
                r.Success = true;
                r.Verified = true;
                r.Message = "Override removed. The factory address returns the next time the adapter" +
                            " is disabled/enabled, its driver reloads or the computer restarts.";
                if (log != null) log(r.Message);
                return r;
            }

            RestartAdapter(a, log);
            RefreshAdapter(a);
            r.Success = true;
            r.Verified = true;
            return r;
        }

        private static bool ClearNetworkAddress(AdapterInfo a)
        {
            using (RegistryKey k = OpenAdapterKey(a, true))
            {
                if (k == null) return false;
                if (k.GetValue("NetworkAddress", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == null)
                {
                    a.RegistryMac = null;
                    return false;
                }
                k.DeleteValue("NetworkAddress", false);
                a.RegistryMac = null;
                return true;
            }
        }

        private static ManagementObject FindByIndex(int wmiIndex)
        {
            if (wmiIndex < 0) return null;
            string query = "SELECT * FROM Win32_NetworkAdapter WHERE Index = " + wmiIndex.ToString();
            ManagementObjectSearcher s = new ManagementObjectSearcher("root\\cimv2", query);
            ManagementObjectCollection col = s.Get();
            ManagementObject hit = null;
            foreach (ManagementObject mo in col)
            {
                hit = mo;
                break;
            }
            s.Dispose();
            return hit;
        }

        public static void RestartAdapter(AdapterInfo a, Action<string> log)
        {
            if (a == null || a.WmiIndex < 0)
                throw new ApplicationException("Adapter cannot be restarted (no WMI device index).");

            ManagementObject mo = null;
            try
            {
                if (log != null) log("Restarting adapter: disabling...");
                mo = FindByIndex(a.WmiIndex);
                if (mo == null) throw new ApplicationException("Adapter no longer present.");
                mo.InvokeMethod("Disable", null);

                WaitFor(delegate { return AdapterEnabled(a); }, false, 15000);
                if (log != null) log("Adapter disabled. Re-enabling...");
            }
            finally
            {
                if (mo != null) mo.Dispose();
            }

            ManagementObject mo2 = null;
            try
            {
                mo2 = FindByIndex(a.WmiIndex);
                if (mo2 == null)
                {
                    for (int i = 0; i < 5 && mo2 == null; i++)
                    {
                        System.Threading.Thread.Sleep(400);
                        mo2 = FindByIndex(a.WmiIndex);
                    }
                }
                if (mo2 == null) throw new ApplicationException("Adapter did not come back after disabling.");
                mo2.InvokeMethod("Enable", null);
            }
            finally
            {
                if (mo2 != null) mo2.Dispose();
            }

            if (log != null) log("Waiting for the adapter to finish starting...");
            bool ready = false;
            for (int i = 0; i < 60; i++)
            {
                System.Threading.Thread.Sleep(500);
                if (AdapterEnabled(a) && adapterHasMac(a)) { ready = true; break; }
            }
            if (!ready && log != null) log("Adapter is slow to start; continuing anyway.");
            if (log != null) log("Adapter restarted.");
        }

        private static bool AdapterEnabled(AdapterInfo a)
        {
            AdapterState st = LoadAdapterState(a);
            if (st != null) return st.AdminUp;

            ManagementObject mo = null;
            try
            {
                mo = FindByIndex(a.WmiIndex);
                if (mo == null) return false;
                object e = mo["NetEnabled"];
                if (e is bool) return (bool)e;
                return true;
            }
            finally { if (mo != null) mo.Dispose(); }
        }

        private static bool adapterHasMac(AdapterInfo a)
        {
            ManagementObject mo = null;
            try
            {
                mo = FindByIndex(a.WmiIndex);
                if (mo == null) return false;
                object m = mo["MACAddress"];
                return m is string && !string.IsNullOrEmpty((string)m);
            }
            finally { if (mo != null) mo.Dispose(); }
        }

        private static void WaitFor(Func<bool> condition, bool target, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (condition() == target) return;
                System.Threading.Thread.Sleep(250);
                waited += 250;
            }
        }
    }
}
