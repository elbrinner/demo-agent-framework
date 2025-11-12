using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos
{
    // Ejemplo educativo de workflow paralelo con SSE para Mostrar Código.
    // No se compila; ilustra estructura y eventos SSE por step.
    //
    // Integración sugerida con Minimal API:
    // app.MapGet("/api/workflows/parallel", async (HttpContext http, CancellationToken ct)
    //     => await ParallelWorkflowDemo.RunAsync(http.Response, ct));

    public sealed record WorkflowStep(string Id, string Prompt);

    public static class ParallelWorkflowDemo
    {
        public static async Task RunAsync(HttpResponse response, CancellationToken ct = default)
        {
            response.Headers["Content-Type"] = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";
            await response.Body.FlushAsync(ct);

            var client = new AzureOpenAIClient(new Uri(Credentials.Endpoint), new ApiKeyCredential(Credentials.ApiKey));
            var agent = client.GetChatClient(Credentials.Model).CreateAIAgent();

            var steps = new[]
            {
                new WorkflowStep("step1", "Explica qué es concurrencia en 2 frases."),
                new WorkflowStep("step2", "Da un ejemplo de procesamiento paralelo.")
            };

            await WriteSseAsync(response, "workflow_started", new { count = steps.Length, mode = "parallel" }, ct);

            // Ejecutar pasos en paralelo y emitir SSE por cada uno
            var tasks = steps.Select(async step =>
            {
                await WriteSseAsync(response, "step_started", new { stepId = step.Id }, ct);
                await foreach (var update in agent.RunStreamingAsync(step.Prompt).WithCancellation(ct))
                {
                    var text = update?.ToString();
                    if (!string.IsNullOrEmpty(text))
                        await WriteSseAsync(response, "token", new { stepId = step.Id, text }, ct);
                }
                await WriteSseAsync(response, "step_completed", new { stepId = step.Id }, ct);
            });

            await Task.WhenAll(tasks);
            await WriteSseAsync(response, "workflow_completed", new { }, ct);
        }

        private static async Task WriteSseAsync(HttpResponse response, string type, object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new { type, data = payload, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            await response.WriteAsync(json + "\n\n", ct);
            await response.Body.FlushAsync(ct);
        }
    }
}
