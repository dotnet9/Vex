using System.Text.Json;
using System.Text.Json.Serialization;
using Vex.Core.Models;

namespace Vex.Core.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private readonly object _syncRoot = new();
    private AppSettings? _settings;

    public AppSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                _settings ??= Load();
                return _settings;
            }
        }
    }

    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        lock (_syncRoot)
        {
            _settings = update(Current);
            Save(_settings);
            return _settings;
        }
    }

    private static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 用户设置保存失败不应打断当前编辑流程，后续可接入统一错误提示。
        }
    }

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeWF",
            "Vex",
            "settings.json");
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
