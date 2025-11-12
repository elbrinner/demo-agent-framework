using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using BriAgent.Backend.Services;
using BriAgent.Backend.Config;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BriAgent.Backend.DemosRuntime
{
    public class BasicAgentApiDemo : IApiDemo
    {
        public string Id => "bri-basic-agent";
        public string Title => "Agente Básico";
        public string Description => "Retorna una respuesta simple usando Azure OpenAI";
        public IEnumerable<string> Tags => new[] { "agent", "basic", "azure-openai" };
        public IEnumerable<string> SourceFiles => new[] { "Demos/HolaMundo.cs" }; // referencia al origen original

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost($"/bri-agent/demos/{Id}/run", async (HttpRequest request) =>
            {
                var sw = Stopwatch.StartNew();
                using var activity = BriAgent.Backend.Telemetry.ActivitySource.StartActivity("demo.basic.run", ActivityKind.Server);
                try
                {
                    var body = await new StreamReader(request.Body).ReadToEndAsync();
                    var prompt = string.IsNullOrWhiteSpace(body) ? "Hola, escribe un haiku sobre agentes." : body;
                    activity?.SetTag("demo.prompt.length", prompt.Length);
                    var model = Credentials.Model;
                    var agent = AgentFactory.CreateBasicAgent();
                    var response = await agent.RunAsync(prompt);
                    sw.Stop();
                    var usage = new { promptChars = prompt.Length, responseChars = response.Text?.Length ?? 0, durationMs = sw.ElapsedMilliseconds };
                    var traceId = activity?.TraceId.ToString();
                    var spanId = activity?.SpanId.ToString();
                    TelemetryStore.Add(new AgentInvocationTelemetry(DateTimeOffset.UtcNow, "basic-demo", model, prompt.Length, response.Text?.Length ?? 0, sw.ElapsedMilliseconds, traceId, spanId));
                    return Results.Json(new { prompt, response = response.Text, model, usage, traceId, spanId });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    return Results.Problem(title: "Error ejecutando demo básica", detail: ex.Message);
                }
            })
            .WithName("BriBasicAgentDemoRun")
            .WithSummary("Ejecuta prompt simple")
            .WithDescription("Demo básica Bri-Agent para mostrar respuesta inicial de un agente.");
        }
    }
}
