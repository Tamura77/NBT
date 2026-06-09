# NBT — Nvidia Brightness Toggle

A lightweight Windows console utility that lets you switch between five display brightness/gamma presets in a single keypress, with state persistence across reboots.

Built for NVIDIA GPUs. Uses the Win32 GDI gamma ramp API and [NvAPIWrapper.Net](https://github.com/falahati/NvAPIWrapper) to surface driver and display info.

---

## Features

- Five presets from **Normal** to **Brightest**, toggled with keys `1`–`5`
- Applies only to the **primary monitor**
- Saves the last-used level to disk and restores it automatically on next launch
- Logs every change to `%LOCALAPPDATA%\DisplayToggle\toggle.log`
- Reports NVIDIA driver version, GPU name, and connected display info on startup

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
dotnet publish -c Release -r win-x64 --self-contained true
```

The single-file executable is written to `bin\Release\net8.0\win-x64\DisplayToggle.exe`.

---

## Usage

Run `DisplayToggle.exe` directly — no installer, no admin required.

```
Driver : 572.83 (branch r572_00)
GPU    : NVIDIA GeForce RTX XXXX
NVIDIA outputs (1):
  \\.\DISPLAY1  DVC=50

  1 = Normal   2   3   4   5 = Brightest   Q = Quit
> _
```

Press `1`–`5` to switch presets, `Q` to quit. The selected level persists; next launch restores it automatically.

---

## Troubleshooting

**Ramp applied but display doesn't change**  
The NVIDIA driver is overriding the GDI gamma ramp. In NVIDIA Control Panel go to  
*Display → Adjust desktop color settings* and switch from "Use NVIDIA settings" to **"Use desktop color settings"**.

**"NvAPI: …" error on startup**  
NvAPI info is informational only — the gamma ramp still applies. The error just means the NVIDIA API couldn't be initialised (driver mismatch, no NVIDIA GPU, etc.).

---

## License

MIT
