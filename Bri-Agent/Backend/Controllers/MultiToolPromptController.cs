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
using System.ComponentModel; // DescriptionAttribute

namespace BriAgent.Backend.Controllers;

/// <summary>
/// Demo 1: Prompt Engineering Mejorado para multi-tool (function calling automático).
/// - Refuerza en las instrucciones del agente que debe identificar TODAS las tools relevantes
///   y ejecutarlas antes de redactar la respuesta final.
/// - Mantiene un fallback heurístico por seguridad si el LLM no llama herramientas.
/// - Devuelve el rastro de invocaciones indicando source = "llm" o "fallback".
/// Endpoint: POST /bri-agent/agents/tools-prompt
/// </summary>
[Route("bri-agent/agents")]
public class MultiToolPromptController : BaseAgentController
{
    /// <summary>
    /// Cuerpo de la petición: pregunta libre y selección opcional de tools (nombres amistosos: climate, currency, summary, worldtime, sentiment, dish)
    /// </summary>
    public record ToolRequest(string? question, string? city, string[]? tools);

    /// <summary>
    /// Resultado de ejecución heurística local (para fallback).
    /// </summary>
    public readonly record struct ToolInvocationResult(bool Invoked, string Name, object? Args, string Output);

    [HttpPost("tools-prompt")]
    public async Task<IActionResult> ToolsPrompt([FromBody] ToolRequest req)
    {
        var question = string.IsNullOrWhiteSpace(req.question)
            ? "Dame el clima en Madrid, convierte 100 EUR a USD, resume: 'https://example.com', qué hora es en Bogotá y analiza el sentimiento de 'me siento genial'."
            : req.question!;

        // Catálogo amistoso (para UI y fallback heurístico)
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

        var used = new List<object>();

        // Tools locales (registradas para function calling). Cada una registra source = "llm".
        [Description("Obtiene el clima para una ciudad dada.")]
        string ObtenerClima([Description("La ciudad para consultar el clima.")] string ciudad)
        {
            var output = $"El clima en {ciudad} es nublado con una máxima de 15°C.";
            used.Add(new { tool = "climate", source = "llm", args = new { ciudad }, output });
            return output;
        }

        [Description("Convierte un monto entre monedas (demo fija EUR→USD).")]
        string ConvertirMoneda([Description("Cantidad a convertir")] decimal cantidad, [Description("Moneda origen")] string from, [Description("Moneda destino")] string to)
        {
            var rate = 1.08m;
            var value = from.Equals("EUR", StringComparison.OrdinalIgnoreCase) && to.Equals("USD", StringComparison.OrdinalIgnoreCase) ? cantidad * rate : cantidad;
            var output = $"{cantidad} {from} -> {value:F2} {to} (tasa demo {rate})";
            used.Add(new { tool = "currency", source = "llm", args = new { cantidad, from, to }, output });
            return output;
        }

        [Description("Resume un texto o URL (simulado)")]
        string Resumir([Description("Texto o URL a resumir")] string texto)
        {
            var output = "Resumen en una frase sintética (demo).";
            used.Add(new { tool = "summary", source = "llm", args = new { texto }, output });
            return output;
        }

        [Description("Da la hora aproximada de una región (UTC demo)")]
        string HoraEn([Description("Región o ciudad")] string region)
        {
            var output = $"Hora en {region}: {DateTimeOffset.UtcNow:HH:mm}Z (demo)";
            used.Add(new { tool = "worldtime", source = "llm", args = new { region }, output });
            return output;
        }

        [Description("Analiza el sentimiento de un texto (heurístico)")]
        string AnalizarSentimiento([Description("Texto a analizar")] string texto)
        {
            var negative = texto.Contains("malo", StringComparison.OrdinalIgnoreCase) || texto.Contains("triste", StringComparison.OrdinalIgnoreCase);
            var sentiment = negative ? "negativo" : "positivo"; var score = negative ? 0.12 : 0.91;
            var output = $"{sentiment} (score {score:F2})";
            used.Add(new { tool = "sentiment", source = "llm", args = new { texto }, output });
            return output;
        }

        [Description("Recomienda un plato típico por ciudad")]
        string RecomendarPlato([Description("Ciudad para recomendar comida")] string ciudad)
        {
            var rec = ciudad.ToLower() switch
            {
                "madrid" => "Cocido madrileño",
                "lisboa" => "Bacalhau à Brás",
                "paris" or "parís" => "Coq au vin",
                _ => $"No tengo datos sobre platos típicos en {ciudad}."
            };
            var output = rec;
            used.Add(new { tool = "dish", source = "llm", args = new { ciudad }, output });
            return output;
        }

        // Catálogo de funciones disponibles para el agente (filtradas por selección si corresponde)
        var available = new (string name, string description, Delegate impl)[]
        {
            (nameof(ObtenerClima), "Clima por ciudad", (Func<string, string>)ObtenerClima),
            (nameof(ConvertirMoneda), "Conversión de moneda (demo)", (Func<decimal, string, string, string>)ConvertirMoneda),
            (nameof(Resumir), "Resumen de texto/URL (demo)", (Func<string, string>)Resumir),
            (nameof(HoraEn), "Hora mundial (demo)", (Func<string, string>)HoraEn),
            (nameof(AnalizarSentimiento), "Análisis de sentimiento (demo)", (Func<string, string>)AnalizarSentimiento),
            (nameof(RecomendarPlato), "Plato típico por ciudad", (Func<string, string>)RecomendarPlato)
        };

        var active = available;

        var aiTools = active.Select(t => AIFunctionFactory.Create(t.impl)).ToArray();

        var reinforcedInstructions = string.Join('\n', new[]
        {
            "Eres un asistente que primero identifica TODAS las herramientas relevantes (climate, currency, summary, worldtime, sentiment, dish)",
            "y las invoca (una por cada necesidad detectada) antes de redactar la respuesta final.",
            "No inventes datos que una tool puede proveer. Utiliza TODAS las herramientas relevantes antes de responder.",
            "Integra y redacta en español una respuesta clara utilizando las salidas de las tools."
        });

        var agent = AgentFactory.CreateBasicAgent(reinforcedInstructions, aiTools);

        var resp = await agent.RunAsync(question);
        var answer = resp.Text ?? string.Empty;

        // Fallback: si el LLM no invocó ninguna tool, usar heurística local (source = "fallback")
        if (used.Count == 0)
        {
            var fallbacks = activeFriendly.Select(c => c.impl(question)).Where(r => r.Invoked).ToList();
            if (fallbacks.Count > 0)
            {
                foreach (var r in fallbacks)
                {
                    used.Add(new { tool = r.Name, source = "fallback", args = r.Args, output = r.Output });
                }
                if (string.IsNullOrWhiteSpace(answer))
                {
                    answer = string.Join("\n", fallbacks.Select(r => r.Output));
                }
            }
        }

        // Meta UI
        var meta = BuildMeta(
            demoId: "tools-prompt",
            controller: nameof(MultiToolPromptController),
            profile: new UiProfile(
                mode: "tools",
                stream: false,
                history: false,
                tools: activeFriendly.Select(t => t.name),
                recommendedView: "ToolsViewer",
                capabilities: new[] { "tools", "multi-tool", "prompt-engineering" }
            )
        );

        var availableTools = catalog.Select(c => new { name = c.name, description = c.description });
        return Ok(new { question, answer, response = answer, toolsUsed = used, availableTools, meta });
    }

    // === Heurísticas de fallback (idénticas a ToolsAgentController para consistencia) ===
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
