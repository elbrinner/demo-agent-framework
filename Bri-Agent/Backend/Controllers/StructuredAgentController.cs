using Microsoft.AspNetCore.Mvc;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using BriAgent.Backend.Models;
using System.Text.Json;

namespace BriAgent.Backend.Controllers;

[Route("bri-agent/agents")] // mismo prefijo
public class StructuredAgentController : BaseAgentController
{
    public record StructuredRequest(string? prompt, string? name, int? age, string? occupation);

    [HttpPost("structured")] // POST /bri-agent/agents/structured
    public async Task<IActionResult> Structured([FromBody] StructuredRequest body)
    {
        var basePrompt = body.prompt ?? "Genera un objeto persona para Ana Rodríguez, ingeniera de software de 32 años";

        var endpoint = BriAgent.Backend.Config.Credentials.Endpoint;
        var apiKey = BriAgent.Backend.Config.Credentials.ApiKey;
        var model = BriAgent.Backend.Config.Credentials.Model;
        var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var agentOptions = new ChatClientAgentOptions(name: "AgenteEstructurado", instructions: "Devuelve JSON válido del esquema solicitado.")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<BriAgent.Backend.Models.PersonInfo>()
            }
        };

        var chatClient = client.GetChatClient(model);
        var agent = chatClient.CreateAIAgent(agentOptions);
        var updates = agent.RunStreamingAsync(basePrompt);
        var full = await updates.ToAgentRunResponseAsync();
        var person = full.Deserialize<BriAgent.Backend.Models.PersonInfo>(JsonSerializerOptions.Web);

        var meta = BuildMeta(
            demoId: "structured-agent",
            controller: nameof(StructuredAgentController),
            profile: new UiProfile(mode: "structured", stream: false, structured: true, recommendedView: "StructuredViewer", capabilities: new[] { "structured" })
        );
        return Ok(new { prompt = basePrompt, person, meta });
    }
}
