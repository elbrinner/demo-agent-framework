using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using BriAgent.Backend.Models;
using BriAgent.Backend.Services;

namespace BriAgent.Backend.Controllers;

[Route("bri-agent/agents")] // mismo prefijo
public class BasicAgentController : BaseAgentController
{
    public record BasicRequest(string? prompt);

    [HttpPost("basic")] // POST /bri-agent/agents/basic
    public async Task<IActionResult> Basic([FromBody] BasicRequest? req)
    {
        try
        {
            var prompt = string.IsNullOrWhiteSpace(req?.prompt)
                ? "¿Qué es el 'Hola mundo' en el mundo de la programación?"
                : req!.prompt!;

            using var activity = StartActivity("agent.basic", prompt.Length);
            var model = BriAgent.Backend.Config.Credentials.Model;
            var agent = CreateBasicAgent();
            var resp = await agent.RunAsync(prompt);

            var usage = new { promptChars = prompt.Length, responseChars = resp.Text?.Length ?? 0, durationMs = 0L };
            // Si se quiere medir duración, envolver con Stopwatch aquí
            TelemetryStore.Add(new AgentInvocationTelemetry(DateTimeOffset.UtcNow, "basic-agent", model, prompt.Length, resp.Text?.Length ?? 0, 0, activity?.TraceId.ToString(), activity?.SpanId.ToString()));

            var meta = BuildMeta(
                demoId: "basic-agent",
                controller: nameof(BasicAgentController),
                profile: new UiProfile(mode: "single", stream: false, history: false, recommendedView: "PromptConsole", capabilities: new[] { "single" })
            );

            return Ok(new { prompt, response = resp.Text, model, usage, traceId = activity?.TraceId.ToString(), spanId = activity?.SpanId.ToString(), meta });
        }
        catch (Exception ex)
        {
            return Problem(title: "Error ejecutando agente básico", detail: ex.Message);
        }
    }
}
