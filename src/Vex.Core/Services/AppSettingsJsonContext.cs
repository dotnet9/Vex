using System.Text.Json.Serialization;

namespace Vex.Core.Services;

/// <summary>应用设置 JSON 的 AOT 源生成上下文：运行时反射序列化在本应用被禁用（runtimeconfig 开关）。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
