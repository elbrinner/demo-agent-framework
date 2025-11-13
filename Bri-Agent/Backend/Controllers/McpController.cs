using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;

namespace BriAgent.Backend.Controllers;

[ApiController]
[Route("api/mcp")] // prefijo específico para MCP
public class McpController : ControllerBase
{
    private readonly McpFileSystemService _svc;

    public McpController(McpFileSystemService svc)
    {
        _svc = svc;
    }

    [HttpGet("resources")]
    public async Task<ActionResult<IEnumerable<McpResourceDto>>> Resources(CancellationToken ct)
    {
        var list = await _svc.ListAsync(ct);
        return Ok(list);
    }

    [HttpGet("read")]
    public async Task<ActionResult<object>> Read([FromQuery] string uri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uri)) return BadRequest(new { error = "InvalidUri", detail = "URI vacío" });
        var text = await _svc.ReadTextAsync(uri, ct);
        return Ok(new { uri, text });
    }
}
