using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Azure.Identity;
using Azure.Core;
using Azure.AI.Agents.Persistent;
using Microsoft.Agents.AI;
using BriAgent.Backend.Config;

namespace BriAgent.Backend.Controllers;

[ApiController]
[Route("bri-agent/aifoundry")] // Consistente con otros controladores
public class AiFoundryController : BaseAgentController
{
    public record RunRequest(string? prompt, string? name, string? description, string? systemPrompt, string? endpoint, string? model);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IWebHostEnvironment _env;
    public AiFoundryController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private static string DefaultEndpoint =>
        Environment.GetEnvironmentVariable("AZURE_PERSISTENT_AGENTS_ENDPOINT")
        ?? "https://bri-ai-openai.services.ai.azure.com/api/projects/AIBRI"; // fallback demo

    private static string DefaultAgentName => "AgenteBri";
    private static string DefaultAgentDescription => "Agente de prueba para la tienda de informática de Bri";
    private static string DefaultAgentSystemPrompt => "Eres un agente experto en tiendas de informática y ayudas a los clientes a encontrar productos y resolver dudas técnicas.";

    [HttpPost("run")]
    public Task Run([FromBody] RunRequest? body)
        => StreamInternal(body?.prompt, body?.name, body?.description, body?.systemPrompt, body?.endpoint, body?.model);

    [HttpGet("stream")]
    public Task Stream([FromQuery] string? prompt, [FromQuery] string? name, [FromQuery] string? description, [FromQuery] string? systemPrompt, [FromQuery] string? endpoint, [FromQuery] string? model)
        => StreamInternal(prompt, name, description, systemPrompt, endpoint, model);

    private static string? _cachedAgentId;
    private static readonly SemaphoreSlim _agentLock = new(1, 1);

    private async Task<string?> LoadSavedAgentIdAsync()
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, ".agents");
            var file = Path.Combine(dir, DefaultAgentName + ".id");
            if (System.IO.File.Exists(file))
                return (await System.IO.File.ReadAllTextAsync(file)).Trim();
        }
        catch { }
        return null;
    }

    private async Task SaveAgentIdAsync(string id)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, ".agents");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, DefaultAgentName + ".id");
            await System.IO.File.WriteAllTextAsync(file, id);
        }
        catch { }
    }

    private static TokenCredential BuildCredential()
    {
        if (Credentials.HasClientCredentials && Credentials.ClientId != null && Credentials.TenantId != null && Credentials.ClientSecret != null)
            return new ClientSecretCredential(Credentials.TenantId, Credentials.ClientId, Credentials.ClientSecret);
        return new AzureCliCredential();
    }

    private async Task EnsureAgentAsync(PersistentAgentsClient client, string model, string name, string description, string systemPrompt, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedAgentId))
        {
                try { _ = await client.GetAIAgentAsync(_cachedAgentId); return; } catch { /* will recreate */ }
        }

        await _agentLock.WaitAsync(ct);
        try
        {
            // Double-check after entering lock
            if (!string.IsNullOrEmpty(_cachedAgentId))
            {
                try { _ = await client.GetAIAgentAsync(_cachedAgentId); return; } catch { }
            }

            var saved = await LoadSavedAgentIdAsync();
            if (!string.IsNullOrEmpty(saved))
            {
                try { _ = await client.GetAIAgentAsync(saved); _cachedAgentId = saved; return; } catch { }
            }

            var created = await client.Administration.CreateAgentAsync(model, name, description, systemPrompt);
            _cachedAgentId = created.Value.Id;
            await SaveAgentIdAsync(_cachedAgentId);
        }
        finally
        {
            _agentLock.Release();
        }
    }

    private async Task StreamInternal(string? promptIn, string? nameIn, string? descIn, string? sysIn, string? endpointIn, string? modelIn)
    {
        SetSseHeaders();
        var ct = HttpContext.RequestAborted;
        var prompt = string.IsNullOrWhiteSpace(promptIn) ? "Ayudarme a crear un agente para mi tienda de informática" : promptIn!;
        var model = string.IsNullOrWhiteSpace(modelIn) ? Credentials.Model : modelIn!;
        var endpoint = string.IsNullOrWhiteSpace(endpointIn) ? DefaultEndpoint : endpointIn!;
        var name = string.IsNullOrWhiteSpace(nameIn) ? DefaultAgentName : nameIn!;
        var description = string.IsNullOrWhiteSpace(descIn) ? DefaultAgentDescription : descIn!;
        var systemPrompt = string.IsNullOrWhiteSpace(sysIn) ? DefaultAgentSystemPrompt : sysIn!;

        using var activity = StartActivity("aifoundry.controller.run", prompt.Length);
        activity?.SetTag("aifoundry.endpoint", endpoint);
        activity?.SetTag("aifoundry.model", model);
        activity?.SetTag("aifoundry.name", name);

        await SseEventWriter.WriteEventAsync(Response, "started", new { model, endpoint, promptLength = prompt.Length, name }, null, ct);

        try
        {
            var credential = BuildCredential();
            var client = new PersistentAgentsClient(endpoint, credential);
            await EnsureAgentAsync(client, model, name, description, systemPrompt, ct);

            if (string.IsNullOrEmpty(_cachedAgentId))
                throw new InvalidOperationException("No se pudo resolver el Id del agente persistente");

            var agent = await client.GetAIAgentAsync(_cachedAgentId);

            await foreach (var update in agent.RunStreamingAsync(prompt))
            {
                var chunk = update?.Text;
                if (!string.IsNullOrEmpty(chunk))
                {
                    await SseEventWriter.WriteEventAsync(Response, "token", new { text = chunk }, null, ct);
                }
            }

            await SseEventWriter.WriteEventAsync(Response, "completed", new { model, agentId = _cachedAgentId }, null, ct);
        }
        catch (Exception ex)
        {
            await SseEventWriter.WriteEventAsync(Response, "error", new { error = ex.Message });
        }
    }
}
