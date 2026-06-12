# NBT — Nvidia Brightness Toggle

A lightweight Windows console utility for managing display brightness presets, power plans, and monitor on/off state — all in a single keypress.

Built for NVIDIA GPUs. Uses the Win32 GDI gamma ramp API, SetDisplayConfig CCD API, and [NvAPIWrapper.Net](https://github.com/falahati/NvAPIWrapper).

---

## Features

- Five brightness/gamma presets toggled with `1`–`5`
- Power plan switching (`P` / `B` / `E`)
- Disable/re-enable individual non-primary monitors (`L` / `R`)
- Force-restore all monitors (`X`) — useful after a KVM switch or driver glitch
- Brightness level and power plan persist across reboots
- Monitor disable is **session-only** — a reboot always brings all monitors back
- Handles monitors that come back externally (KVM switch, Windows re-detection) without getting stuck
- Logs every action to `%LOCALAPPDATA%\DisplayToggle\toggle.log`

---

## Keys

| Key | Action |
|-----|--------|
| `1`–`5` | Apply brightness preset |
| `P` | Switch to Ultimate Performance power plan |
| `B` | Switch to Balanced power plan |
| `E` | Switch to Eco (Power Saver) power plan |
| `L` | Toggle left non-primary monitor on/off |
| `R` | Toggle right non-primary monitor on/off |
| `X` | Force-restore all monitors to extended layout |
| `Q` | Quit |

Monitor slots are assigned by horizontal position: the leftmost non-primary display is `L`, the rightmost is `R`.

---

## Presets

| Key | Name    | Gamma | Brightness | Contrast |
|-----|---------|-------|------------|----------|
| `1` | Level 1 | 1.00  | +0.00      | 1.00     |
| `2` | Level 2 | 1.30  | +0.05      | 0.97     |
| `3` | Level 3 | 1.50  | +0.09      | 0.94     |
| `4` | Level 4 | 1.65  | +0.12      | 0.92     |
| `5` | Level 5 | 1.80  | +0.16      | 0.90     |

- **Gamma > 1.0** lifts shadow detail (inverse gamma curve)
- **Brightness** shifts the entire output up
- **Contrast < 1.0** pulls back highlights to avoid clipping

---

## Requirements

- Windows 10/11 x64
- NVIDIA GPU with up-to-date drivers
- .NET 8 SDK (build only; the published binary is self-contained)
- NVIDIA Control Panel → **Desktop Color Settings** → set to **"Use desktop color settings"**  
  _(If left on "Use NVIDIA settings", the driver will override the gamma ramp.)_

---

## Build

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

The single-file executable is written to `publish\NBT.exe`.

---

## Usage

Run `NBT.exe` directly or via the desktop shortcut. Administrator rights are recommended for power plan switching and monitor toggling.

```
Log      : C:\Users\...\AppData\Local\DisplayToggle\toggle.log
Display  : Level 1
Power    : Ultimate Performance

  1–5 = Brightness   P/B/E = Power Plan   L = Left  [on]   R = Right [on]   X = Restore all   Q = Quit
> _
```

---

## Monitor toggle behaviour

- **Disable** (`L`/`R`): removes the monitor from the active display topology for the current session only. Does not write to the Windows display config database, so a reboot always recovers all monitors.
- **Re-enable** (`L`/`R` again): restores the monitor and saves the extended layout to the database so it sticks.
- **External re-detection**: if the monitor comes back on its own (e.g. KVM switch returning input, Windows re-enumerating the display), pressing the key again detects it is already active and clears the stale state without issuing a redundant SetDisplayConfig call.
- **Force restore** (`X`): calls `SetDisplayConfig` with `SDC_FORCE_MODE_ENUMERATION` and saves to the database. Use this if re-enable fails or the monitor disappears from Device Manager.

---

## Troubleshooting

**Monitor stuck disabled after reboot / not in Device Manager**  
Run the following in an admin PowerShell, then reboot:
```powershell
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class DisplayFix {
    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(uint n, IntPtr p, uint m, IntPtr q, uint flags);
}
"@
$result = [DisplayFix]::SetDisplayConfig(0, [IntPtr]::Zero, 0, [IntPtr]::Zero, 0x4 -bor 0x80 -bor 0x1000 -bor 0x200)
Write-Host "Result: $result  (0 = success)"
Remove-Item "$env:LOCALAPPDATA\DisplayToggle\monitor_1.txt" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\DisplayToggle\monitor_0.txt" -Force -ErrorAction SilentlyContinue
```

**Ramp applied but display doesn't change**  
The NVIDIA driver is overriding the GDI gamma ramp. In NVIDIA Control Panel go to  
*Display → Adjust desktop color settings* and switch from "Use NVIDIA settings" to **"Use desktop color settings"**.

**"NvAPI: …" error on startup**  
NvAPI info is informational only — brightness still applies. The error means the NVIDIA API couldn't initialise (driver mismatch, no NVIDIA GPU, etc.).

**Power plan change fails**  
Run NBT as administrator.

---

## License

MIT
