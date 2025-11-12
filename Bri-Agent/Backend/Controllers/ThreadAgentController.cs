using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using BriAgent.Backend.Models;
using BriAgent.Backend.Services;

namespace BriAgent.Backend.Controllers;

[Route("bri-agent/agents")] // mismo prefijo
public class ThreadAgentController : BaseAgentController
{
    public record ThreadRequest(string? threadId, string? message);

    [HttpPost("thread")] // POST /bri-agent/agents/thread
    public async Task<IActionResult> Threaded([FromBody] ThreadRequest req)
    {
        var threadId = string.IsNullOrWhiteSpace(req.threadId) ? Guid.NewGuid().ToString("N") : req.threadId!;
        var message = string.IsNullOrWhiteSpace(req.message) ? "Hola, me llamo Usuario de Bri-Agent." : req.message!;

        var agent = CreateBasicAgent();
        var context = ThreadStore.GetOrCreateAgentContext(threadId, agent);

        // Historial educativo (para UI)
        ThreadStore.AddMessage(threadId, message);

        var sb = new StringBuilder();
        var threadContext = (Microsoft.Agents.AI.AgentThread)context;
        await foreach (var update in agent.RunStreamingAsync(message, threadContext))
        {
            var chunk = update.Text;
            if (!string.IsNullOrEmpty(chunk)) sb.Append(chunk);
        }
        var full = sb.ToString();
        ThreadStore.AddMessage(threadId, full);
        var turns = ThreadStore.GetHistory(threadId).Count / 2;

        var meta = BuildMeta(
            demoId: "thread-agent",
            controller: nameof(ThreadAgentController),
            profile: new UiProfile(mode: "thread", stream: false, history: true, recommendedView: "ThreadChat", capabilities: new[] { "memory" })
        );

        return Ok(new { threadId, user = message, response = full, turns, meta });
    }

    [HttpGet("thread/{threadId}/history")] // GET /bri-agent/agents/thread/{threadId}/history
    public IActionResult ThreadHistory(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId)) return BadRequest(new { error = "threadId requerido" });
        var history = ThreadStore.GetHistory(threadId);
        var meta = BuildMeta(
            demoId: "thread-agent",
            controller: nameof(ThreadAgentController),
            profile: new UiProfile(mode: "thread", stream: false, history: true, recommendedView: "ThreadChat", capabilities: new[] { "memory" })
        );
        return Ok(new { threadId, count = history.Count, history, meta });
    }

    [HttpGet("thread/stream")] // GET /bri-agent/agents/thread/stream?message=...&threadId=...
    public async Task ThreadStreamGet([FromQuery] string? message, [FromQuery] string? threadId)
    {
        SetSseHeaders();

        try
        {
            var msg = string.IsNullOrWhiteSpace(message)
                ? "Hola, me llamo Usuario de Bri-Agent."
                : message!;

            var id = string.IsNullOrWhiteSpace(threadId) ? Guid.NewGuid().ToString("N") : threadId!;

            using var activity = StartActivity("agent.thread.stream", msg.Length);
            var agent = CreateBasicAgent();
            var model = BriAgent.Backend.Config.Credentials.Model;
            var sw = Stopwatch.StartNew();
            var responseChars = 0;
            var lastHeartbeat = DateTime.UtcNow;
            var sb = new StringBuilder();

            // Historial educativo
            ThreadStore.AddMessage(id, msg);
            // Contexto real
            var context = ThreadStore.GetOrCreateAgentContext(id, agent);
            var threadContext = (Microsoft.Agents.AI.AgentThread)context;

            var profile = new UiProfile(mode: "thread", stream: true, history: true, recommendedView: "ThreadChat", capabilities: new[] { "memory", "streaming" });
            var meta = BuildMeta("thread-agent", nameof(ThreadAgentController), profile);
            await WriteStartedAsync(new { model, threadId = id, prompt = msg, meta });

            await foreach (var update in agent.RunStreamingAsync(msg, threadContext))
            {
                if (HttpContext.RequestAborted.IsCancellationRequested) break;
                var chunk = update.Text;
                if (string.IsNullOrEmpty(chunk)) continue;
                sb.Append(chunk);
                await WriteTokenAsync(chunk);
                responseChars += chunk.Length;

                if ((DateTime.UtcNow - lastHeartbeat).TotalSeconds >= 5)
                {
                    await WriteHeartbeatAsync();
                    lastHeartbeat = DateTime.UtcNow;
                }
            }

            sw.Stop();
            ThreadStore.AddMessage(id, sb.ToString());
            await WriteCompletedAsync(new { threadId = id });
            TelemetryStore.Add(new AgentInvocationTelemetry(DateTimeOffset.UtcNow, "thread-stream", model, msg.Length, responseChars, sw.ElapsedMilliseconds, activity?.TraceId.ToString(), activity?.SpanId.ToString()));
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(ex.Message);
        }
    }
}
