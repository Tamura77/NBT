using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// ─── PRESETS ────────────────────────────────────────────────────────────────
// Gamma:      1.0 = normal | >1.0 lifts dark areas
// Brightness: 0.0 = normal | positive = brighter
// Contrast:   1.0 = normal | below 1.0 prevents highlights blowing out
record Profile(string Name, double Gamma, double Brightness, double Contrast);
static class Profiles
{
    public static readonly Profile L1 = new("Level 1", 1.00, 0.00, 1.00);
    public static readonly Profile L2 = new("Level 2", 1.30, 0.05, 0.97);
    public static readonly Profile L3 = new("Level 3", 1.50, 0.09, 0.94);
    public static readonly Profile L4 = new("Level 4", 1.65, 0.12, 0.92);
    public static readonly Profile L5 = new("Level 5", 1.80, 0.16, 0.90);
}

// ─── WIN32 STRUCTS ───────────────────────────────────────────────────────────
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DEVMODE
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public uint   dmFields;
    public int    dmPositionX, dmPositionY;
    public uint   dmDisplayOrientation, dmDisplayFixedOutput;
    public short  dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public ushort dmLogPixels;
    public uint   dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    public uint   dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
    public uint   dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
struct MONITORINFOEX
{
    public int  cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;
}

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int left, top, right, bottom; }

[StructLayout(LayoutKind.Sequential)]
struct GammaRamp
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Red;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Green;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Blue;
}

delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

// ─── DISPLAYCONFIG STRUCTS ───────────────────────────────────────────────────
[StructLayout(LayoutKind.Sequential)]
struct LUID { public uint LowPart; public int HighPart; }

[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

// 20 bytes
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public LUID adapterId;   // 8
    public uint id;           // 4
    public uint modeInfoIdx;  // 4 (union: modeInfoIdx / cloneGroupId+sourceModeInfoIdx)
    public uint statusFlags;  // 4
}

// 48 bytes
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public LUID                   adapterId;       // 8
    public uint                   id;               // 4
    public uint                   modeInfoIdx;      // 4 (union)
    public int                    outputTechnology; // 4
    public uint                   rotation;         // 4
    public uint                   scaling;          // 4
    public DISPLAYCONFIG_RATIONAL refreshRate;      // 8
    public uint                   scanLineOrdering; // 4
    public int                    targetAvailable;  // 4
    public uint                   statusFlags;      // 4
}

// 72 bytes
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; // 20
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; // 48
    public uint flags;                                 // 4
}

// 64 bytes — union (target/source/desktop mode info, max 48 bytes) stored opaquely
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_MODE_INFO
{
    public uint infoType;  // 4
    public uint id;         // 4
    public LUID adapterId;  // 8
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] modeData; // 48
}

// 84 bytes
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public int  type;       // 4  (DISPLAYCONFIG_DEVICE_INFO_TYPE)
    public uint size;       // 4
    public LUID adapterId;  // 8
    public uint id;         // 4
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName; // 64
}

static class Win32
{
    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr CreateDC(string? drv, string dev, string? output, IntPtr init);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool SetDeviceGammaRamp(IntPtr hDC, ref GammaRamp ramp);

    [DllImport("gdi32.dll")]
    public static extern bool GetDeviceGammaRamp(IntPtr hDC, ref GammaRamp ramp);

