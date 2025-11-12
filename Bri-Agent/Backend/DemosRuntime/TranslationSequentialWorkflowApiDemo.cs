using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using System.Diagnostics;
using BriAgent.Backend.DemosRuntime;

namespace BriAgent.Backend.DemosRuntime
{
    /// <summary>
    /// Demo de traducción secuencial: EN -> FR -> PT -> DE -> FINAL (agregación en inglés).
    /// Muestra tiempos por agente y tiempo total del workflow via SSE.
    /// </summary>
    public class TranslationSequentialWorkflowApiDemo : IApiDemo
    {
        public string Id => "translation-sequential-workflow";
        public string Title => "Workflow Traducción Secuencial";
    public string Description => "Traduce un texto inglés en cadena (francés→portugués→alemán) y retorna resultado final en inglés con las traducciones intermedias.";
        public IEnumerable<string> Tags => new [] { "workflow", "traduccion", "secuencial", "sse" };
        public IEnumerable<string> SourceFiles => new [] { "DemosRuntime/TranslationSequentialWorkflowApiDemo.cs" };

        public record RunRequest(string? prompt);

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            async Task HandleAsync(HttpContext httpContext, string? prompt)
            {
                httpContext.Response.Headers["Content-Type"] = "text/event-stream";
                httpContext.Response.Headers["Cache-Control"] = "no-cache";
                httpContext.Response.Headers["X-Accel-Buffering"] = "no";
                httpContext.Response.Headers["Connection"] = "keep-alive";

                var original = string.IsNullOrWhiteSpace(prompt) ? "Hola mundo, ¿cómo estás?" : prompt!;

                using var workflowActivity = Telemetry.ActivitySource.StartActivity("translation.seq", ActivityKind.Server);
                workflowActivity?.SetTag("translation.original.length", original.Length);

                await SseEventWriter.WriteEventAsync(httpContext.Response, "workflow_started", new { mode = "sequential", original });

                // Crear agentes con instrucciones
                var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
                var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
                var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
                var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

                var ct = httpContext.RequestAborted;
                string french = string.Empty, portuguese = string.Empty, german = string.Empty, final = string.Empty;
                var totalSw = Stopwatch.StartNew();

                async Task<(string output,long duration)> RunTranslationAsync(string stepId, AIAgent agent, string input)
                {
                    using var stepActivity = Telemetry.ActivitySource.StartActivity($"translation.seq.{stepId}", ActivityKind.Internal);
                    var sw = Stopwatch.StartNew();
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_started", new { stepId, inputLength = input.Length }, stepId);
                    var full = "";
                    await foreach (var up in agent.RunStreamingAsync(input))
                    {
                        if (ct.IsCancellationRequested) break;
                        var chunk = up.Text;
                        if (string.IsNullOrEmpty(chunk)) continue;
                        full += chunk;
                        await SseEventWriter.WriteEventAsync(httpContext.Response, "token", new { stepId, text = chunk }, stepId);
                    }
                    // Nota: evitamos una segunda llamada al modelo. Usamos el texto acumulado del streaming como salida final.
                    sw.Stop();
                    stepActivity?.SetTag("translation.step.output.length", full.Length);
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_completed", new { stepId, output = full, durationMs = sw.ElapsedMilliseconds }, stepId);
                    return (full, sw.ElapsedMilliseconds);
                }

                // EN->FR
                (french, var frMs) = await RunTranslationAsync("fr", frenchAgent, original);
                // FR->PT (usamos traducción anterior como entrada siguiendo cadena)
                (portuguese, var ptMs) = await RunTranslationAsync("pt", portugueseAgent, french);
                // PT->DE
                (german, var deMs) = await RunTranslationAsync("de", germanAgent, portuguese);

                // FINAL agregando todo
                var finalInput = $"Original: {original}\nFrancés: {french}\nPortugués: {portuguese}\nAlemán: {german}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
                (final, var finalMs) = await RunTranslationAsync("final", finalAgent, finalInput);

                totalSw.Stop();
                await SseEventWriter.WriteEventAsync(httpContext.Response, "workflow_completed", new {
                    totalDurationMs = totalSw.ElapsedMilliseconds,
                    steps = new[] {
                        new { id = "fr", durationMs = frMs, chars = french.Length },
                        new { id = "pt", durationMs = ptMs, chars = portuguese.Length },
                        new { id = "de", durationMs = deMs, chars = german.Length },
                        new { id = "final", durationMs = finalMs, chars = final.Length }
                    },
                    result = final
                });
            }

            app.MapPost("/bri-agent/demos/translation-sequential-workflow/run", async (
                HttpContext httpContext,
                [FromBody] RunRequest? body) =>
            {
                await HandleAsync(httpContext, body?.prompt);
            });

            app.MapGet("/bri-agent/demos/translation-sequential-workflow/run", async (
                HttpContext httpContext,
                [FromQuery] string? prompt) =>
            {
                await HandleAsync(httpContext, prompt);
            });
        }
    }
}
