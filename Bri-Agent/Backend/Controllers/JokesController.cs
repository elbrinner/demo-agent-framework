using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using System.Threading.Channels;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI; // necesario para CreateAIAgent y tipos de Agent Framework
using BriAgent.Backend.Config;
using System.Text.RegularExpressions;

namespace BriAgent.Backend.Controllers;

[ApiController]
[Route("api/jokes")] // prefijo para workflow de chistes
public class JokesController : ControllerBase
{
    private readonly JokesWorkflowService _workflow;
    private readonly McpFileSystemService _mcp;
    private readonly AgentRunner _runner;
    private readonly IAgentFactory _factory;

    public JokesController(JokesWorkflowService workflow, McpFileSystemService mcp, AgentRunner runner, IAgentFactory factory)
    {
        _workflow = workflow;
        _mcp = mcp;
        _runner = runner;
        _factory = factory;
    }

    public record StartRequest(int? Total, string? Topic, bool? EnsureHitl);
    public record StartResponse(string WorkflowId, int TargetTotal);
    public record StatusResponse(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals, IEnumerable<object> Items);
    public record MetricsResponse(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals);
    public record ApprovalResponse(string ApprovalId, string Status);
    public record StopResponse(string WorkflowId, string Status);
    public record SearchItem(string Name, string Uri, string Preview);
    public record SearchResponse(int Total, int Matched, IEnumerable<SearchItem> Results);
    public record AiSummaryRequest(string? Query, int? Limit, int? MaxChars);
    public record AiSummaryResponse(int Considered, int Included, string Summary);
    public record ApprovalListResponse(int Total, IEnumerable<object> Items);

    [HttpPost("start")]
    public ActionResult<StartResponse> Start([FromBody] StartRequest? req)
    {
        var total = req?.Total is > 0 and <= 50 ? req.Total!.Value : 10;
        var ensureHitl = req?.EnsureHitl == true;
        var state = _workflow.Start(total, ensureHitl);
        return Ok(new StartResponse(state.WorkflowId, state.TargetTotal));
    }

    [HttpGet("stream/{workflowId}")]
    public async Task Stream([FromRoute] string workflowId)
    {
    Response.Headers["Content-Type"] = "text/event-stream";
    Response.Headers["Cache-Control"] = "no-cache";
    Response.Headers["X-Accel-Buffering"] = "no"; // nginx disable buffering
        var ct = HttpContext.RequestAborted;
        var reader = HttpContext.RequestServices.GetRequiredService<JokesEventBus>().Subscribe(workflowId);

        // Enviar evento inicial si hay estado
        var state = _workflow.GetState(workflowId);
        if (state != null)
        {
            await SseEventWriter.WriteEventAsync(Response, "state_snapshot", new { state.WorkflowId, state.TargetTotal, state.Generated, state.Saved, state.Deleted, state.PendingApprovals }, null, ct);
        }

        var heartbeatAt = DateTime.UtcNow;
        await foreach (var evt in reader.ReadAllAsync(ct))
        {
            await SseEventWriter.WriteEventAsync(Response, evt.Type, evt.Payload, evt.JokeId, ct);
            if (DateTime.UtcNow - heartbeatAt > TimeSpan.FromSeconds(15))
            {
                await SseEventWriter.WriteHeartbeatAsync(Response, ct);
                heartbeatAt = DateTime.UtcNow;
            }
        }
    }

    [HttpGet("status/{workflowId}")]
    public ActionResult<StatusResponse> Status([FromRoute] string workflowId)
    {
        var state = _workflow.GetState(workflowId);
        if (state is null) return NotFound(new { error = "NotFound" });
        var items = _workflow.GetItems(workflowId)
            .Select(i => new { i.Id, i.Text, i.Score, i.Uri, i.ApprovalId });
        return Ok(new StatusResponse(state.WorkflowId, state.TargetTotal, state.Generated, state.Saved, state.Deleted, state.PendingApprovals, items));
    }

    [HttpPost("approve")]
    public ActionResult<ApprovalResponse> Approve([FromBody] string approvalId)
    {
        var ok = ApprovalStore.Approve(approvalId);
        Console.WriteLine($"[HITL] APPROVE approvalId={approvalId} ok={ok} at={DateTimeOffset.UtcNow:O}");
        if (!ok) return NotFound(new { error = "NotFound", approvalId });
        return Ok(new ApprovalResponse(approvalId, "approved"));
    }

