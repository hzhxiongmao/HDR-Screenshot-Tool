namespace HDRScreenshotTool;

public static class Loc
{
    private static readonly Dictionary<string, (string en, string zh)> _dict = new()
    {
        ["Title"] = ("HDR Screenshot", "HDR 截图工具"),
        ["CaptureBtn"] = ("Capture Region", "框选截图"),
        ["HotkeyGroup"] = ("Hotkey", "快捷键"),
        ["AdjustGroup"] = ("Adjustments", "图像调整"),
        ["Contrast"] = ("Contrast", "对比度"),
        ["Saturation"] = ("Saturation", "饱和度"),
        ["Brightness"] = ("Brightness", "亮度"),
        ["Gamma"] = ("Gamma", "伽马"),
        ["Reset"] = ("Reset", "重置"),
        ["Startup"] = ("Start with Windows", "开机自动启动"),
        ["SetHotkey"] = ("Set", "设置"),
        ["HotkeyHint"] = ("Click Set then press key combo", "点击设置后按下组合键"),
        ["HotkeyPrompt"] = ("Press keys...", "请按键..."),
        ["HotkeySet"] = ("Hotkey set", "已设置"),
        ["Language"] = ("Language", "语言"),
        ["StatusReady"] = ("Ready", "就绪"),
        ["Capturing"] = ("Capturing...", "截图中..."),
        ["Saved"] = ("saved", "已保存"),
        ["Pinned"] = ("Pinned", "已钉图"),
        ["Error"] = ("Error", "错误"),
        ["TrayCapture"] = ("Capture Region", "框选截图"),
        ["TrayShow"] = ("Show Window", "显示窗口"),
        ["TrayExit"] = ("Exit", "退出"),
        ["PinBtn"] = ("📌", "📌"),
        ["SaveBtn"] = ("✔", "✔"),
        ["DrawBtn"] = ("✎", "✎"),
        ["CancelBtn"] = ("✘", "✘"),
        ["TrayTip"] = ("HDR Screenshot Tool", "HDR 截图工具"),
        ["SaveFolder"] = ("Save to folder", "保存到文件夹"),
        ["Browse"] = ("Browse...", "浏览..."),
        ["SaveToFile"] = ("Save to file", "保存到文件"),
        ["CopyToClipboard"] = ("Copy to clipboard", "复制到剪贴板"),
    };

    public static string Lang { get; set; } = "en";

    public static string Get(string key)
    {
        if (_dict.TryGetValue(key, out var t))
            return Lang == "zh" ? t.zh : t.en;
        return key;
    }

    public static string Fmt(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }
}
