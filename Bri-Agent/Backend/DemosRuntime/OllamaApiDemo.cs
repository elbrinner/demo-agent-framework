using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BriAgent.Backend.Services;
using System.Diagnostics;

namespace BriAgent.Backend.DemosRuntime
{
    /// <summary>
    /// Demo que consume un modelo local vía Ollama (API HTTP) y emite tokens vía SSE.
    /// No requiere paquetes adicionales: usa HttpClient contra /api/generate con stream=true.
    /// </summary>
    public class OllamaApiDemo : IApiDemo
    {
        public string Id => "ollama";
        public string Title => "Agente con Ollama (local)";
        public string Description => "Usa un modelo local de Ollama vía su API HTTP y hace streaming de la respuesta.";
    public IEnumerable<string> Tags => new[] { "ollama", "local", "streaming" };
    public IEnumerable<string> SourceFiles => new[] { "Bri-Agent/Backend/Controllers/OllamaController.cs" };

        public record RunRequest(string? prompt, string? model, string? endpoint);

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public void MapEndpoints(IEndpointRouteBuilder app) { }
    }
}
