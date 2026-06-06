using System.Text.Json;
using Vex.Modules.Mcp.Models;

namespace Vex.Modules.Mcp.Services;

public interface IMcpToolDispatcher
{
    McpToolsListResult ListTools();

    Task<McpToolCallResult> CallToolAsync(string name, JsonElement? arguments);
}