    [HttpPost("reject")]
    public ActionResult<ApprovalResponse> Reject([FromBody] string approvalId)
    {
        var ok = ApprovalStore.Reject(approvalId, "Rejected by user");
        Console.WriteLine($"[HITL] REJECT approvalId={approvalId} ok={ok} at={DateTimeOffset.UtcNow:O}");
        if (!ok) return NotFound(new { error = "NotFound", approvalId });
        return Ok(new ApprovalResponse(approvalId, "rejected"));
    }

    // Diagnóstico: consultar estado de un approvalId
    [HttpGet("approval/{approvalId}")]
    public ActionResult<object> ApprovalStatus([FromRoute] string approvalId)
    {
        var r = ApprovalStore.Get(approvalId);
        if (r is null) return NotFound(new { error = "NotFound", approvalId });
        return Ok(new { r.ApprovalId, status = r.Status.ToString(), r.CreatedAt, r.Reason });
    }

    // Listado de approvals activos (snapshot)
    [HttpGet("approvals")]
    public ActionResult<ApprovalListResponse> Approvals()
    {
        var list = ApprovalStore.List()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.ApprovalId, status = a.Status.ToString(), a.CreatedAt, a.Reason })
            .ToList();
        return Ok(new ApprovalListResponse(list.Count, list));
    }

    [HttpGet("list")]
    public async Task<ActionResult<object>> List()
    {
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(new { count = jokes.Count, resources = jokes });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary([FromQuery] int? limit)
    {
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.Name)
                        .Take(Math.Clamp(limit ?? 5, 1, 50))
                        .ToList();
        var previews = new List<object>();
        foreach (var j in jokes)
        {
            try
            {
                var text = await _mcp.ReadTextAsync(j.Uri);
                var firstLine = text.Split('\n').FirstOrDefault()?.Trim() ?? "";
                previews.Add(new { j.Name, j.Uri, firstLine });
            }
            catch { /* ignore individual errors */ }
        }
        return Ok(new { total = jokes.Count, previews });
    }

    // Nuevo: contar chistes guardados vía MCP
    [HttpGet("count")]
    public async Task<ActionResult<object>> Count()
    {
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(new { count = jokes.Count });
    }

    // Nuevo: buscar chistes por substring (case-insensitive) leyendo archivos con MCP
    [HttpGet("search")]
    public async Task<ActionResult<SearchResponse>> Search([FromQuery] string? query, [FromQuery] int? limit)
    {
        var q = (query ?? string.Empty).Trim();
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(r => r.Name)
                        .ToList();
        var cap = Math.Clamp(limit ?? 50, 1, 500);
        var results = new List<SearchItem>();
        foreach (var j in jokes)
        {
            if (results.Count >= cap) break;
            try
            {
                var text = await _mcp.ReadTextAsync(j.Uri);
                if (string.IsNullOrEmpty(q) || text.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    var firstLine = text.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
                    results.Add(new SearchItem(j.Name, j.Uri, firstLine));
                }
            }
            catch { /* ignorar errores individuales */ }
        }
        return Ok(new SearchResponse(jokes.Count, results.Count, results));
    }

    // Nuevo: resumen con IA de contenidos leídos vía MCP
    [HttpPost("ai-summary")]
    public async Task<ActionResult<AiSummaryResponse>> AiSummary([FromBody] AiSummaryRequest body)
    {
        var q = (body?.Query ?? string.Empty).Trim();
        var cap = Math.Clamp(body?.Limit ?? 10, 1, 50);
        var maxChars = Math.Clamp(body?.MaxChars ?? 4000, 500, 20000); // límite de caracteres agregados

        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.Name)
                        .ToList();

        int considered = jokes.Count;
        var included = new List<(string name, string uri, string text)>();
        int totalChars = 0;
        foreach (var j in jokes)
        {
            if (included.Count >= cap) break;
            try
            {
                var text = await _mcp.ReadTextAsync(j.Uri);
                if (!string.IsNullOrEmpty(q) && !text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    continue;
                var payload = text.Length > 1200 ? text.Substring(0, 1200) : text; // snippet razonable por archivo
                if (totalChars + payload.Length > maxChars) break;
                included.Add((j.Name, j.Uri, payload));
                totalChars += payload.Length;
            }
            catch { /* ignorar */ }
        }

        // Preparar prompt para LLM
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Eres un asistente que resume en español el contenido de varios archivos de chistes.");
        sb.AppendLine("Devuelve un resumen breve (3-6 frases) con ideas generales y ejemplos si es útil. Evita repetir líneas exactas.");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(q))
        {
            sb.AppendLine($"Prioriza información relacionada con: '{q}'.");
        }
        for (int i = 0; i < included.Count; i++)
        {
            var it = included[i];
            sb.AppendLine($"=== Archivo {i+1}: {it.name} ===");
            sb.AppendLine(it.text);
            sb.AppendLine();
        }

        // Crear agente con Agent Framework (sin tools para este caso) y ejecutar vía AgentRunner para telemetría
        var agent = _factory.CreateBasicAgent(
            instructions: "Eres un asistente conciso y claro.",
            tools: Array.Empty<AIFunction>()
        );
        var response = await _runner.RunAsync(
            agent,
            sb.ToString(),
            thread: null,
            cancellationToken: HttpContext.RequestAborted,
            model: Credentials.Model,
            agentType: "jokes.ai_summary"
        );
        var summary = response.Text ?? "(sin resumen)";

        return Ok(new AiSummaryResponse(considered, included.Count, summary));
    }

    // Nuevo: devolver el mejor chiste por score parseado del archivo (primera línea: timestamp=...|score=NN)
    [HttpGet("best")]
    public async Task<ActionResult<object>> Best()
    {
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase)).ToList();
        if (jokes.Count == 0) return NotFound(new { error = "NoData" });

        var rx = new Regex(@"score=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        (string name, string uri, int score, string firstLine)? best = null;
        foreach (var j in jokes)
        {
            try
            {
                var text = await _mcp.ReadTextAsync(j.Uri);
                var first = text.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
                var m = rx.Match(first);
                var sc = m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : -1;
                if (best is null || sc > best.Value.score)
                {
                    best = (j.Name, j.Uri, sc, first);
                }
            }
            catch { /* ignorar */ }
        }
        if (best is null) return NotFound(new { error = "NoReadable" });
        return Ok(new { name = best.Value.name, uri = best.Value.uri, score = best.Value.score, firstLine = best.Value.firstLine });
    }

    // Nuevo: devolver contenidos de chistes (snippets) con límites para UI
    [HttpGet("contents")]
    public async Task<ActionResult<object>> Contents([FromQuery] int? limit, [FromQuery] int? maxCharsPerFile)
    {
        var cap = Math.Clamp(limit ?? 50, 1, 200);
        var maxChars = Math.Clamp(maxCharsPerFile ?? 1200, 100, 8000);
        var list = await _mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.Name)
                        .Take(cap)
                        .ToList();
        var files = new List<object>();
        foreach (var j in jokes)
        {
            try
            {
                var text = await _mcp.ReadTextAsync(j.Uri);
                var snippet = text.Length > maxChars ? text.Substring(0, maxChars) : text;
                files.Add(new { j.Name, j.Uri, content = snippet });
            }
            catch { /* ignorar */ }
        }
        return Ok(new { total = jokes.Count, included = files.Count, files });
    }

    // Endpoint de métricas por workflow para polls ligeros
    [HttpGet("metrics/{workflowId}")]
    public ActionResult<MetricsResponse> Metrics([FromRoute] string workflowId)
    {
        var state = _workflow.GetState(workflowId);
        if (state is null) return NotFound(new { error = "NotFound" });
        return Ok(new MetricsResponse(state.WorkflowId, state.TargetTotal, state.Generated, state.Saved, state.Deleted, state.PendingApprovals));
    }

    [HttpPost("stop/{workflowId}")]
    public ActionResult<StopResponse> Stop([FromRoute] string workflowId)
    {
        var ok = _workflow.Stop(workflowId);
        if (!ok) return NotFound(new { error = "NotFound" });
        return Ok(new StopResponse(workflowId, "stopped"));
    }
}
