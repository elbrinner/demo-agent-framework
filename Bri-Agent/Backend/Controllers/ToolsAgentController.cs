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

[Route("bri-agent/agents")] // mismo prefijo
public class ToolsAgentController : BaseAgentController
{
    /// <summary>
    /// Cuerpo de la petición para <c>POST /bri-agent/agents/tools</c>.
    /// Permite enviar una pregunta libre y, opcionalmente, limitar qué tools se deben considerar.
    /// </summary>
    public record ToolRequest(string? question, string? city, string[]? tools, string? threadId);

    /// <summary>
    /// Resultado de la ejecución de una tool: indica si se invocó, su nombre, argumentos usados y salida textual.
    /// </summary>
    public readonly record struct ToolInvocationResult(bool Invoked, string Name, object? Args, string Output);

    private readonly AgentRunner _runner;
    public ToolsAgentController(AgentRunner runner)
    {
        _runner = runner;
    }

    [HttpPost("tools")] // POST /bri-agent/agents/tools
    /// <summary>
    /// Endpoint que simula invocación de múltiples herramientas (function calling) según el contenido del prompt.
    /// Devuelve la respuesta agregada, el rastro de invocaciones (<c>toolsUsed</c>), el catálogo de tools y <c>meta.ui</c>.
    /// </summary>
    public async Task<IActionResult> Tools([FromBody] ToolRequest req)
    {
        var question = string.IsNullOrWhiteSpace(req.question) ? "Dame el clima en Madrid, convierte 100 EUR a USD, resume: 'https://example.com', qué hora es en Bogotá y analiza el sentimiento de 'me siento genial'." : req.question!;

        // Catálogo de tools disponibles (nombres "amistosos" usados por el frontend)
        var catalog = new (string name, string description, Func<string, ToolInvocationResult> impl)[]
        {
            ("climate",   "Obtiene clima actual sintético para una ciudad.", ClimateTool),
            ("currency",  "Convierte montos EUR→USD con tasa fija demo.",   CurrencyTool),
            ("summary",   "Resume contenido textual (simulado).",            SummaryTool),
            ("worldtime", "Devuelve hora local aproximada de una región.",   WorldTimeTool),
            ("sentiment", "Analiza sentimiento de un fragmento de texto.",   SentimentTool),
            ("dish",      "Recomienda un plato típico por ciudad.",         DishTool)
        };

        // Selección opcional de tools por el usuario (si no se especifican, se usan todas)
        var selectedFriendly = (req.tools ?? Array.Empty<string>()).Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
        var activeFriendly = selectedFriendly.Count > 0 ? catalog.Where(c => selectedFriendly.Contains(c.name)).ToArray() : catalog;

        // Implementación basada en Agent Framework: registramos tools reales con AIFunctionFactory y dejamos que el modelo decida.
    var endpoint = BriAgent.Backend.Config.Credentials.Endpoint;
    var apiKey = BriAgent.Backend.Config.Credentials.ApiKey;
    var model = BriAgent.Backend.Config.Credentials.Model;
    var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var used = new List<object>();

        // Mapeo entre nombre "amistoso" y el nombre del delegado local (para filtrar correctamente)
        var friendlyToDelegate = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["climate"] = nameof(ObtenerClima),
            ["currency"] = nameof(ConvertirMoneda),
            ["summary"] = nameof(Resumir),
            ["worldtime"] = nameof(HoraEn),
            ["sentiment"] = nameof(AnalizarSentimiento),
            ["dish"] = nameof(RecomendarPlato)
        };

        // Definición de tools como funciones locales para poder capturar invocaciones en 'used'
        [System.ComponentModel.Description("Obtiene el clima para una ciudad dada.")]
        string ObtenerClima([System.ComponentModel.Description("La ciudad para consultar el clima.")] string ciudad)
        {
            var output = $"El clima en {ciudad} es nublado con una máxima de 15°C.";
            used.Add(new { tool = "climate", args = new { ciudad }, output });
            return output;
        }

        [Description("Convierte un monto entre monedas (demo fija EUR→USD).")]
        string ConvertirMoneda([Description("Cantidad a convertir")] decimal cantidad, [Description("Moneda origen")] string from, [Description("Moneda destino")] string to)
        {
            var rate = 1.08m; var value = from.Equals("EUR", StringComparison.OrdinalIgnoreCase) && to.Equals("USD", StringComparison.OrdinalIgnoreCase) ? cantidad * rate : cantidad;
            var output = $"{cantidad} {from} -> {value:F2} {to} (tasa demo {rate})";
            used.Add(new { tool = "currency", args = new { cantidad, from, to }, output });
            return output;
        }

        [System.ComponentModel.Description("Resume un texto o URL (simulado)")]
        string Resumir([System.ComponentModel.Description("Texto o URL a resumir")] string texto)
        {
            var output = "Resumen en una frase sintética (demo).";
            used.Add(new { tool = "summary", args = new { texto }, output });
            return output;
        }

