using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos
{
    // Ejemplo educativo de agente con salida estructurada (JSON Schema) para Mostrar Código.
    // IMPORTANTE: Este archivo NO se compila (está en Bri-Agent/Backend/Code/**) y sirve para explicar el concepto.
    //
    // Pipeline conceptual de transformación (prompt -> JSON tipado):
    // 1. Definimos un tipo .NET (PersonInfo) que representa el esquema deseado.
    // 2. Creamos ChatClientAgentOptions con ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
    //    Esto le indica al modelo que debe adherirse al JSON Schema derivado de PersonInfo.
    // 3. Ejecutamos agent.RunAsync(prompt) y obtenemos la respuesta estructurada.
    // 4. Deserializamos la salida directamente al tipo PersonInfo (sin hacer parsing manual de strings) usando
    //    response.Deserialize<PersonInfo>(JsonSerializerOptions.Web) en código "real" (ver StructuredAgentApiDemo.cs).
    // 5. Empaquetamos: Prompt original + datos tipados + texto bruto para debugging.
    //
    // Beneficios respecto a ChatResponseFormat.Json:
    // - Validación estructural: el modelo intenta ajustarse al esquema.
    // - Menos lógica de parsing frágil.
    // - Facilita viewers especializados (p.ej. StructuredViewer en frontend).
    //
    // Integración sugerida con Minimal API (ejemplo simplificado):
    // app.MapPost("/api/structured/run", async (HttpContext http, StructuredRequest req, CancellationToken ct)
    //     => await StructuredAgentDemo.RunAsync(http.Response, req, ct));

    public sealed class StructuredRequest
    {
        public string? Prompt { get; set; }
    }

    // Definición del modelo que genera el JSON Schema.
    // En el código compilado usamos BriAgent.Backend.Models.PersonInfo con atributos JsonPropertyName.
    // Aquí simplificamos para fines educativos.
    public sealed class PersonInfo
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public string? Occupation { get; set; }
        public string[]? Skills { get; set; }
        public string? Summary { get; set; }
    }

    public sealed class StructuredResponse
    {
        public string? Prompt { get; set; }
        public PersonInfo? Data { get; set; }
        public string? Raw { get; set; }
    }

    public static class StructuredAgentDemo
    {
        public static async Task RunAsync(HttpResponse response, StructuredRequest request, CancellationToken ct = default)
        {
            var prompt = string.IsNullOrWhiteSpace(request?.Prompt)
                ? "Proporciona información sobre Ana, desarrolladora backend de 28 años en Bogotá"
                : request!.Prompt!;

            var client = new AzureOpenAIClient(new Uri(Credentials.Endpoint), new ApiKeyCredential(Credentials.ApiKey));
            var chatClient = client.GetChatClient(Credentials.Model);

            // Configuración usando JSON Schema FUERTE (derivado de PersonInfo):
            // ChatResponseFormat.ForJsonSchema<PersonInfo>() genera un schema y guía al modelo para responder con JSON válido.
            var agentOptions = new ChatClientAgentOptions(name: "AgenteEstructurado", instructions: "Devuelve JSON válido del esquema solicitado.")
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
                }
            };

            var agent = chatClient.CreateAIAgent(agentOptions);
            var responseRun = await agent.RunAsync(prompt);

            // En código compilado podríamos hacer: var person = responseRun.Deserialize<PersonInfo>(JsonSerializerOptions.Web);
            // Aquí demostramos dos caminos: (1) método de la librería (comentado) y (2) fallback manual.
            PersonInfo? parsed = null;
            // try { parsed = responseRun.Deserialize<PersonInfo>(JsonSerializerOptions.Web); } catch { /* fallback abajo */ }

            var rawText = responseRun?.ToString();
            if (parsed == null)
            {
                try { parsed = rawText != null ? JsonSerializer.Deserialize<PersonInfo>(rawText) : null; } catch { }
            }

            var payload = new StructuredResponse { Prompt = prompt, Data = parsed, Raw = rawText };
            response.Headers["Content-Type"] = "application/json";
            await response.WriteAsJsonAsync(payload, cancellationToken: ct);
        }
    }
}
