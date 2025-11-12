using Microsoft.AspNetCore.Routing;
using BriAgent.Backend.DemosRuntime;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using System.ClientModel;
using BriAgent.Backend.Controllers;

namespace BriAgent.Backend.DemosRuntime
{
    public class ThreadAgentApiDemo : IApiDemo
    {
        public string Id => "thread-agent";
        public string Title => "Agente con Contexto (Thread)";
        public string Description => "Demuestra un agente que mantiene contexto entre interacciones usando threads.";
        public IEnumerable<string> Tags => new[] { "agente", "thread", "contexto" };
    public IEnumerable<string> SourceFiles => new[] { "DemosRuntime/ThreadAgentApiDemo.cs", "Controllers/ThreadAgentController.cs", "Services/ThreadStore.cs" };

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/bri-agent/demos/thread-agent/run", async (
                HttpContext httpContext,
                [FromBody] ThreadRequestDto? request
            ) =>
            {
                var threadId = string.IsNullOrWhiteSpace(request?.threadId) ? Guid.NewGuid().ToString("N") : request.threadId!;
                var message = string.IsNullOrWhiteSpace(request?.message) ? "Hola, me llamo Usuario de Bri-Agent." : request.message!;
                using var activity = Telemetry.ActivitySource.StartActivity("demo.thread.run");

                var agent = AgentFactory.CreateBasicAgent();
                // Usar contexto real (AgentThread)
                var context = ThreadStore.GetOrCreateAgentContext(threadId, agent);
                ThreadStore.AddMessage(threadId, message);
                var threadContext = (Microsoft.Agents.AI.AgentThread)context;
                var sb = new System.Text.StringBuilder();
                await foreach (var update in agent.RunStreamingAsync(message, threadContext))
                {
                    var chunk = update.Text;
                    if (!string.IsNullOrEmpty(chunk)) sb.Append(chunk);
                }
                var full = sb.ToString();
                ThreadStore.AddMessage(threadId, full);
                var turns = ThreadStore.GetHistory(threadId).Count / 2;
                var meta = new BriAgent.Backend.Models.UiMeta(
                    version: "v1",
                    demoId: "thread-agent",
                    controller: nameof(ThreadAgentApiDemo),
                    ui: new BriAgent.Backend.Models.UiProfile(mode: "thread", stream: false, history: true, recommendedView: "ThreadChat", capabilities: new[] { "memory" })
                );
                return Results.Ok(new { threadId, user = message, response = full, turns, meta });
            });
        }
    }

    public record ThreadRequestDto(string? threadId, string? message);
}