        [System.ComponentModel.Description("Da la hora aproximada de una región (UTC demo)")]
        string HoraEn([System.ComponentModel.Description("Región o ciudad")] string region)
        {
            var output = $"Hora en {region}: {DateTimeOffset.UtcNow:HH:mm}Z (demo)";
            used.Add(new { tool = "worldtime", args = new { region }, output });
            return output;
        }

        [System.ComponentModel.Description("Analiza el sentimiento de un texto (heurístico)")]
        string AnalizarSentimiento([System.ComponentModel.Description("Texto a analizar")] string texto)
        {
            var negative = texto.Contains("malo", StringComparison.OrdinalIgnoreCase) || texto.Contains("triste", StringComparison.OrdinalIgnoreCase);
            var sentiment = negative ? "negativo" : "positivo"; var score = negative ? 0.12 : 0.91;
            var output = $"{sentiment} (score {score:F2})";
            used.Add(new { tool = "sentiment", args = new { texto }, output });
            return output;
        }

        [System.ComponentModel.Description("Recomienda un plato típico por ciudad")]
        string RecomendarPlato([System.ComponentModel.Description("Ciudad para recomendar comida")] string ciudad)
        {
            var rec = ciudad.ToLower() switch
            {
                "madrid" => "Cocido madrileño",
                "lisboa" => "Bacalhau à Brás",
                "paris" or "parís" => "Coq au vin",
                _ => $"No tengo datos sobre platos típicos en {ciudad}."
            };
            var output = rec;
            used.Add(new { tool = "dish", args = new { ciudad }, output });
            return output;
        }

        // Armar lista de tools y filtrar por selección del usuario si corresponde
        var available = new (string name, string description, Delegate impl)[]
        {
            (nameof(ObtenerClima), "Clima por ciudad", (Func<string, string>)ObtenerClima),
            (nameof(ConvertirMoneda), "Conversión de moneda (demo)", (Func<decimal, string, string, string>)ConvertirMoneda),
            (nameof(Resumir), "Resumen de texto/URL (demo)", (Func<string, string>)Resumir),
            (nameof(HoraEn), "Hora mundial (demo)", (Func<string, string>)HoraEn),
            (nameof(AnalizarSentimiento), "Análisis de sentimiento (demo)", (Func<string, string>)AnalizarSentimiento),
            (nameof(RecomendarPlato), "Plato típico por ciudad", (Func<string, string>)RecomendarPlato)
        };

        var namesSelected = (req.tools ?? Array.Empty<string>()).Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedDelegateNames = namesSelected.Count > 0
            ? namesSelected.Select(f => friendlyToDelegate.TryGetValue(f, out var dn) ? dn : null)
                           .Where(dn => dn != null)
                           .Select(dn => dn!)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = selectedDelegateNames.Count > 0 ? available.Where(t => selectedDelegateNames.Contains(t.name)).ToArray() : available;

        var aiTools = active.Select(t => AIFunctionFactory.Create(t.impl)).ToArray();
        var agent = client.GetChatClient(model).CreateAIAgent(
            instructions: "Eres un asistente útil que puede invocar herramientas para resolver la tarea.",
            tools: aiTools
        );

        // Soporte de conversación con memoria por thread opcional
        Microsoft.Agents.AI.AgentThread? thread = null;
        string? threadId = req.threadId;
        if (!string.IsNullOrWhiteSpace(threadId))
        {
            var ctx = BriAgent.Backend.Services.ThreadStore.GetOrCreateAgentContext(threadId!, agent);
            thread = ctx as Microsoft.Agents.AI.AgentThread;
        }
        else
        {
            // Si el cliente no manda threadId, creamos uno para permitir continuidad opcional
            thread = agent.GetNewThread();
            threadId = Guid.NewGuid().ToString("N");
        }

        // Ejecutar con cancelación si el cliente cierra
    var ct = HttpContext.RequestAborted;
    var resp = await _runner.RunAsync(agent, question, thread: thread, cancellationToken: ct, model: model, agentType: "tools-agent");
    var answer = resp.Text ?? string.Empty;

        // Fallback determinista: si el modelo no invocó ninguna tool, intentamos las heurísticas locales
        if (used.Count == 0)
        {
            var fallbacks = activeFriendly.Select(c => c.impl(question)).Where(r => r.Invoked).ToList();
            if (fallbacks.Count > 0)
            {
                foreach (var r in fallbacks)
                {
                    used.Add(new { tool = r.Name, args = r.Args, output = r.Output });
                }
                if (string.IsNullOrWhiteSpace(answer))
                {
                    answer = string.Join("\n", fallbacks.Select(r => r.Output));
                }
            }
        }

