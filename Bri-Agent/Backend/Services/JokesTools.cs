using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;
using BriAgent.Backend.Config;

namespace BriAgent.Backend.Services;

/// <summary>
/// Herramientas AIFunction para el caso de uso de chistes. generate_joke usa un agente REAL (Azure OpenAI).
/// </summary>
public static class JokesTools
{
    [Description("Genera un chiste corto sobre un tema. Entrada: { topic?: string }. Salida: { text: string }")]
    public static async Task<object> generate_joke([Description("Tema opcional")] string? topic = null)
    {
        // Crear un agente real (Azure OpenAI vía Agent Framework) para generar un chiste de programación
        var factory = ServiceLocator.Get<IAgentFactory>();
        var runner = ServiceLocator.Get<AgentRunner>();

        var agent = factory.CreateBasicAgent(
            instructions: "Eres un comediante experto en programación. Escribe chistes muy cortos, originales y en TEXTO PLANO (sin comillas, sin JSON ni explicaciones). Evita contenido ofensivo.",
            tools: Array.Empty<AIFunction>()
        );

        var sb = new StringBuilder();
        sb.Append("Genera un chiste MUY breve y original de programación");
        if (!string.IsNullOrWhiteSpace(topic))
        {
            sb.Append($" sobre '{topic}'");
        }
        sb.Append(". Devuelve solo el chiste en texto plano, sin comillas, sin prefijos ni explicación.");

        var response = await runner.RunAsync(
            agent,
            sb.ToString(),
            thread: null,
            cancellationToken: default,
            model: Credentials.Model,
            agentType: "tools.generate_joke"
        );

        var text = (response.Text ?? string.Empty).Trim();
        // Limpieza defensiva si accidentalmente viniera JSON con {"text": ...}
        if (text.StartsWith("{") && text.Contains("\"text\""))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    text = (t.GetString() ?? string.Empty).Trim();
                }
            }
            catch { /* ignore */ }
        }

        // Si no hubo texto, devolver vacío para que el caller lo trate como fallo (sin mocks)
        return new { text };
    }

    [Description("Valida y puntúa un chiste. Entrada: { text: string }. Salida: { score: number, rationale: string }")]
    public static async Task<object> validate_joke([Description("Texto del chiste")] string text)
    {
        // Usar LLM para puntuar: devolver solo un número 0..10 y construir una rationale breve
        var factory = ServiceLocator.Get<IAgentFactory>();
        var runner = ServiceLocator.Get<AgentRunner>();
        var agent = factory.CreateBasicAgent(
            instructions: "Eres un crítico de chistes de programación. Puntúa la calidad del chiste del 0 al 10. Devuelve solo un número entero entre 0 y 10, sin texto adicional.",
            tools: Array.Empty<AIFunction>()
        );
        var response = await runner.RunAsync(
            agent,
            $"Evalúa este chiste y devuelve SOLO un número 0..10, sin más texto: '{text}'",
            model: Credentials.Model,
            agentType: "tools.validate_joke"
        );
        var digits = new string((response.Text ?? string.Empty).Where(char.IsDigit).ToArray());
        int score = int.TryParse(digits, out var sc) ? Math.Clamp(sc, 0, 10) : 7;
        string rationale = score >= 9 ? "excelente" : score >= 7 ? "bueno" : "meh";
        return new { score, rationale };
    }

    [Description("Revisión del Jefazo: decide si requiere HITL cuando score es muy alto (>=9). Entrada: { score:int }. Salida: { decision: 'auto'|'hitl' }")]
    public static async Task<object> boss_review([Description("Puntaje del chiste 0..10")] int score)
    {
        // Usar LLM para decidir 'hitl' o 'auto'
        var factory = ServiceLocator.Get<IAgentFactory>();
        var runner = ServiceLocator.Get<AgentRunner>();
        var agent = factory.CreateBasicAgent(
            instructions: "Eres el jefe. Si con el score y el contenido el chiste es excepcional (solo casos 9-10 muy destacables), requiere aprobación humana; si no, auto. Responde exactamente 'hitl' o 'auto' en minúsculas y nada más.",
            tools: Array.Empty<AIFunction>()
        );
        var response = await runner.RunAsync(
            agent,
            $"Con score={score}, decide si requiere aprobación humana. Devuelve exactamente 'hitl' o 'auto', sin más texto.",
            model: Credentials.Model,
            agentType: "tools.boss_review"
        );
        var txt = (response.Text ?? string.Empty).Trim().ToLowerInvariant();
        var decision = txt.Contains("hitl") ? "hitl" : "auto";
        return new { decision };
    }


    [Description("Lista chistes guardados. Entrada: { limit?:int }. Salida: { resources:any[] }")]
    public static async Task<object> list_jokes([Description("Límite máximo a devolver")] int? limit = 20)
    {
        var mcp = ServiceLocator.Get<McpFileSystemService>();
        var list = await mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.Name)
                        .Take(Math.Clamp(limit ?? 20, 1, 100))
                        .ToList();
        return new { resources = jokes };
    }

    [Description("Hace un resumen muy corto de los últimos chistes. Entrada: { limit?:int }. Salida: { summary:string }")]
    public static async Task<object> summarize_jokes([Description("Cuántos chistes")] int? limit = 5)
    {
        var mcp = ServiceLocator.Get<McpFileSystemService>();
        var list = await mcp.ListAsync();
        var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.Name)
                        .Take(Math.Clamp(limit ?? 5, 1, 50))
                        .ToList();
        var lines = new List<string>();
        foreach (var j in jokes)
        {
            try
            {
                var text = await mcp.ReadTextAsync(j.Uri);
                var first = text.Split('\n').FirstOrDefault()?.Trim() ?? "";
                lines.Add($"• {j.Name}: {first}");
            }
            catch { }
        }
        var summary = lines.Count == 0 ? "(sin chistes)" : string.Join(" ", lines);
        return new { summary };
    }

    public static AIFunction[] AsFunctions(IServiceProvider sp)
    {
        // Registrar ServiceLocator para funciones que necesitan DI
        ServiceLocator.SetProvider(sp);
        return new[]
        {
            AIFunctionFactory.Create((Func<string?, Task<object>>)generate_joke),
            AIFunctionFactory.Create((Func<string, Task<object>>)validate_joke),
            AIFunctionFactory.Create((Func<int, Task<object>>)boss_review),
            AIFunctionFactory.Create((Func<string, Task<object>>)moderate_content),
            AIFunctionFactory.Create((Func<int?, Task<object>>)list_jokes),
            AIFunctionFactory.Create((Func<int?, Task<object>>)summarize_jokes),
            // Nuevas tools para índice MCP
            AIFunctionFactory.Create((Func<int?, Task<object>>)index_list),
            AIFunctionFactory.Create((Func<string, int?, Task<object>>)index_search),
            AIFunctionFactory.Create((Func<Task<object>>)index_rebuild),
            // Tools para inspección de checkpoints HITL
            AIFunctionFactory.Create((Func<int?, Task<object>>)checkpoint_list),
            AIFunctionFactory.Create((Func<string, Task<object>>)checkpoint_get)
        };
    }

    [Description("Modera un contenido de texto. Entrada: { text:string }. Salida: { allowed:bool, category?:string, reason?:string }")]
    public static async Task<object> moderate_content([Description("Texto a moderar")] string text)
    {
        var moderation = ServiceLocator.Get<JokesModerationService>();
        var res = await moderation.EvaluateAsync(text);
        return new { allowed = res.Allowed, category = res.Category, reason = res.Reason };
    }

    [Description("Lista entradas del índice de chistes (jokes/index.json). Entrada: { limit?:int }. Salida: { items:any[] }")]
    public static async Task<object> index_list([Description("Límite máximo")] int? limit = 50)
    {
        var index = ServiceLocator.Get<JokesIndexService>();
        var items = await index.ReadIndexAsync();
        return new { items = items.Take(Math.Clamp(limit ?? 50, 1, 500)).ToList() };
    }

    [Description("Busca en el índice por texto (normalizado). Entrada: { query:string, limit?:int }. Salida: { items:any[] }")]
    public static async Task<object> index_search([Description("Texto a buscar")] string query, [Description("Límite")] int? limit = 20)
    {
        var index = ServiceLocator.Get<JokesIndexService>();
        var items = await index.SearchAsync(query, Math.Clamp(limit ?? 20, 1, 200));
        return new { items };
    }

    [Description("Reconstruye el índice leyendo los archivos en jokes/. Salida: { count:int }")]
    public static async Task<object> index_rebuild()
    {
        var index = ServiceLocator.Get<JokesIndexService>();
        var count = await index.RebuildAsync();
        return new { count };
    }

    [Description("Lista snapshots de checkpoint HITL más recientes. Entrada: { limit?:int }. Salida: { items:any[] }")]
    public static async Task<object> checkpoint_list([Description("Límite")] int? limit = 50)
    {
        var cp = ServiceLocator.Get<JokesCheckpointService>();
        var items = await cp.ListAsync(Math.Clamp(limit ?? 50, 1, 500));
        return new { items };
    }

    [Description("Obtiene un snapshot de checkpoint por approvalId. Entrada: { approvalId:string }. Salida: snapshot | null")]
    public static async Task<object> checkpoint_get([Description("approvalId")] string approvalId)
    {
        var cp = ServiceLocator.Get<JokesCheckpointService>();
        var snap = await cp.GetAsync(approvalId);
        return snap is null ? new { found = false } : new { found = true, snapshot = snap };
    }
}

/// <summary>
/// Localizador sencillo para obtener servicios desde métodos estáticos de herramientas.
/// </summary>
internal static class ServiceLocator
{
    private static IServiceProvider? _sp;
    public static void SetProvider(IServiceProvider sp) => _sp = sp;
    public static T Get<T>() where T : notnull
        => _sp is null ? throw new InvalidOperationException("ServiceProvider not set") : _sp.GetRequiredService<T>();
}
