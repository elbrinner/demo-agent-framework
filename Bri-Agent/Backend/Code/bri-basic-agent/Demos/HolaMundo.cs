using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using System;
using System.Threading.Tasks;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos 
{
    // Ejemplo de interfaz tipo API (contrato request/response)
    // Puedes mapearlo en Minimal API así:
    // app.MapPost("/api/hola-mundo", (HolaMundoRequest req) => HolaMundoDemo.RunAsync(req));

    public sealed class HolaMundoRequest
    {
        public string? Prompt { get; set; }
    }

    public sealed class HolaMundoResponse
    {
        public string? Prompt { get; set; }
        public string? Answer { get; set; }
    }

    public static class HolaMundoDemo
    {
        // En formato API: recibe un request con el prompt y devuelve un response con la respuesta del agente
        public static async Task<HolaMundoResponse> RunAsync(HolaMundoRequest request)
        {
            // Configuración desde Credentials (endpoint, apiKey y modelo)
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;

            // Prompt desde el request; si no viene, usamos uno por defecto
            var prompt = string.IsNullOrWhiteSpace(request?.Prompt)
                ? "¿Qué es el 'Hola mundo' en el mundo de la programación?"
                : request!.Prompt!;

            // Crear cliente y agente
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            AIAgent agent = client.GetChatClient(model).CreateAIAgent();

            // Ejecutar la petición al agente
            AgentRunResponse response = await agent.RunAsync(prompt);

            // Devolver un objeto de respuesta (ideal para APIs)
            return new HolaMundoResponse
            {
                Prompt = prompt,
                Answer = response?.ToString()
            };
        }
    }
}