        // Perfil UI para que el frontend renderice la vista de tools
        var meta = BuildMeta(
            demoId: "tools-agent",
            controller: nameof(ToolsAgentController),
            profile: new UiProfile(
                mode: "tools",
                stream: false,
                history: false,
                tools: activeFriendly.Select(t => t.name),
                recommendedView: "ToolsViewer",
                capabilities: new[] { "tools", "multi-tool" }
            )
        );

        // Telemetría ya registrada por AgentRunner

        // Enviar catálogo amistoso al frontend y el threadId generado/recibido
        var availableTools = catalog.Select(c => new { name = c.name, description = c.description });
        return Ok(new { question, answer, response = answer, toolsUsed = used, availableTools, threadId, meta });
    }

    // === Implementaciones de tools demo ===
    /// <summary>
    /// Tool de clima: si detecta palabras clave ("clima" o "weather"),
    /// extrae la ciudad tras la palabra "en" y devuelve un clima sintético.
    /// </summary>
    private static ToolInvocationResult ClimateTool(string prompt)
    {
        if (!prompt.Contains("clima", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("tiempo", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("weather", StringComparison.OrdinalIgnoreCase))
            return new(false, "climate", null, string.Empty);
        var city = ExtractWordAfter(prompt, "en") ?? "ciudad-desconocida";
        return new(true, "climate", new { city }, $"[climate] {city}: Soleado 22C");
    }

    /// <summary>
    /// Tool de conversión: si detecta "convierte"/"convert"/"USD", convierte un monto EUR→USD a una tasa fija demo.
    /// </summary>
    private static ToolInvocationResult CurrencyTool(string prompt)
    {
        if (!prompt.Contains("convierte", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("convert", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("USD", StringComparison.OrdinalIgnoreCase))
            return new(false, "currency", null, string.Empty);
        decimal amount = 100m; decimal rate = 1.08m;
        return new(true, "currency", new { amount, rate }, $"[currency] {amount} EUR -> {(amount * rate):F2} USD (tasa demo {rate})");
    }

    /// <summary>
    /// Tool de resumen: si detecta "resume"/"resumen", devuelve un resumen sintético de una frase.
    /// </summary>
    private static ToolInvocationResult SummaryTool(string prompt)
    {
        if (!prompt.Contains("resume", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("resumen", StringComparison.OrdinalIgnoreCase))
            return new(false, "summary", null, string.Empty);
        return new(true, "summary", null, "[summary] Contenido resumido en una frase sintética.");
    }

    /// <summary>
    /// Tool de hora mundial: si detecta "hora"/"time", extrae la región tras "en" y devuelve una hora aproximada (UTC demo).
    /// </summary>
    private static ToolInvocationResult WorldTimeTool(string prompt)
    {
        if (!prompt.Contains("hora", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("time", StringComparison.OrdinalIgnoreCase))
            return new(false, "worldtime", null, string.Empty);
        var region = ExtractWordAfter(prompt, "en") ?? "UTC";
        return new(true, "worldtime", new { region }, $"[worldtime] {region}: {DateTimeOffset.UtcNow:HH:mm}Z (demo)");
    }

    /// <summary>
    /// Tool de sentimiento: heurística simple; si detecta palabras negativas ("malo", "triste") marca negativo, de lo contrario positivo.
    /// </summary>
    private static ToolInvocationResult SentimentTool(string prompt)
    {
        if (!prompt.Contains("sentimiento", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("sentiment", StringComparison.OrdinalIgnoreCase))
            return new(false, "sentiment", null, string.Empty);
        var negative = prompt.Contains("malo", StringComparison.OrdinalIgnoreCase) || prompt.Contains("triste", StringComparison.OrdinalIgnoreCase);
        var sentiment = negative ? "negativo" : "positivo"; var score = negative ? 0.12 : 0.91;
        return new(true, "sentiment", new { sentiment, score }, $"[sentiment] {sentiment} (score {score:F2})");
    }

    /// <summary>
    /// Tool de recomendación de plato típico: si detecta "plato"/"comida"/"dish",
    /// extrae la ciudad tras "en" y devuelve una sugerencia simple por mapa estático.
    /// </summary>
    private static ToolInvocationResult DishTool(string prompt)
    {
        if (!prompt.Contains("plato", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("comida", StringComparison.OrdinalIgnoreCase) && !prompt.Contains("dish", StringComparison.OrdinalIgnoreCase))
            return new(false, "dish", null, string.Empty);
        var city = ExtractWordAfter(prompt, "en") ?? "ciudad";
        var recomendacion = city.ToLower() switch {
            "madrid" => "Cocido madrileño",
            "lisboa" => "Bacalhau à Brás",
            "paris" or "parís" => "Coq au vin",
            _ => $"No tengo datos sobre platos típicos en {city}."
        };
        return new(true, "dish", new { city }, $"[dish] Recomendación para {city}: {recomendacion}");
    }

    /// <summary>
    /// Utilidad: devuelve la palabra (solo letras) inmediatamente posterior a <paramref name="marker"/> en el texto, si existe.
    /// </summary>
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
