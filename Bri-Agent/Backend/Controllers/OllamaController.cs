using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Net.Http;

namespace BriAgent.Backend.Controllers;

[ApiController]
[Route("bri-agent/ollama")] // Mantiene consistencia con otros controladores
public class OllamaController : BaseAgentController
{
    public record RunRequest(string? prompt, string? model, string? endpoint);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Streaming SSE desde un servidor Ollama local (POST /bri-agent/ollama/run)
    /// </summary>
    [HttpPost("run")]
    public Task Run([FromBody] RunRequest? body)
        => StreamInternal(body?.prompt, body?.model, body?.endpoint);

    /// <summary>
    /// Variante GET compatible con EventSource (GET /bri-agent/ollama/stream?prompt=...)
    /// </summary>
    [HttpGet("stream")]
    public Task Stream([FromQuery] string? prompt, [FromQuery] string? model, [FromQuery] string? endpoint)
        => StreamInternal(prompt, model, endpoint);

    private async Task StreamInternal(string? promptIn, string? modelIn, string? endpointIn)
    {
        SetSseHeaders();
        var ct = HttpContext.RequestAborted;
        var prompt = string.IsNullOrWhiteSpace(promptIn) ? "Que son los modelos locales con Ollama" : promptIn!;
        var model = string.IsNullOrWhiteSpace(modelIn) ? "llama3.1:8b" : modelIn!;
        var endpoint = string.IsNullOrWhiteSpace(endpointIn) ? "http://localhost:11434" : endpointIn!;

        using var activity = StartActivity("ollama.controller.run", prompt.Length);
        activity?.SetTag("ollama.model", model);
        activity?.SetTag("ollama.endpoint", endpoint);

        await SseEventWriter.WriteEventAsync(Response, "started", new { model, endpoint, promptLength = prompt.Length });

        try
        {
            using var http = new HttpClient();
            var url = endpoint.TrimEnd('/') + "/api/generate";
            var payload = new { model, prompt, stream = true };
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while (!reader.EndOfStream && (line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("done", out var doneProp) && doneProp.ValueKind == JsonValueKind.True)
                        break;
                    if (root.TryGetProperty("response", out var respText) && respText.ValueKind == JsonValueKind.String)
                    {
                        var chunk = respText.GetString();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            await SseEventWriter.WriteEventAsync(Response, "token", new { text = chunk }, null, ct);
                        }
                    }
                }
                catch
                {
                    // Ignorar líneas no JSON; el stream de Ollama puede incluir keepalives
                }
            }

            await SseEventWriter.WriteEventAsync(Response, "completed", new { model }, null, ct);
        }
        catch (Exception ex)
        {
            await SseEventWriter.WriteEventAsync(Response, "error", new { error = ex.Message });
        }
    }
}