    [DllImport("powrprof.dll")]
    public static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll")]
    public static extern uint PowerGetActiveScheme(IntPtr RootPowerKey, out IntPtr SchemeGuidPtr);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);

    public const int  ENUM_CURRENT_SETTINGS  = -1;
    public const int  ENUM_REGISTRY_SETTINGS = -2;
    public const uint CDS_UPDATEREGISTRY     = 0x00000001;
    public const uint CDS_NORESET            = 0x10000000;
    public const uint DM_POSITION            = 0x00000020;
    public const uint DM_PELSWIDTH           = 0x00080000;
    public const uint DM_PELSHEIGHT          = 0x00100000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettingsW(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsExW(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsExW(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    // SDC flags for SetDisplayConfig
    public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    public const uint SDC_TOPOLOGY_EXTEND             = 0x00000004;
    public const uint SDC_APPLY                       = 0x00000080;
    public const uint SDC_SAVE_TO_DATABASE            = 0x00000200;
    public const uint SDC_ALLOW_CHANGES               = 0x00000400;
    public const uint SDC_FORCE_MODE_ENUMERATION      = 0x00001000;

    // Overload used for zero-array restore calls
    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(uint numPathArrayElements, IntPtr pathArray, uint numModeInfoArrayElements, IntPtr modeInfoArray, uint flags);

    // Overload used when supplying path/mode arrays
    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[] pathArray, uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, uint flags);

    // QueryDisplayConfig APIs
    public const uint QDC_ONLY_ACTIVE_PATHS                    = 0x00000002;
    public const int  DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
}

// ─── MAIN ────────────────────────────────────────────────────────────────────
static class Program
{
    static readonly Guid PlanPerformance = new("d0d2696b-417a-4cc2-8f70-abb4cffc9c89"); // Ultimate Performance
    static readonly Guid PlanBalanced    = new("381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
    static readonly Guid PlanEco         = new("a1841308-3541-4fab-bc81-f71556f20b4a"); // Power Saver

    static StreamWriter? _log;

    static void Log(string msg = "")
    {
        Console.WriteLine(msg);
        _log?.WriteLine(msg);
    }

    static string _stateFile = "";
    static string _logDir    = "";

    // slot 0 = leftmost non-primary, slot 1 = rightmost non-primary
    static bool[]   _monDisabled = new bool[2];
    static string[] _monDevice   = new string[2] { "", "" };

    static DISPLAYCONFIG_PATH_INFO[]       _savedPaths    = [];
    static DISPLAYCONFIG_MODE_INFO[]       _savedModes    = [];
    static (int h, uint l, uint id)?[]     _disabledMonKey = new (int, uint, uint)?[2];

    static void Main()
    {
        string logDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayToggle");
        string logFile = Path.Combine(logDir, "toggle.log");
        Directory.CreateDirectory(logDir);

        using var logWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
        _log = logWriter;
        _logDir    = logDir;
        _stateFile = Path.Combine(logDir, "state.txt");

        Log($"Log      : {logFile}");

        string saved = File.Exists(_stateFile) ? File.ReadAllText(_stateFile).Trim() : "1";
        var initial = saved switch { "2" => Profiles.L2, "3" => Profiles.L3, "4" => Profiles.L4, "5" => Profiles.L5, _ => Profiles.L1 };
        ApplyProfile(initial, saved);
        Log($"Power    : {GetActivePlanName()}");
        LoadPersistedMonitorStates();
        Log();
        PrintMenu();

        while (true)
        {
            Console.Write("> ");
            var key = Console.ReadKey(intercept: true).Key;
            Console.WriteLine();

            Profile? p = key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => Profiles.L1,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => Profiles.L2,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => Profiles.L3,
                ConsoleKey.D4 or ConsoleKey.NumPad4 => Profiles.L4,
                ConsoleKey.D5 or ConsoleKey.NumPad5 => Profiles.L5,
                _ => null
            };

            if      (key == ConsoleKey.Q) { Log("Quit."); break; }
            else if (key == ConsoleKey.P) { SetPowerPlan(PlanPerformance, "Ultimate Performance"); PrintMenu(); }
            else if (key == ConsoleKey.B) { SetPowerPlan(PlanBalanced,    "Balanced");             PrintMenu(); }
            else if (key == ConsoleKey.E) { SetPowerPlan(PlanEco,         "Eco (Power Saver)");    PrintMenu(); }
            else if (key == ConsoleKey.L) { ToggleMonitor(0);                                      PrintMenu(); }
            else if (key == ConsoleKey.R) { ToggleMonitor(1);                                      PrintMenu(); }
            else if (key == ConsoleKey.X) { ForceRestoreAllMonitors();                             PrintMenu(); }
            else if (p is not null)       { ApplyProfile(p, p.Name[^1..]); PrintMenu(); }
            else                          { PrintMenu(); }
        }
    }

    static string WinErr(int code) =>
        code == 0 ? "OK" : $"{code} ({new System.ComponentModel.Win32Exception(code).Message})";

    static void PrintMenu()
    {
        string lLabel = _monDisabled[0] ? "L = Left  [OFF]" : "L = Left  [on]";
        string rLabel = _monDisabled[1] ? "R = Right [OFF]" : "R = Right [on]";
        Console.WriteLine($"  1–5 = Brightness   P/B/E = Power Plan   {lLabel}   {rLabel}   X = Restore all   Q = Quit");
    }

    // slot 0 = leftmost non-primary (most-negative x), slot 1 = rightmost non-primary (most-positive x)
    static void ToggleMonitor(int slot)
    {
        string label = slot == 0 ? "Left" : "Right";

        if (!_monDisabled[slot])
        {
            var nonPrimary = GetMonitors()
                .Where(m => (m.flags & 1) == 0)
                .OrderBy(m => m.x)
                .ToList();

            if (nonPrimary.Count <= slot) { Log($"Monitor  : {label} — no display found at that position"); return; }

            string device = nonPrimary[slot].device;
            _monDevice[slot] = device;

            if (DisableMonitorByDevice(device, slot))
            {
                _monDisabled[slot] = true;
                SaveMonitorState(slot);
                Log($"Monitor  : {label} ({device}) disabled");
            }
            else
            {
                _monDevice[slot] = "";
                Log($"Monitor  : {label} disable failed — try running as administrator");
            }
        }
        else
        {
            // Monitor may have come back externally (KVM switch, Windows re-detection)
            if (!string.IsNullOrEmpty(_monDevice[slot]) &&
                GetMonitors().Any(m => string.Equals(m.device, _monDevice[slot], StringComparison.OrdinalIgnoreCase)))
            {
                _monDisabled[slot] = false;
                ClearMonitorState(slot);
                _disabledMonKey[slot] = null;
                _monDevice[slot] = "";
                Log($"Monitor  : {label} — already active (re-detected externally)");
            }
            else if (EnableMonitorByDevice(slot))
            {
                _monDisabled[slot] = false;
                ClearMonitorState(slot);
                _monDevice[slot] = "";
                Log($"Monitor  : {label} re-enabled");
            }
            else
            {
                Log($"Monitor  : {label} re-enable failed — try X to force restore all");
            }
        }
    }

    // Clears the DISPLAYCONFIG_PATH_ACTIVE flag on the target path and reapplies the full
    // topology. Caches the snapshot so re-enable can set the flag back without QDC_ALL_PATHS.
    static bool DisableMonitorByDevice(string targetDevice, int slot)
    {
        int err = Win32.GetDisplayConfigBufferSizes(Win32.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
        if (err != 0) { Log($"Monitor  : GetDisplayConfigBufferSizes {WinErr(err)}"); return false; }

        var freshPaths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var freshModes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        err = Win32.QueryDisplayConfig(Win32.QDC_ONLY_ACTIVE_PATHS, ref pathCount, freshPaths, ref modeCount, freshModes, IntPtr.Zero);
        if (err != 0) { Log($"Monitor  : QueryDisplayConfig {WinErr(err)}"); return false; }

        Array.Resize(ref freshPaths, (int)pathCount);
        Array.Resize(ref freshModes, (int)modeCount);

        // If we already have a larger saved snapshot (another monitor was disabled earlier),
        // use it as the base and sync active flags from the fresh query.
        DISPLAYCONFIG_PATH_INFO[] workPaths;
        DISPLAYCONFIG_MODE_INFO[] workModes;
        if (_savedPaths.Length > freshPaths.Length)
        {
            workPaths = (DISPLAYCONFIG_PATH_INFO[])_savedPaths.Clone();
            workModes = _savedModes;
            for (int i = 0; i < workPaths.Length; i++)
            {
                bool active = Array.Exists(freshPaths, p =>
                    p.sourceInfo.adapterId.HighPart == workPaths[i].sourceInfo.adapterId.HighPart &&
                    p.sourceInfo.adapterId.LowPart  == workPaths[i].sourceInfo.adapterId.LowPart  &&
                    p.sourceInfo.id                 == workPaths[i].sourceInfo.id);
                if (active) workPaths[i].flags |=  0x1u;
                else        workPaths[i].flags &= ~0x1u;
            }
        }
        else
        {
            workPaths = freshPaths;
            workModes = freshModes;
        }

        int targetIdx = -1;
        for (int i = 0; i < workPaths.Length; i++)
        {
            var req = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                type      = Win32.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                size      = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                adapterId = workPaths[i].sourceInfo.adapterId,
                id        = workPaths[i].sourceInfo.id,
            };
            Win32.DisplayConfigGetDeviceInfo(ref req);
            if (string.Equals(req.viewGdiDeviceName, targetDevice, StringComparison.OrdinalIgnoreCase))
            { targetIdx = i; break; }
        }

        if (targetIdx < 0) { Log($"Monitor  : {targetDevice} not found"); return false; }
        if (workPaths.Count(p => (p.flags & 0x1u) != 0) <= 1) { Log("Monitor  : cannot disable the only active display"); return false; }

        _disabledMonKey[slot] = (workPaths[targetIdx].sourceInfo.adapterId.HighPart,
                                  workPaths[targetIdx].sourceInfo.adapterId.LowPart,
                                  workPaths[targetIdx].sourceInfo.id);
        workPaths[targetIdx].flags &= ~0x1u;
        _savedPaths = workPaths;
        _savedModes = workModes;

        err = Win32.SetDisplayConfig((uint)workPaths.Length, workPaths, (uint)workModes.Length, workModes,
            Win32.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Win32.SDC_APPLY);
        if (err != 0)
            err = Win32.SetDisplayConfig((uint)workPaths.Length, workPaths, (uint)workModes.Length, workModes,
                Win32.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Win32.SDC_APPLY | Win32.SDC_ALLOW_CHANGES);

        Log($"Monitor  : SetDisplayConfig disable {WinErr(err)}");
        return err == 0;
    }

    // Sets the DISPLAYCONFIG_PATH_ACTIVE flag back on the cached path and reapplies.
    static bool EnableMonitorByDevice(int slot)
    {
        var key = _disabledMonKey[slot];
        Log($"[DBG] Enable slot={slot} key={key} savedPaths={_savedPaths.Length} savedModes={_savedModes.Length}");

        if (key is null || _savedPaths.Length == 0)
        {
            Log("[DBG] No key/snapshot — falling back to SDC_TOPOLOGY_EXTEND");
            int r = Win32.SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero,
                Win32.SDC_TOPOLOGY_EXTEND | Win32.SDC_ALLOW_CHANGES | Win32.SDC_SAVE_TO_DATABASE | Win32.SDC_APPLY);
            if (r == 0) { for (int s = 0; s < 2; s++) { _monDisabled[s] = false; ClearMonitorState(s); _monDevice[s] = ""; } }
            Log($"Monitor  : extend fallback {WinErr(r)}");
            return r == 0;
        }

        var paths = (DISPLAYCONFIG_PATH_INFO[])_savedPaths.Clone();
        bool found = false;
        for (int i = 0; i < paths.Length; i++)
        {
            Log($"[DBG] path[{i}] adapterH={paths[i].sourceInfo.adapterId.HighPart} adapterL={paths[i].sourceInfo.adapterId.LowPart} srcId={paths[i].sourceInfo.id} flags=0x{paths[i].flags:X}");
            if (paths[i].sourceInfo.adapterId.HighPart == key.Value.h &&
                paths[i].sourceInfo.adapterId.LowPart  == key.Value.l &&
                paths[i].sourceInfo.id                 == key.Value.id)
            {
                Log($"[DBG] Found target path[{i}] flags before=0x{paths[i].flags:X}");
                paths[i].flags |= 0x1u;
                Log($"[DBG] flags after=0x{paths[i].flags:X}");
                found = true;
                break;
            }
        }

        if (!found) { Log("[DBG] Path not found in saved topology — key didn't match any path"); return false; }

        Log($"[DBG] Calling SetDisplayConfig paths={paths.Length} modes={_savedModes.Length}");
        int err = Win32.SetDisplayConfig((uint)paths.Length, paths, (uint)_savedModes.Length, _savedModes,
            Win32.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Win32.SDC_APPLY | Win32.SDC_SAVE_TO_DATABASE);
        Log($"[DBG] First attempt: {WinErr(err)}");
        if (err != 0)
        {
            err = Win32.SetDisplayConfig((uint)paths.Length, paths, (uint)_savedModes.Length, _savedModes,
                Win32.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Win32.SDC_APPLY | Win32.SDC_SAVE_TO_DATABASE | Win32.SDC_ALLOW_CHANGES);
            Log($"[DBG] Second attempt (ALLOW_CHANGES): {WinErr(err)}");
        }

        // Verify the display actually became active
        if (err == 0)
        {
            Win32.GetDisplayConfigBufferSizes(Win32.QDC_ONLY_ACTIVE_PATHS, out uint pc, out uint mc);
            var vp = new DISPLAYCONFIG_PATH_INFO[pc]; var vm = new DISPLAYCONFIG_MODE_INFO[mc];
            Win32.QueryDisplayConfig(Win32.QDC_ONLY_ACTIVE_PATHS, ref pc, vp, ref mc, vm, IntPtr.Zero);
            Log($"[DBG] Active paths after enable: {pc}");
            for (int i = 0; i < pc; i++)
            {
                var req = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    type = Win32.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = vp[i].sourceInfo.adapterId,
                    id = vp[i].sourceInfo.id,
                };
                Win32.DisplayConfigGetDeviceInfo(ref req);
                Log($"[DBG]   active[{i}] = {req.viewGdiDeviceName} flags=0x{vp[i].flags:X}");
            }
        }

        Log($"Monitor  : SetDisplayConfig enable {WinErr(err)}");
        if (err == 0) _disabledMonKey[slot] = null;
        return err == 0;
    }

    static void ForceRestoreAllMonitors()
    {
        uint flags = Win32.SDC_TOPOLOGY_EXTEND | Win32.SDC_FORCE_MODE_ENUMERATION | Win32.SDC_SAVE_TO_DATABASE | Win32.SDC_APPLY;
        int r = Win32.SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, flags);
        Log($"Monitor  : SetDisplayConfig restore {WinErr(r)}");

        for (int s = 0; s < 2; s++) { _monDisabled[s] = false; ClearMonitorState(s); _monDevice[s] = ""; }

        if (r == 0) Log("Monitor  : all displays restored to extended layout");
        else        Log("Monitor  : restore failed — try rebooting or unplugging/replugging the cable");
    }

    static string MonitorStateFile(int slot) => Path.Combine(_logDir, $"monitor_{slot}.txt");

    static void SaveMonitorState(int slot)
    {
        File.WriteAllText(MonitorStateFile(slot), _monDevice[slot]);
    }

    static void ClearMonitorState(int slot)
    {
        string f = MonitorStateFile(slot);
        if (File.Exists(f)) File.Delete(f);
    }

    static void LoadPersistedMonitorStates()
    {
        var active = GetMonitors().Select(m => m.device).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int slot = 0; slot < 2; slot++)
        {
            string f = MonitorStateFile(slot);
            if (!File.Exists(f)) continue;
            string device = File.ReadAllText(f).Trim();
            if (string.IsNullOrEmpty(device) || active.Contains(device)) { File.Delete(f); continue; }
            _monDevice[slot]   = device;
            _monDisabled[slot] = true;
            Log($"Monitor  : {(slot == 0 ? "Left" : "Right")} ({device}) is still disabled from last session");
        }
    }

    static void ApplyProfile(Profile profile, string name)
    {
        foreach (var mon in GetMonitors())
        {
            if ((mon.flags & 1) == 0) continue;
            IntPtr dc = Win32.CreateDC(null, mon.device, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) { Log("ERROR: CreateDC failed"); continue; }
            var ramp = BuildRamp(profile);
            Win32.SetDeviceGammaRamp(dc, ref ramp);
            Win32.DeleteDC(dc);
        }
        Log($"Display  : {profile.Name}");
        File.WriteAllText(_stateFile, name);
    }

    static void SetPowerPlan(Guid guid, string name)
    {
        uint result = Win32.PowerSetActiveScheme(IntPtr.Zero, ref guid);
        if (result == 0)
            Log($"Power    : {name}");
        else
            Log($"Power    : FAILED (0x{result:X}) — try running as admin");
    }

    static string GetActivePlanName()
    {
        uint result = Win32.PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr);
        if (result != 0 || ptr == IntPtr.Zero) return "unknown";
        Guid active = Marshal.PtrToStructure<Guid>(ptr);
        Win32.LocalFree(ptr);
        if (active == PlanPerformance) return "Ultimate Performance";
        if (active == PlanBalanced)    return "Balanced";
        if (active == PlanEco)         return "Eco (Power Saver)";
        return active.ToString();
    }

    static List<(string device, int x, int y, int w, int h, uint flags)> GetMonitors()
    {
        var list = new List<(string, int, int, int, int, uint)>();

        bool Callback(IntPtr hMon, IntPtr hdcMon, ref RECT rc, IntPtr data)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (Win32.GetMonitorInfo(hMon, ref info))
                list.Add((
                    info.szDevice,
                    info.rcMonitor.left,
                    info.rcMonitor.top,
                    info.rcMonitor.right  - info.rcMonitor.left,
                    info.rcMonitor.bottom - info.rcMonitor.top,
                    info.dwFlags));
            return true;
        }

        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return list;
    }

    static GammaRamp BuildRamp(Profile p)
    {
        var ramp = MakeRamp();
        for (int i = 0; i < 256; i++)
        {
            double v = Math.Pow(i / 255.0, 1.0 / p.Gamma);
            v = (v - 0.5) * p.Contrast + 0.5;
            v += p.Brightness;
            v = Math.Clamp(v, 0.0, 1.0);
            ushort w = (ushort)(v * 65535);
            ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = w;
        }
        return ramp;
    }

    static GammaRamp MakeRamp() => new()
    {
        Red   = new ushort[256],
        Green = new ushort[256],
        Blue  = new ushort[256],
    };
}
