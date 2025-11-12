using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BriAgent.Backend.DemosRuntime
{
    /// <summary>
    /// Demo AI Foundry con agentes persistentes en Azure. Hace streaming vía SSE.
    /// Solo expone metadatos para el listado; los endpoints viven en el controlador.
    /// </summary>
    public class AiFoundryApiDemo : IApiDemo
    {
        public string Id => "ai-foundry";
        public string Title => "AI Foundry Agent (Azure)";
        public string Description => "Ejecuta un agente persistente en Azure AI Foundry y hace streaming de la respuesta.";
        public IEnumerable<string> Tags => new[] { "azure", "ai-foundry", "persistent", "streaming" };
        public IEnumerable<string> SourceFiles => new[] { "Bri-Agent/Backend/Controllers/AiFoundryController.cs" };

        public void MapEndpoints(IEndpointRouteBuilder app) { }
    }
}
