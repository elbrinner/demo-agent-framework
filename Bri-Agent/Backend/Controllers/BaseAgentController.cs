using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Models;
using System.Diagnostics;
using Microsoft.Agents.AI;

namespace BriAgent.Backend.Controllers;

[ApiController]
public abstract class BaseAgentController : ControllerBase
{
    public BaseAgentController() { }

    protected void SetSseHeaders()
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";
    }

    protected UiMeta BuildMeta(string demoId, string controller, UiProfile profile) =>
        new("v1", demoId, controller, profile);

    protected AIAgent CreateBasicAgent() => BriAgent.Backend.Services.AgentFactory.CreateBasicAgent();

    protected Activity? StartActivity(string name, int promptLength)
    {
        var activity = BriAgent.Backend.Telemetry.ActivitySource.StartActivity(name, ActivityKind.Server);
        activity?.SetTag("agent.prompt.length", promptLength);
        return activity;
    }

    protected async Task WriteStartedAsync(object payload)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "started", payload);

    protected async Task WriteTokenAsync(string text)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "token", new { text });

    // Token con contexto de paso (workflow). Si stepId es null se comporta igual que el anterior.
    protected async Task WriteTokenAsync(string text, string? stepId)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "token", new { text }, stepId);

    protected async Task WriteHeartbeatAsync()
        => await BriAgent.Backend.Services.SseEventWriter.WriteHeartbeatAsync(Response);

    protected async Task WriteCompletedAsync(object? payload = null)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "completed", payload ?? new { });

    protected async Task WriteErrorAsync(string message)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "error", new { error = message });

    // Eventos específicos de workflows
    protected async Task WriteWorkflowStartedAsync(object payload)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "workflow_started", payload);

    protected async Task WriteStepStartedAsync(string stepId, string name)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "step_started", new { id = stepId, name }, stepId);

    protected async Task WriteStepCompletedAsync(string stepId, string text, long durationMs)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "step_completed", new { id = stepId, text, durationMs }, stepId);

    protected async Task WriteWorkflowCompletedAsync(object payload)
        => await BriAgent.Backend.Services.SseEventWriter.WriteEventAsync(Response, "workflow_completed", payload);
}
