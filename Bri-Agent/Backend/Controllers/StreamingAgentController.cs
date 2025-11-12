using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Models;
using System.Text;
using System.Diagnostics;
using BriAgent.Backend.Services; // Para AgentInvocationTelemetry

namespace BriAgent.Backend.Controllers;

[Route("bri-agent/agents")] // mismo prefijo que el controlador original
public class StreamingAgentController : BaseAgentController
{
    public record BasicRequest(string? prompt);

    [HttpPost("stream")]
    public async Task Stream([FromBody] BasicRequest? req)
    {
        SetSseHeaders();
        var prompt = string.IsNullOrWhiteSpace(req?.prompt)
            ? "Explica brevemente el concepto de streaming de tokens en LLMs"
            : req!.prompt!;

        try
        {
            using var activity = StartActivity("agent.stream", prompt.Length);
            var agent = CreateBasicAgent();
            var model = BriAgent.Backend.Config.Credentials.Model;
            var profile = new UiProfile(mode: "streaming", stream: true, recommendedView: "StreamingViewer", capabilities: new[] { "streaming" });
            var meta = BuildMeta("streaming-agent", nameof(StreamingAgentController), profile);
            await WriteStartedAsync(new { model, prompt, meta });

            var responseChars = 0;
            var lastHeartbeat = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            await foreach (var update in agent.RunStreamingAsync(prompt))
            {
                if (HttpContext.RequestAborted.IsCancellationRequested) break;
                var chunk = update.Text;
                if (string.IsNullOrEmpty(chunk)) continue;
                await WriteTokenAsync(chunk);
                responseChars += chunk.Length;
                if ((DateTime.UtcNow - lastHeartbeat).TotalSeconds >= 5)
                {
                    await WriteHeartbeatAsync();
                    lastHeartbeat = DateTime.UtcNow;
                }
            }
            sw.Stop();
            await WriteCompletedAsync();
            TelemetryStore.Add(new AgentInvocationTelemetry(DateTimeOffset.UtcNow, "basic-stream", model, prompt.Length, responseChars, sw.ElapsedMilliseconds, activity?.TraceId.ToString(), activity?.SpanId.ToString()));
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(ex.Message);
        }
    }

    [HttpGet("stream")]
    public async Task StreamGet([FromQuery] string? prompt)
    {
        SetSseHeaders();
        var text = string.IsNullOrWhiteSpace(prompt)
            ? "Explica brevemente el concepto de streaming de tokens en LLMs"
            : prompt!;

        try
        {
            using var activity = StartActivity("agent.stream", text.Length);
            var agent = CreateBasicAgent();
            var model = BriAgent.Backend.Config.Credentials.Model;
            var profile = new UiProfile(mode: "streaming", stream: true, recommendedView: "StreamingViewer", capabilities: new[] { "streaming" });
            var meta = BuildMeta("streaming-agent", nameof(StreamingAgentController), profile);
            await WriteStartedAsync(new { model, prompt = text, meta });

            var responseChars = 0;
            var lastHeartbeat = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            await foreach (var update in agent.RunStreamingAsync(text))
            {
                if (HttpContext.RequestAborted.IsCancellationRequested) break;
                var chunk = update.Text;
                if (string.IsNullOrEmpty(chunk)) continue;
                await WriteTokenAsync(chunk);
                responseChars += chunk.Length;
                if ((DateTime.UtcNow - lastHeartbeat).TotalSeconds >= 5)
                {
                    await WriteHeartbeatAsync();
                    lastHeartbeat = DateTime.UtcNow;
                }
            }
            sw.Stop();
            await WriteCompletedAsync();
            TelemetryStore.Add(new AgentInvocationTelemetry(DateTimeOffset.UtcNow, "basic-stream", model, text.Length, responseChars, sw.ElapsedMilliseconds, activity?.TraceId.ToString(), activity?.SpanId.ToString()));
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(ex.Message);
        }
    }
}
