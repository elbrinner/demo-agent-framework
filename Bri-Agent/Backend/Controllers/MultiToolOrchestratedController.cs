using Microsoft.AspNetCore.Mvc;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using BriAgent.Backend.Models;
using BriAgent.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel; // DescriptionAttribute

namespace BriAgent.Backend.Controllers;

/// <summary>
/// Demo 2: Orquestación Explícita para multi-tool (determinista y paralelo).
/// - Detecta todas las tools necesarias con heurísticas (regex/keywords).
/// - Ejecuta las tools relevantes en paralelo con Task.WhenAll.
/// - Construye un contexto con los resultados.
/// - Llama al modelo UNA sola vez para integrar y redactar la respuesta final.
/// - Garantiza multi-tool sin depender del LLM; controla el orden y evita olvidos.
/// Endpoint: POST /bri-agent/agents/tools-orchestrated
/// </summary>
[Route("bri-agent/agents")]
public class MultiToolOrchestratedController : BaseAgentController
{
    /// <summary>
    /// Cuerpo de la petición: pregunta libre y selección opcional de tools (nombres amistosos: climate, currency, summary, worldtime, sentiment, dish)
    /// </summary>
    public record ToolRequest(string? question, string? city, string[]? tools);

    /// <summary>
    /// Resultado de ejecución heurística local.
    /// </summary>
    public readonly record struct ToolInvocationResult(bool Invoked, string Name, object? Args, string Output);

