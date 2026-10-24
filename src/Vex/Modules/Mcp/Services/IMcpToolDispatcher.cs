using System.Text.Json;
using Vex.Modules.Mcp.Models;

namespace Vex.Modules.Mcp.Services;

public interface IMcpToolDispatcher
{
    McpToolsListResult ListTools();

    Task<ResourceListResult> ListResourcesAsync();

    ResourceReadResult ReadResource(string uri);

    Task<McpToolCallResult> CallToolAsync(string name, JsonElement? arguments);
}
