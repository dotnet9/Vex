using System.Text.Json;
using Vex.Modules.Mcp.Models;

namespace Vex.Modules.Mcp.Services;

public interface IMcpToolDispatcher
{
    McpToolsListResult ListTools();

    ResourceListResult ListResources();

    ResourceReadResult ReadResource(string uri);

    Task<McpToolCallResult> CallToolAsync(string name, JsonElement? arguments);
}
