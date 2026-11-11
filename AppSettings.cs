using System.Text.Json;
using Microsoft.Win32;

namespace NiyazisugarItemci;

internal sealed class AppSettings
{
    public string LastQuickListingServer { get; set; } = ""; public bool AutoFreeBumpEnabled { get; set; } = true; public bool NotificationsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public int ReminderMinutes { get; set; } = 5;
    public bool StartWithWindows { get; set; } = false;
    public bool SidebarCollapsed { get; set; } = false;
    public string OwnStoreUrl { get; set; } = "";
    public bool AdBlockEnabled { get; set; } = true;
    public bool AutoUpdateCheck { get; set; } = true;

    private static string FolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NiyazisugarItemci"
        );

    private static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })
            );
        }
        catch
        {
            // Ayar kaydı başarısız olsa da uygulama çalışmaya devam etsin.
        }
    }

    public static void SetStartup(bool enabled)
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "NiyazisugarItemci";

        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key is null) return;

        if (enabled)
        {
            key.SetValue(valueName, $"\"{Application.ExecutablePath}\" --tray");
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}

