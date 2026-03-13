namespace WiFiCast.Services;

/// <summary>Windows WLAN API P/Invoke로 주변 Wi-Fi 네트워크를 스캔합니다.</summary>
public static class WlanScanner
{
    // ── WLAN API P/Invoke ─────────────────────────────────────────────

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, nint pReserved, out uint pdwNegotiatedVersion, out nint phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(nint hClientHandle, nint pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(nint hClientHandle, nint pReserved, out nint ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetNetworkBssList(nint hClientHandle, ref Guid pInterfaceGuid,
        nint pDot11Ssid, int dot11BssType, bool bSecurityEnabled, nint pReserved, out nint ppWlanBssList);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(nint pMemory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public uint isState;
    }

    // ── 오프셋 상수 (WLAN_BSS_ENTRY 레이아웃) ──────────────────────────
    // dot11Ssid       :  0 (4+32 = 36 bytes)
    // uPhyId          : 36 (4 bytes)
    // dot11Bssid      : 40 (6 bytes)  → +2 pad
    // dot11BssType    : 48 (4 bytes)
    // dot11BssPhyType : 52 (4 bytes)
    // lRssi           : 56 (4 bytes)
    // uLinkQuality    : 60 (4 bytes)
    // bInRegDomain    : 64 (1 byte)   → +1 pad
    // usBeaconPeriod  : 66 (2 bytes)  → +4 pad
    // ullTimestamp    : 72 (8 bytes)
    // ullHostTimestamp: 80 (8 bytes)
    // usCapability    : 88 (2 bytes)  → +2 pad
    // ulChCenterFreq  : 92 (4 bytes, kHz)
    // wlanRateSet     : 96 (504 bytes)
    // ulIeOffset      :600 (4 bytes)
    // ulIeSize        :604 (4 bytes)
    private const int OFF_SSID_LEN    = 0;
    private const int OFF_SSID_DATA   = 4;
    private const int OFF_BSSID       = 40;
    private const int OFF_RSSI        = 56;
    private const int OFF_LINK_QUAL   = 60;
    private const int OFF_FREQ_KHZ    = 92;
    private const int OFF_IE_OFFSET   = 600;
    private const int OFF_IE_SIZE     = 604;
    private const int FIXED_ENTRY_SZ  = 608;

    // ── 공개 API ──────────────────────────────────────────────────────

    public static List<WifiNetwork> Scan()
    {
        var results = new List<WifiNetwork>();

        if (WlanOpenHandle(2, nint.Zero, out _, out nint client) != 0)
            return results;

        try
        {
            if (WlanEnumInterfaces(client, nint.Zero, out nint ifList) != 0)
                return results;

            try
            {
                int count = Marshal.ReadInt32(ifList, 4); // dwNumberOfItems
                nint ifEntry = ifList + 8;
                int ifInfoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                for (int i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(ifEntry + i * ifInfoSize);
                    Guid guid = info.InterfaceGuid;

                    if (WlanGetNetworkBssList(client, ref guid, nint.Zero,
                            3 /*dot11_BSS_type_any*/, false, nint.Zero, out nint bssList) == 0)
                    {
                        try   { ParseBssList(bssList, results); }
                        finally { WlanFreeMemory(bssList); }
                    }
                }
            }
            finally { WlanFreeMemory(ifList); }
        }
        finally { WlanCloseHandle(client, nint.Zero); }

        // 중복 BSSID 제거 (신호 강도 높은 것 유지)
        return results
            .GroupBy(n => n.Bssid)
            .Select(g => g.OrderByDescending(n => n.Signal).First())
            .OrderByDescending(n => n.Signal)
            .ToList();
    }

    // ── 내부 파서 ─────────────────────────────────────────────────────

    private static void ParseBssList(nint ptr, List<WifiNetwork> results)
    {
        int numEntries = Marshal.ReadInt32(ptr, 4);
        nint entry = ptr + 8;

        for (int i = 0; i < numEntries; i++)
        {
            try
            {
                // SSID
                int ssidLen = Math.Clamp(Marshal.ReadInt32(entry, OFF_SSID_LEN), 0, 32);
                byte[] ssidBytes = new byte[ssidLen];
                if (ssidLen > 0) Marshal.Copy(entry + OFF_SSID_DATA, ssidBytes, 0, ssidLen);
                string ssid = Encoding.UTF8.GetString(ssidBytes).TrimEnd('\0');

                // BSSID
                byte[] mac = new byte[6];
                Marshal.Copy(entry + OFF_BSSID, mac, 0, 6);
                string bssid = string.Join(":", mac.Select(b => b.ToString("X2")));

                // Signal
                int rssi        = Marshal.ReadInt32(entry, OFF_RSSI);
                int linkQuality = Marshal.ReadInt32(entry, OFF_LINK_QUAL);

                // Frequency → Channel + Band
                uint freqKhz = (uint)Marshal.ReadInt32(entry, OFF_FREQ_KHZ);
                int  freqMhz = (int)(freqKhz / 1000);
                int  channel;
                string band;

                if (freqMhz is >= 2400 and < 2500)
                {
                    channel = (freqMhz - 2407) / 5;
                    band = "2.4GHz";
                }
                else if (freqMhz is >= 4900 and < 6000)
                {
                    channel = (freqMhz - 5000) / 5;
                    band = "5GHz";
                }
                else
                {
                    channel = freqMhz;
                    band = "Unknown";
                }

                results.Add(new WifiNetwork
                {
                    Ssid    = ssid.Length > 0 ? ssid : "(hidden)",
                    Bssid   = bssid,
                    Signal  = Math.Clamp(linkQuality, 0, 100),
                    Rssi    = rssi,
                    Channel = channel,
                    Band    = band,
                    FreqMhz = freqMhz,
                });

                // 다음 엔트리로 이동
                uint ieOffset = (uint)Marshal.ReadInt32(entry, OFF_IE_OFFSET);
                uint ieSize   = (uint)Marshal.ReadInt32(entry, OFF_IE_SIZE);
                int  entrySize = (int)(Math.Max(ieOffset, FIXED_ENTRY_SZ) + ieSize);
                entrySize = (entrySize + 7) & ~7;   // 8-byte align
                if (entrySize < FIXED_ENTRY_SZ) entrySize = FIXED_ENTRY_SZ;
                entry += entrySize;
            }
            catch { break; }
        }
    }
}
