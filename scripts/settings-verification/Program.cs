using Vex.Core.Services;

// 使用本验证宿主自己的配置文件，验证真实存储，不替换为内存实现。
var first = new AppSettingsStore();
first.Update(settings => settings with { ThemeKey = "system", PaletteKey = "aurora" });
var second = new AppSettingsStore();
if (second.Current.ThemeKey != "system" || second.Current.PaletteKey != "aurora")
    throw new InvalidOperationException("Theme mode and palette did not survive a new settings store.");

// 来自另一实例的部分更新必须保留磁盘上的配色。
second.Update(settings => settings with { ThemeKey = "dark" });
first.Update(settings => settings with { ShowLineNumbers = true });
var restored = new AppSettingsStore().Current;
if (restored.ThemeKey != "dark" || restored.PaletteKey != "aurora" || restored.ShowLineNumbers != true)
    throw new InvalidOperationException("A partial update discarded persisted appearance settings.");

Console.WriteLine("PASS: real settings persist independent mode and palette across instances and partial updates.");
