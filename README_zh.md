# HDR 截图工具

一款 Windows 桌面截图工具，使用 Windows Graphics Capture API 正确截取 **HDR 显示器画面**，支持涂鸦标注和桌面钉图。

[English](README.md)

![](https://img.shields.io/badge/平台-Windows%2010%2B-blue)
![](https://img.shields.io/badge/框架-.NET%2010.0-purple)
![](https://img.shields.io/badge/语言-C%23%20%2B%20C%2B%2B-orange)

## 功能

- **真 HDR 截图** — 通过 Direct3D 11 + WGC 抓取 HDR 帧缓冲，Reinhard 色调映射转 SDR
- **区域框选** — 拖动选择任意屏幕区域，支持缩放手柄精细调整
- **涂鸦标注** — 截图前可在选区内自由绘制标注
- **桌面钉图** — 将截图以无边框窗口固定在桌面上，支持拖拽移动、Ctrl+滚轮缩放
- **全局热键** — 可自定义快捷键（默认 `Ctrl+Shift+S`）
- **图像调整** — 对比度、饱和度、亮度、伽马滑块实时预览
- **系统托盘** — 最小化到托盘，后台运行
- **开机自启** — 可选"开机自动启动"
- **双语界面** — 中英文一键切换
- **输出设置** — 自定义保存文件夹，独立开关文件保存和剪贴板复制

## 截图

*[在此添加截图]*

## 下载

前往 [Releases](https://github.com/hzhxiongmao/HDR-Screenshot-Tool/releases) 页面，下载 `HDRScreenshotTool_v1.0.zip`，解压后双击 `HDRScreenshotTool.exe` 即可运行。

## 环境要求（从源码构建）

### 运行已打包版本
- Windows 10 19041 (20H1) 及以上，64 位
- 无需安装 .NET（已内置运行时）

### 从源码构建
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/)，需安装 **C++桌面开发** 和 **C++/WinRT** 组件
- Windows 10 SDK (10.0.19041.0 以上)

## 构建

### 1. 编译原生捕获 DLL

```powershell
msbuild NativeCapture/NativeCapture.vcxproj /p:Configuration=Release /p:Platform=x64
copy NativeCapture\x64\Release\NativeCapture.dll App\
```

### 2. 编译 WPF 应用

```powershell
cd App
dotnet build -c Release
```

## 项目结构

```
HDRScreenshotTool/
├── App/                          # WPF 应用 (.NET 10)
│   ├── MainWindow.xaml/.cs       # 主设置窗口 + 截图流程
│   ├── CaptureOverlay.xaml/.cs   # 全屏选区覆盖层 + 工具栏 + 涂鸦
│   ├── PinnedImageWindow.xaml/.cs # 无边框钉图窗口
│   ├── NativeCaptureService.cs   # P/Invoke 桥接 C++ DLL
│   ├── AppSettings.cs            # JSON 设置持久化
│   ├── TrayIconFactory.cs        # 系统托盘图标
│   └── Loc.cs                    # 中英文本地化
├── NativeCapture/                # C++ 原生捕获引擎
│   ├── CaptureEngine.h/.cpp      # WGC + D3D11 HDR 捕获
│   └── dllmain.cpp               # DLL 入口
└── README.md
```

## 工作原理

1. **HDR 捕获**: C++ 引擎用 `Windows.Graphics.Capture` 以 `R16G16B16A16_Float` 格式抓取帧，保留 HDR 色彩数据，再通过 Reinhard 色调映射 + 线性到 sRGB 转换生成可查看的 8-bit 图像。

2. **降级方案**: 如果原生 WGC 引擎初始化失败（如无兼容 GPU），自动降级为 GDI+ `CopyFromScreen`（仅 SDR）。

## 许可证

MIT License — 详见 [LICENSE](LICENSE)。

## 作者

XiongMao
