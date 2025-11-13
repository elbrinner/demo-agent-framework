using BriAgent.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using System.Text;
using Azure.AI.OpenAI;
using BriAgent.Backend.Config;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using System.ComponentModel;
using System.Diagnostics;

namespace BriAgent.Backend.Controllers;

public record BasicRequest(string? prompt);

[ApiController]
[Route("bri-agent/agents")] // prefijo establecido
public class AgentsController : ControllerBase
{
    // Endpoint básico movido a BasicAgentController

    // Streaming endpoints trasladados a StreamingAgentController

    // Endpoints thread/tools/structured trasladados a controladores dedicados

    [HttpGet("diagnostics")]
    public IActionResult Diagnostics()
    {
        // Últimos eventos de telemetría
        var telemetry = TelemetryStore.GetAll().Take(50).ToArray();
        // Configuración básica del modelo desde Credentials
        var model = Config.Credentials.Model;
        var endpoint = Config.Credentials.Endpoint;
        return Ok(new
        {
            endpoint,
            model,
            telemetryCount = telemetry.Length,
            telemetry,
            serverTime = DateTimeOffset.UtcNow
        });
    }
}
