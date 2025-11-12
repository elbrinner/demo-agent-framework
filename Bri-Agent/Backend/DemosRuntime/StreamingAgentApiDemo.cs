using Microsoft.AspNetCore.Routing;
using BriAgent.Backend.DemosRuntime;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using System.ClientModel;
using System.Text.Json;
using BriAgent.Backend.Controllers;

namespace BriAgent.Backend.DemosRuntime
{
    public class StreamingAgentApiDemo : IApiDemo
    {
        public string Id => "streaming-agent";
        public string Title => "Agente en Modo Streaming";
        public string Description => "Demuestra un agente básico ejecutándose en modo streaming con eventos SSE.";
        public IEnumerable<string> Tags => new[] { "agente", "streaming", "sse" };
        public IEnumerable<string> SourceFiles => new[] { "DemosRuntime/StreamingAgentApiDemo.cs", "Controllers/AgentsController.cs", "Services/SseEventWriter.cs" };

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/bri-agent/demos/streaming-agent/run", async (
                HttpContext httpContext,
                [FromBody] BasicRequest? request
            ) =>
            {
                var prompt = request?.prompt ?? "Explica qué es el streaming en LLMs.";
                using var activity = Telemetry.ActivitySource.StartActivity("demo.streaming.run");

                var agent = AgentFactory.CreateBasicAgent();

                await SseEventWriter.WriteEventAsync(httpContext.Response, "started", new { prompt });
                await foreach (var update in agent.RunStreamingAsync(prompt))
                {
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "token", new { text = update.ToString() });
                }
                await SseEventWriter.WriteEventAsync(httpContext.Response, "complete", new { });
            });
        }
    }
}