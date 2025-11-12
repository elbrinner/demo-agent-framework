using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using System.Diagnostics;
using BriAgent.Backend.DemosRuntime;

namespace BriAgent.Backend.DemosRuntime
{
    /// <summary>
    /// Demo de traducción en paralelo: EN -> (FR, PT, DE) en paralelo, luego un agente final combina y devuelve en inglés.
    /// Emite trazas por agente y tiempo total del workflow via SSE.
    /// </summary>
    public class TranslationParallelWorkflowApiDemo : IApiDemo
    {
        public string Id => "translation-parallel-workflow";
        public string Title => "Workflow Traducción Paralela";
    public string Description => "Traduce un texto a Francés, Portugués y Alemán en paralelo y sintetiza el resultado en inglés incluyendo las traducciones intermedias.";
        public IEnumerable<string> Tags => new [] { "workflow", "traduccion", "paralelo", "sse" };
        public IEnumerable<string> SourceFiles => new [] { "DemosRuntime/TranslationParallelWorkflowApiDemo.cs" };

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
                using var workflowActivity = Telemetry.ActivitySource.StartActivity("translation.par", ActivityKind.Server);
                workflowActivity?.SetTag("translation.original.length", original.Length);
                await SseEventWriter.WriteEventAsync(httpContext.Response, "workflow_started", new { mode = "parallel", original });

                // Crear agentes
                var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
                var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
                var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
                var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

                var ct = httpContext.RequestAborted;
                var totalSw = Stopwatch.StartNew();

                async Task<(string output,long duration)> FanoutAsync(string stepId, AIAgent agent)
                {
                    using var stepActivity = Telemetry.ActivitySource.StartActivity($"translation.par.{stepId}", ActivityKind.Internal);
                    var sw = Stopwatch.StartNew();
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_started", new { stepId, inputLength = original.Length }, stepId);
                    var full = "";
                    await foreach (var up in agent.RunStreamingAsync(original))
                    {
                        if (ct.IsCancellationRequested) break;
                        var chunk = up.Text;
                        if (string.IsNullOrEmpty(chunk)) continue;
                        full += chunk;
                        await SseEventWriter.WriteEventAsync(httpContext.Response, "token", new { stepId, text = chunk }, stepId);
                    }
                    // Nota: evitamos una segunda llamada al modelo, usamos solo el stream acumulado
                    sw.Stop();
                    stepActivity?.SetTag("translation.step.output.length", full.Length);
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_completed", new { stepId, output = full, durationMs = sw.ElapsedMilliseconds }, stepId);
                    return (full, sw.ElapsedMilliseconds);
                }

                var frTask = FanoutAsync("fr", frenchAgent);
                var ptTask = FanoutAsync("pt", portugueseAgent);
                var deTask = FanoutAsync("de", germanAgent);

                await Task.WhenAll(frTask, ptTask, deTask);
                var french = frTask.Result.output;
                var portuguese = ptTask.Result.output;
                var german = deTask.Result.output;

                // Paso final
                using (var stepActivity = Telemetry.ActivitySource.StartActivity("translation.par.final", ActivityKind.Internal))
                {
                    var sw = Stopwatch.StartNew();
                    var stepId = "final";
                    var finalInput = $"Original: {original}\nFrancés: {french}\nPortugués: {portuguese}\nAlemán: {german}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_started", new { stepId, inputLength = finalInput.Length }, stepId);
                    var finalText = "";
                    await foreach (var up in finalAgent.RunStreamingAsync(finalInput))
                    {
                        if (ct.IsCancellationRequested) break;
                        var chunk = up.Text;
                        if (string.IsNullOrEmpty(chunk)) continue;
                        finalText += chunk;
                        await SseEventWriter.WriteEventAsync(httpContext.Response, "token", new { stepId, text = chunk }, stepId);
                    }
                    // Nota: evitamos una segunda llamada al modelo, usamos solo el stream acumulado
                    sw.Stop();
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "step_completed", new { stepId, output = finalText, durationMs = sw.ElapsedMilliseconds }, stepId);

                    totalSw.Stop();
                    await SseEventWriter.WriteEventAsync(httpContext.Response, "workflow_completed", new {
                        totalDurationMs = totalSw.ElapsedMilliseconds,
                        result = finalText,
                        steps = new[] {
                            new { id = "fr", durationMs = frTask.Result.duration, chars = french.Length },
                            new { id = "pt", durationMs = ptTask.Result.duration, chars = portuguese.Length },
                            new { id = "de", durationMs = deTask.Result.duration, chars = german.Length },
                            new { id = "final", durationMs = sw.ElapsedMilliseconds, chars = finalText.Length }
                        }
                    });
                }
            }

            app.MapPost("/bri-agent/demos/translation-parallel-workflow/run", async (
                HttpContext httpContext,
                [FromBody] RunRequest? body) =>
            {
                await HandleAsync(httpContext, body?.prompt);
            });

            app.MapGet("/bri-agent/demos/translation-parallel-workflow/run", async (
                HttpContext httpContext,
                [FromQuery] string? prompt) =>
            {
                await HandleAsync(httpContext, prompt);
            });
        }
    }
}