    [HttpPost("tools-orchestrated")]
    public async Task<IActionResult> ToolsOrchestrated([FromBody] ToolRequest req)
    {
        var question = string.IsNullOrWhiteSpace(req.question)
            ? "Dame el clima en Madrid, convierte 100 EUR a USD, resume: 'https://example.com', qué hora es en Bogotá y analiza el sentimiento de 'me siento genial'."
            : req.question!;

        // Catálogo amistoso (para UI y detección heurística)
        var catalog = new (string name, string description, Func<string, ToolInvocationResult> impl)[]
        {
            ("climate",   "Obtiene clima actual sintético para una ciudad.", ClimateTool),
            ("currency",  "Convierte montos EUR→USD con tasa fija demo.",   CurrencyTool),
            ("summary",   "Resume contenido textual (simulado).",            SummaryTool),
            ("worldtime", "Devuelve hora local aproximada de una región.",   WorldTimeTool),
            ("sentiment", "Analiza sentimiento de un fragmento de texto.",   SentimentTool),
            ("dish",      "Recomienda un plato típico por ciudad.",         DishTool)
        };

        // Selección opcional de tools
        var selectedFriendly = (req.tools ?? Array.Empty<string>()).Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
        var activeFriendly = selectedFriendly.Count > 0 ? catalog.Where(c => selectedFriendly.Contains(c.name)).ToArray() : catalog;

        // Config Azure OpenAI
        var endpoint = BriAgent.Backend.Config.Credentials.Endpoint;
        var apiKey = BriAgent.Backend.Config.Credentials.ApiKey;
        var model = BriAgent.Backend.Config.Credentials.Model;
        var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        // Paso 1: Detectar todas las tools necesarias (heurística determinista)
        var needed = activeFriendly.Select(c => c.impl(question)).Where(r => r.Invoked).ToList();

        if (needed.Count == 0)
        {
            var errorMeta = BuildMeta(
                demoId: "tools-orchestrated",
                controller: nameof(MultiToolOrchestratedController),
                profile: new UiProfile(
                    mode: "tools",
                    stream: false,
                    history: false,
                    tools: activeFriendly.Select(t => t.name),
                    recommendedView: "ToolsViewer",
                    capabilities: new[] { "tools", "multi-tool", "orchestration" }
                )
            );
            var errorAvailableTools = catalog.Select(c => new { name = c.name, description = c.description });
            return Ok(new { question, answer = "No se detectaron herramientas relevantes para esta consulta.", response = "No se detectaron herramientas relevantes para esta consulta.", toolsUsed = new List<object>(), availableTools = errorAvailableTools, meta = errorMeta });
        }

        // Paso 2: Ejecutar las tools detectadas en paralelo
        var tasks = needed.Select(async r =>
        {
            // Simular ejecución asíncrona (las heurísticas son síncronas, pero en producción podrían ser async)
            await Task.Yield(); // Para simular paralelismo
            return new { tool = r.Name, source = "orchestration", args = r.Args, output = r.Output };
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Paso 3: Construir contexto con los resultados
        var contextLines = results.Select(r => $"{r.tool.ToUpper()}: {r.output}").ToArray();
        var context = string.Join("\n", contextLines);

        // Paso 4: Llamar al modelo UNA sola vez para integrar y redactar respuesta final
        var integrationPrompt = $@"
Contexto de herramientas ejecutadas:
{context}

Pregunta original: {question}

Elabora una respuesta integrada en español, utilizando los resultados de las herramientas sin alterar los datos duros. Redacta de manera clara y natural.
";

        var agent = client.GetChatClient(model).CreateAIAgent(
            instructions: "Responde basado en el contexto proporcionado, integrando los resultados de las herramientas.",
            tools: Array.Empty<AIFunction>()
        );
        var finalResp = await agent.RunAsync(integrationPrompt);
        var answer = finalResp.Text ?? string.Empty;

        // Meta UI
        var meta = BuildMeta(
            demoId: "tools-orchestrated",
            controller: nameof(MultiToolOrchestratedController),
            profile: new UiProfile(
                mode: "tools",
                stream: false,
                history: false,
                tools: activeFriendly.Select(t => t.name),
                recommendedView: "ToolsViewer",
                capabilities: new[] { "tools", "multi-tool", "orchestration" }
            )
        );

        var availableTools = catalog.Select(c => new { name = c.name, description = c.description });
        return Ok(new { question, answer, response = answer, toolsUsed = results, availableTools, meta });
    }

    // === Heurísticas de detección (idénticas a ToolsAgentController para consistencia) ===
    private static ToolInvocationResult ClimateTool(string prompt)
    {
        if (!prompt.Contains("clima", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("tiempo", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("weather", StringComparison.OrdinalIgnoreCase))
            return new(false, "climate", null, string.Empty);
        var city = ExtractWordAfter(prompt, "en") ?? "ciudad-desconocida";
        return new(true, "climate", new { city }, $"[climate] {city}: Soleado 22C");
    }

    private static ToolInvocationResult CurrencyTool(string prompt)
    {
        if (!prompt.Contains("convierte", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("convert", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("USD", StringComparison.OrdinalIgnoreCase))
            return new(false, "currency", null, string.Empty);
        decimal amount = 100m; decimal rate = 1.08m;
        return new(true, "currency", new { amount, rate }, $"[currency] {amount} EUR -> {(amount * rate):F2} USD (tasa demo {rate})");
    }

    private static ToolInvocationResult SummaryTool(string prompt)
    {
        if (!prompt.Contains("resume", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("resumen", StringComparison.OrdinalIgnoreCase))
            return new(false, "summary", null, string.Empty);
        return new(true, "summary", null, "[summary] Contenido resumido en una frase sintética.");
    }

    private static ToolInvocationResult WorldTimeTool(string prompt)
    {
        if (!prompt.Contains("hora", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("time", StringComparison.OrdinalIgnoreCase))
            return new(false, "worldtime", null, string.Empty);
        var region = ExtractWordAfter(prompt, "en") ?? "UTC";
        return new(true, "worldtime", new { region }, $"[worldtime] {region}: {DateTimeOffset.UtcNow:HH:mm}Z (demo)");
    }

    private static ToolInvocationResult SentimentTool(string prompt)
    {
        if (!prompt.Contains("sentimiento", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("sentiment", StringComparison.OrdinalIgnoreCase))
            return new(false, "sentiment", null, string.Empty);
        var negative = prompt.Contains("malo", StringComparison.OrdinalIgnoreCase) || prompt.Contains("triste", StringComparison.OrdinalIgnoreCase);
        var sentiment = negative ? "negativo" : "positivo"; var score = negative ? 0.12 : 0.91;
        return new(true, "sentiment", new { sentiment, score }, $"[sentiment] {sentiment} (score {score:F2})");
    }

    private static ToolInvocationResult DishTool(string prompt)
    {
        if (!prompt.Contains("plato", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("comida", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("dish", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("comer", StringComparison.OrdinalIgnoreCase))
            return new(false, "dish", null, string.Empty);
        var city = ExtractWordAfter(prompt, "en") ?? "ciudad";
        var recomendacion = city.ToLower() switch
        {
            "madrid" => "Cocido madrileño",
            "lisboa" => "Bacalhau à Brás",
            "paris" or "parís" => "Coq au vin",
            _ => $"No tengo datos sobre platos típicos en {city}."
        };
        return new(true, "dish", new { city }, $"[dish] Recomendación para {city}: {recomendacion}");
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