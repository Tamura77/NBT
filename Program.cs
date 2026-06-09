using System;
using System.Collections.Generic;
using System.IO;
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
}

// ─── MAIN ────────────────────────────────────────────────────────────────────
static class Program
{
    static StreamWriter? _log;

    static void Log(string msg = "")
    {
        Console.WriteLine(msg);
        _log?.WriteLine(msg);
    }

    static string _stateFile = "";

    static void Main()
    {
        string logDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayToggle");
        string logFile = Path.Combine(logDir, "toggle.log");
        Directory.CreateDirectory(logDir);

        using var logWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
        _log = logWriter;
        _stateFile = Path.Combine(logDir, "state.txt");

        Log($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] === Display Profile Toggle ===");
        PrintNvidiaInfo();

        // Apply whatever was last saved on launch
        string saved = File.Exists(_stateFile) ? File.ReadAllText(_stateFile).Trim() : "1";
        var initial = saved switch { "2" => Profiles.L2, "3" => Profiles.L3, "4" => Profiles.L4, "5" => Profiles.L5, _ => Profiles.L1 };
        ApplyProfile(initial, saved);

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

            if (key == ConsoleKey.Q) { Log("Quit."); break; }
            else if (p is not null) { ApplyProfile(p, p.Name[^1..]); PrintMenu(); }
            else { PrintMenu(); }
        }
    }

    static void PrintMenu()
    {
        Console.WriteLine("  1 = Normal   2   3   4   5 = Brightest   Q = Quit");
    }

    static void ApplyProfile(Profile profile, string name)
    {
        Log($"[{DateTime.Now:HH:mm:ss}] Switching to: {profile.Name}");
        Log($"  Gamma={profile.Gamma}  Brightness={profile.Brightness:+0.00;-0.00;+0.00}  Contrast={profile.Contrast}");

        var monitors = GetMonitors();
        int applied = 0;
        foreach (var mon in monitors)
        {
            bool primary = (mon.flags & 1) != 0;
            if (!primary) continue;

            Log($"  Monitor: {mon.device}  {mon.w}x{mon.h}  [PRIMARY]");

            IntPtr dc = Win32.CreateDC(null, mon.device, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) { Log("    ERROR: CreateDC failed"); continue; }

            var before = MakeRamp();
            Win32.GetDeviceGammaRamp(dc, ref before);
            ushort midBefore = before.Red[128];

            var ramp = BuildRamp(profile);
            bool ok = Win32.SetDeviceGammaRamp(dc, ref ramp);

            var after = MakeRamp();
            Win32.GetDeviceGammaRamp(dc, ref after);
            ushort midAfter = after.Red[128];

            Win32.DeleteDC(dc);

            bool changed = midAfter != midBefore;
            Log($"    SetDeviceGammaRamp : {(ok ? "OK" : "FAILED")}");
            Log($"    Ramp Red[128]      : before={midBefore}  after={midAfter}  -> {(changed ? "changed OK" : "UNCHANGED - driver overriding")}");

            if (ok && !changed)
            {
                Log("    NOTE: NVCP is resetting the ramp. Try 'Use desktop color settings' in NVCP.");
            }

            if (ok) applied++;
        }

        Log(applied > 0 ? $"  Applied to {applied} monitor(s)." : "  WARNING: No monitors updated.");
        File.WriteAllText(_stateFile, name);
    }

    static void PrintNvidiaInfo()
    {
        try
        {
            NvAPIWrapper.NVIDIA.Initialize();

            Log($"Driver : {NvAPIWrapper.NVIDIA.DriverVersion} (branch {NvAPIWrapper.NVIDIA.DriverBranchVersion})");

            var gpus = NvAPIWrapper.GPU.PhysicalGPU.GetPhysicalGPUs();
            foreach (var gpu in gpus)
                Log($"GPU    : {gpu.FullName}");

            var displays = NvAPIWrapper.Display.Display.GetDisplays();
            Log($"NVIDIA outputs ({displays.Length}):");
            foreach (var d in displays)
                Log($"  {d.Name}  DVC={d.DigitalVibranceControl.CurrentLevel}");
        }
        catch (Exception ex)
        {
            Log($"NvAPI  : {ex.Message}");
        }
        Log();
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
