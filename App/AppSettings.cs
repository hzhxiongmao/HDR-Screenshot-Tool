using System.IO;
using System.Text.Json;

namespace HDRScreenshotTool;

public class AppSettings
{
    public double Contrast { get; set; } = 1.0;
    public double Saturation { get; set; } = 1.0;
    public double Brightness { get; set; } = 0;
    public double Gamma { get; set; } = 1.0;
    public string Hotkey { get; set; } = "Ctrl+Shift+S";
    public bool StartWithWindows { get; set; } = false;

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HDRScreenshotTool", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
