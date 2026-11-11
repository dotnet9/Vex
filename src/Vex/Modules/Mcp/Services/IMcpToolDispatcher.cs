using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Vex.Modules.Mcp.Services;

public interface IMcpToolDispatcher
{
    ListToolsResult ListTools();

    Task<ListResourcesResult> ListResourcesAsync();

    ReadResourceResult ReadResource(string uri);

    Task<CallToolResult> CallToolAsync(string name, JsonElement? arguments);
}
