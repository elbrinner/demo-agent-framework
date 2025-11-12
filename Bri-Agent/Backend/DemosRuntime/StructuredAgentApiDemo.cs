using Microsoft.AspNetCore.Routing;
using BriAgent.Backend.DemosRuntime;
using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using System.ClientModel;
using System.Text.Json;
using BriAgent.Backend.Controllers;
using BriAgent.Backend.Models;
using BriAgent.Backend.Config;
using OpenAI;
using Microsoft.Extensions.AI;

namespace BriAgent.Backend.DemosRuntime
{
    public class StructuredAgentApiDemo : IApiDemo
    {
        public string Id => "structured-agent";
        public string Title => "Agente con Salida Estructurada";
        public string Description => "Demuestra un agente que devuelve respuestas en formato JSON estructurado.";
        public IEnumerable<string> Tags => new[] { "agente", "structured", "json-schema" };
        public IEnumerable<string> SourceFiles => new[] { "DemosRuntime/StructuredAgentApiDemo.cs", "Controllers/AgentsController.cs", "Models/PersonInfo.cs" };

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/bri-agent/demos/structured-agent/run", async (
                HttpContext httpContext,
                [FromBody] BasicRequest? request
            ) =>
            {
                var prompt = request?.prompt ?? "Proporciona información sobre Juan Pérez, un desarrollador de software de 30 años.";
                using var activity = Telemetry.ActivitySource.StartActivity("demo.structured.run");

                var endpoint = Credentials.Endpoint;
                var apiKey = Credentials.ApiKey;
                var model = Credentials.Model;
                var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

                var agentOptions = new ChatClientAgentOptions(name: "AgenteEstructurado", instructions: "Eres un asistente que responde con información estructurada sobre personas.")
                {
                    ChatOptions = new()
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
                    }
                };

                var chatClient = client.GetChatClient(model);
                var agent = chatClient.CreateAIAgent(agentOptions);

                var response = await agent.RunAsync(prompt);
                var personInfo = response.Deserialize<PersonInfo>(JsonSerializerOptions.Web);

                var ui = new UiProfile(
                    mode: "structured",
                    stream: false,
                    history: false,
                    recommendedView: "StructuredViewer",
                    capabilities: new[] { "structured" }
                );
                var meta = new UiMeta(
                    version: "v1",
                    demoId: "structured-agent",
                    controller: nameof(StructuredAgentApiDemo),
                    ui: ui
                );
                var hints = new[] {
                    "Genera una ficha de persona para 'Juan Pérez', 30 años, desarrollador de software.",
                    "Crea el perfil de 'Ana García', 25 años, analista de datos, con 3 habilidades.",
                    "Resume la biografía profesional de 'Luis López', 40 años, gerente de proyectos."
                };
                var schema = new {
                    type = "object",
                    properties = new {
                        name = new { type = "string" },
                        age = new { type = "number" },
                        occupation = new { type = "string" },
                        skills = new { type = "array", items = new { type = "string" } },
                        summary = new { type = "string" }
                    },
                    required = new[] { "name", "age", "occupation" }
                };

                return Results.Ok(new { personInfo, meta, hints, schema });
            });
        }
    }
}