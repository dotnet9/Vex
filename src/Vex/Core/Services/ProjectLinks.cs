using System.Reflection;

namespace Vex.Core.Services;

/// <summary>产品链接由仓库配置写入发布元数据，程序与原型使用同一来源。</summary>
public static class ProjectLinks
{
    public static readonly string Website = typeof(App).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == nameof(Website)).Value
        ?? throw new InvalidOperationException("Missing website in project configuration.");
}
