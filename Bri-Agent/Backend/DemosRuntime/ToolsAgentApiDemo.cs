using Microsoft.AspNetCore.Routing;
using BriAgent.Backend.DemosRuntime;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using System.ClientModel;
using System.ComponentModel;
using BriAgent.Backend.Controllers;
using BriAgent.Backend.Models;
using BriAgent.Backend.Config;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace BriAgent.Backend.DemosRuntime
{
    public class ToolsAgentApiDemo : IApiDemo
    {
        public string Id => "tools-agent";
        public string Title => "Agente con Herramientas (Function Calling)";
    public string Description => "Demuestra un agente que puede usar múltiples herramientas (function calling simulado) y muestra un rastro de invocaciones.";
    public IEnumerable<string> Tags => new[] { "agente", "tools", "function-calling", "demo" };
    public IEnumerable<string> SourceFiles => new[] { "DemosRuntime/ToolsAgentApiDemo.cs", "Controllers/AgentsController.cs" };

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/bri-agent/demos/tools-agent/run", async (
                HttpContext httpContext,
                [FromBody] ToolsRunRequest? request
            ) =>
            {
                var prompt = request?.prompt ?? "Dame el clima en Madrid, conviértelo a USD si cuesta algo, y resume: 'https://example.com'.";
                using var activity = Telemetry.ActivitySource.StartActivity("demo.tools.run");

                // Simulación determinista (sin dependencias runtime de agents) para que la demo /demos/... siempre muestre tools
                var tools = new List<Func<string, ToolInvocationResult>>()
                {
                    ToolCatalog.ClimateTool,
                    ToolCatalog.CurrencyTool,
                    ToolCatalog.SummaryTool,
                    ToolCatalog.WorldTimeTool,
                    ToolCatalog.SentimentTool
                };

                // Filtrar por selección del usuario si llega en la petición
                var selected = (request?.tools ?? Array.Empty<string>()).Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
                if (selected.Count > 0)
                {
                    tools = tools.Where(f => selected.Contains(ToolCatalog.GetName(f))).ToList();
                }

                var used = new List<object>();
                string aggregateAnswer = "";
                foreach (var tool in tools)
                {
                    var result = tool(prompt);
                    if (result.Invoked)
                    {
                        used.Add(new { tool = result.Name, args = result.Args, output = result.Output });
                        aggregateAnswer += result.Output + "\n";
                    }
                }
                if (string.IsNullOrWhiteSpace(aggregateAnswer))
                {
                    aggregateAnswer = "(No se invocaron tools; prompt no coincidió con patrones esperados)";
                }

                var ui = new UiProfile(
                    mode: "tools",
                    stream: false,
                    history: false,
                    recommendedView: "ToolsViewer",
                    capabilities: new[] { "tools", "multi-tool" }
                );
                var meta = new UiMeta(
                    version: "v1",
                    demoId: Id,
                    controller: nameof(ToolsAgentApiDemo),
                    ui: ui
                );

                var catalog = ToolCatalog.ToolsMetadata;

                var answerFinal = aggregateAnswer.Trim();
                return Results.Ok(new {
                    prompt,
                    answer = answerFinal,
                    response = answerFinal, // alias para consistencia con otros paneles
                    toolsUsed = used,
                    availableTools = catalog,
                    meta
                });
            });
        }
    }

    // Result object for a tool invocation
    public readonly record struct ToolInvocationResult(bool Invoked, string Name, object? Args, string Output);

    public sealed class ToolsRunRequest
    {
        public string? prompt { get; set; }
        public string[]? tools { get; set; }
    }

    public static class ToolCatalog
    {
        public sealed class ToolDef
        {
            public string Name { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public Func<string, ToolInvocationResult> Impl { get; init; } = _ => default;
        }

        // Metadatos para frontend
        public static readonly object[] ToolsMetadata = new object[] {
            new { name = "climate", description = "Obtiene clima actual sintético para una ciudad." },
            new { name = "currency", description = "Convierte montos EUR→USD con tasa fija demo." },
            new { name = "summary", description = "Resume contenido textual (simulado)." },
            new { name = "worldtime", description = "Devuelve hora local aproximada de una región." },
            new { name = "sentiment", description = "Analiza sentimiento de un fragmento de texto." }
        };

        public static string GetName(Func<string, ToolInvocationResult> f)
        {
            if (f == ClimateTool) return "climate";
            if (f == CurrencyTool) return "currency";
            if (f == SummaryTool) return "summary";
            if (f == WorldTimeTool) return "worldtime";
            if (f == SentimentTool) return "sentiment";
            return "unknown";
        }

        public static ToolInvocationResult ClimateTool(string prompt)
        {
            if (!prompt.Contains("clima", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("weather", StringComparison.OrdinalIgnoreCase))
                return new(false, "climate", null, string.Empty);
            var city = ExtractWordAfter(prompt, "en") ?? "ciudad-desconocida";
            var output = $"[climate] {city}: Soleado 22C";
            return new(true, "climate", new { city }, output);
        }

        public static ToolInvocationResult CurrencyTool(string prompt)
        {
            if (!prompt.Contains("convert", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("USD", StringComparison.OrdinalIgnoreCase))
                return new(false, "currency", null, string.Empty);
            // Búsqueda simple de número
            var amount = 100m; // demo fija
            var rate = 1.08m;
            var output = $"[currency] {amount} EUR -> {(amount * rate):F2} USD (tasa demo {rate})";
            return new(true, "currency", new { amount, rate }, output);
        }

        public static ToolInvocationResult SummaryTool(string prompt)
        {
            if (!prompt.Contains("resume", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("resumen", StringComparison.OrdinalIgnoreCase))
                return new(false, "summary", null, string.Empty);
            var output = "[summary] Contenido resumido en una frase sintética.";
            return new(true, "summary", null, output);
        }

        public static ToolInvocationResult WorldTimeTool(string prompt)
        {
            if (!prompt.Contains("hora", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("time", StringComparison.OrdinalIgnoreCase))
                return new(false, "worldtime", null, string.Empty);
            var region = ExtractWordAfter(prompt, "en") ?? "UTC";
            var now = DateTimeOffset.UtcNow;
            var output = $"[worldtime] {region}: {now:HH:mm}Z (demo)";
            return new(true, "worldtime", new { region }, output);
        }

        public static ToolInvocationResult SentimentTool(string prompt)
        {
            if (!prompt.Contains("sentimiento", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("sentiment", StringComparison.OrdinalIgnoreCase))
                return new(false, "sentiment", null, string.Empty);
            var sentiment = prompt.Contains("malo", StringComparison.OrdinalIgnoreCase) ? "negativo" : "positivo";
            var score = sentiment == "positivo" ? 0.91 : 0.12;
            var output = $"[sentiment] {sentiment} (score {score:F2})";
            return new(true, "sentiment", new { sentiment, score }, output);
        }

        private static string? ExtractWordAfter(string text, string marker)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], marker, StringComparison.OrdinalIgnoreCase))
                {
                    return new string(parts[i + 1].Where(char.IsLetter).ToArray());
                }
            }
            return null;
        }
    }
}