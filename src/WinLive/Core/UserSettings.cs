using System.Text.Json;

namespace WinLive.Core;

public static class UserSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLive", "settings.json");

    public static bool LoadMuted()
    {
        return Load().Muted;
    }

    public static void SaveMuted(bool muted)
    {
        var settings = Load();
        Save(settings with { Muted = muted });
    }

    public static string? LoadLastFolder() => Load().LastFolder;

    public static void SaveLastFolder(string path)
    {
        var settings = Load();
        Save(settings with { LastFolder = path });
    }

    public static double LoadSlideshowSeconds() => Math.Clamp(Load().SlideshowSeconds, 2, 15);

    public static void SaveSlideshowSeconds(double seconds)
    {
        var settings = Load();
        Save(settings with { SlideshowSeconds = Math.Clamp(seconds, 2, 15) });
    }

    public static string LoadTheme() => Load().Theme;

    public static void SaveTheme(string theme)
    {
        var settings = Load();
        Save(settings with { Theme = theme });
    }

    public static string LoadLanguage() => Load().Language;

    public static void SaveLanguage(string language)
    {
        var settings = Load();
        Save(settings with { Language = language });
    }

    private static Settings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings()
                : new Settings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    private static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record Settings(bool Muted = false, string? LastFolder = null, double SlideshowSeconds = 3, string Theme = "System", string Language = "System");
}
