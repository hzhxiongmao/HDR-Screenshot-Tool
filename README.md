# HDR Screenshot Tool

A Windows desktop screenshot tool that properly captures **HDR monitor content** using the Windows Graphics Capture API, with annotation tools and pin-to-desktop capability.

![](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![](https://img.shields.io/badge/.NET-10.0-purple)
![](https://img.shields.io/badge/language-C%23%20%2B%20C%2B%2B-orange)

## Features

- **True HDR capture** — Captures HDR display output via Direct3D 11 + WGC, with Reinhard tone mapping to SDR
- **Region selection** — Drag to select any screen region; resize handles for fine-tuning
- **Annotation toolbar** — Freehand drawing on the selected area before saving
- **Pin to desktop** — Pin screenshots as borderless, always-on-top windows; drag to move, Ctrl+scroll to scale
- **Global hotkey** — Configurable keyboard shortcut (default: `Ctrl+Shift+S`)
- **Image adjustments** — Contrast, saturation, brightness, gamma sliders with real-time preview
- **System tray** — Minimizes to tray, runs in background
- **Auto-start** — Optional "Start with Windows"
- **Bilingual UI** — English / Chinese (中文) toggle
- **Configurable output** — Choose save folder, toggle file save and clipboard copy independently

## Screenshots

*[Add screenshots here]*

## Prerequisites

### To run
- Windows 10 version 19041 (20H1) or later
- .NET 10.0 Desktop Runtime

### To build
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with:
  - **Desktop development with C++** workload
  - **C++/WinRT** component (install via Visual Studio Installer → Individual components → "C++/WinRT")
- Windows 10 SDK (10.0.19041.0 or later)

## Build

### 1. Build the native capture DLL

Open `NativeCapture/NativeCapture.vcxproj` in Visual Studio 2022 and build in **Release | x64** configuration.

Or via command line:
```powershell
msbuild NativeCapture/NativeCapture.vcxproj /p:Configuration=Release /p:Platform=x64
```

Copy `NativeCapture.dll` to the `App/` directory:
```powershell
copy NativeCapture\x64\Release\NativeCapture.dll App\
```

### 2. Build the WPF application

```powershell
cd App
dotnet build -c Release
```

The output will be in `App/bin/Release/net10.0-windows10.0.19041.0/`.

### 3. Run

```powershell
dotnet run -c Release
```

## Project Structure

```
HDRScreenshotTool/
├── App/                          # WPF application (.NET 10)
│   ├── MainWindow.xaml/.cs       # Main settings window + capture flow
│   ├── CaptureOverlay.xaml/.cs   # Fullscreen region selection + toolbar + drawing
│   ├── PinnedImageWindow.xaml/.cs # Borderless pinned image window
│   ├── NativeCaptureService.cs   # P/Invoke bridge to C++ DLL
│   ├── AppSettings.cs            # JSON settings persistence
│   ├── TrayIconFactory.cs        # System tray icon
│   └── Loc.cs                    # EN/ZH localization
├── NativeCapture/                # C++ native capture engine
│   ├── CaptureEngine.h/.cpp      # WGC + D3D11 HDR capture
│   └── dllmain.cpp               # DLL entry point
└── README.md
```

## How It Works

1. **HDR Capture**: The C++ engine uses `Windows.Graphics.Capture` to grab frames in `R16G16B16A16_Float` format, preserving HDR color data. It then applies Reinhard tone mapping + linear-to-sRGB conversion to produce viewable 8-bit images.

2. **Fallback**: If the native WGC engine fails to initialize (e.g., no supported GPU), the app falls back to GDI+ `CopyFromScreen` (SDR only).

## License

MIT License — see [LICENSE](LICENSE) for details.

## Author

XiongMao
