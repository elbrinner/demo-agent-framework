using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos
{
    // Ejemplo educativo de agente con herramientas (function calling) para Mostrar Código.
    // Nota: Simplificado; muestra estructura del contrato y dónde se conectarían las tools.
    // No se compila: está bajo Bri-Agent/Backend/Code/**.
    //
    // Integración sugerida con Minimal API:
    // app.MapPost("/api/tools/run", async (HttpContext http, ToolsRequest req, CancellationToken ct)
    //     => await ToolsAgentDemo.RunAsync(http.Response, req, ct));

    public sealed class ToolsRequest
    {
        public string? Prompt { get; set; }
        public string? City { get; set; }
    }

    public sealed class ToolsResponse
    {
        public string? Prompt { get; set; }
        public string? Answer { get; set; }
        public object? ToolInfo { get; set; }
    }

    public static class ToolsAgentDemo
    {
        public static async Task RunAsync(HttpResponse response, ToolsRequest request, CancellationToken ct = default)
        {
            var prompt = string.IsNullOrWhiteSpace(request?.Prompt)
                ? "¿Cómo está el clima en Madrid y qué ropa recomiendas?"
                : request!.Prompt!;

            // Cliente/Agente base
            var client = new AzureOpenAIClient(new Uri(Credentials.Endpoint), new ApiKeyCredential(Credentials.ApiKey));
            var chatClient = client.GetChatClient(Credentials.Model);

            // Aquí se añadirían Tools reales (ejemplo educativo):
            // var tools = new[] { new FunctionTool(ObtenerClima), new FunctionTool(RecomendarRopa) };
            // var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions("AgenteConTools") { Tools = tools });

            // Para simplificar la explicación sin dependency extra, usamos un agente simple
            var agent = chatClient.CreateAIAgent();
            var answer = (await agent.RunAsync(prompt))?.ToString();

            var payload = new ToolsResponse
            {
                Prompt = prompt,
                Answer = answer,
                ToolInfo = new { used = "(educativo) aquí se invocarían tools con function calling" }
            };

            response.Headers["Content-Type"] = "application/json";
            await response.WriteAsJsonAsync(payload, cancellationToken: ct);
        }

        // Ejemplo de firma de una tool (no implementada, solo educativa):
        // [Description("Obtiene el clima actual para una ciudad")] 
        // public static Task<string> ObtenerClima(string ciudad) => Task.FromResult($"Soleado en {ciudad}");
    }
}
