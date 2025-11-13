using BriAgent.Backend; // namespace marcador
using Microsoft.OpenApi.Models;
using BriAgent.Backend.DemosRuntime; // DemoRegistry (namespace existe en Backend)
using BriAgent.Backend.Services; // McpFileSystemService, JokesEventBus, JokesWorkflowService, JokesTools, AgentRunner
// Usings específicos necesarios para extensiones OpenTelemetry
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Configurar raíz del servidor MCP para guardar archivos SOLO en ~/Documents/jokes
var userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
if (!string.IsNullOrEmpty(userDocs))
{
    var current = Environment.GetEnvironmentVariable("MCP_FS_ALLOWED_PATH");
    if (string.IsNullOrWhiteSpace(current))
    {
        try
        {
            var jokesDir = Path.Combine(userDocs, "jokes");
            Directory.CreateDirectory(jokesDir);
            // Establecer la raíz permitida EXCLUSIVAMENTE a ~/Documents/jokes
            Environment.SetEnvironmentVariable("MCP_FS_ALLOWED_PATH", jokesDir);
        }
        catch { /* si falla, dejamos que MCP use su fallback */ }
    }
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bri-Agent API",
        Version = "v1",
        Description = "API para demos de Microsoft Agent Framework"
    });
});
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// Registrar servicio MCP filesystem
builder.Services.AddSingleton<McpFileSystemService>();
// Servicio de índice de chistes
builder.Services.AddSingleton<JokesIndexService>();
// Servicio de checkpoints (HITL snapshots)
builder.Services.AddSingleton<JokesCheckpointService>();
// Servicio de moderación
builder.Services.AddSingleton<JokesModerationService>();
// Registrar ejecución de agentes con telemetría unificada
builder.Services.AddSingleton<AgentRunner>();
// Registrar factoría de agentes (Agent Framework)
builder.Services.AddSingleton<IAgentFactory>(sp =>
{
    var tools = sp.GetRequiredService<Microsoft.Extensions.AI.AIFunction[]>();
    var useAgents = string.Equals(Environment.GetEnvironmentVariable("USE_AGENT_JOKES"), "true", StringComparison.OrdinalIgnoreCase);
    // Si no se van a usar agentes, evitar tocar credenciales aquí para no romper tests/entornos sin configuración
    return useAgents ? new DefaultAgentFactory(tools) : new LazyAgentFactory(tools);
});

// OpenTelemetry: Tracing básico
builder.Services.AddOpenTelemetry()
    .WithTracing(tracer => tracer
        .AddSource(Telemetry.ActivitySourceName)
        //.ConfigureResource(r => r.AddAttribute("service.name", "BriAgent.Backend"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter()
        .AddOtlpExporter());

// Workflow de chistes
builder.Services.AddSingleton<JokesEventBus>();
builder.Services.AddSingleton(provider => JokesTools.AsFunctions(provider));
builder.Services.AddSingleton<JokesWorkflowService>(sp =>
{
    var mcp = sp.GetRequiredService<McpFileSystemService>();
    var index = sp.GetRequiredService<JokesIndexService>();
    var bus = sp.GetRequiredService<JokesEventBus>();
    var tools = sp.GetRequiredService<Microsoft.Extensions.AI.AIFunction[]>();
    var runner = sp.GetRequiredService<AgentRunner>();
    var factory = sp.GetRequiredService<IAgentFactory>();
    var checkpoint = sp.GetRequiredService<JokesCheckpointService>();
    var moderation = sp.GetRequiredService<JokesModerationService>();
    return new JokesWorkflowService(mcp, bus, tools, runner, factory, index, checkpoint, moderation);
});

var app = builder.Build();

// Inicializar ServiceLocator (para tools estáticas) ya se hace en JokesTools.AsFunctions

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.MapGet("/bri-agent/health", () =>
{
    try
    {
        // Validar variables requeridas para Azure OpenAI (sin exponer secretos)
        var endpoint = BriAgent.Backend.Config.Credentials.Endpoint; // puede lanzar si falta
        var _ = BriAgent.Backend.Config.Credentials.ApiKey;          // acceso para validar presencia
        var model = BriAgent.Backend.Config.Credentials.Model;       // puede lanzar si falta

        // Derivar diagnósticos no sensibles
        var useAgents = string.Equals(Environment.GetEnvironmentVariable("USE_AGENT_JOKES"), "true", StringComparison.OrdinalIgnoreCase);
        var timeout = Environment.GetEnvironmentVariable("AGENT_STEP_TIMEOUT_SECONDS");
        var requireLlm = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_LLM_JOKES"), "true", StringComparison.OrdinalIgnoreCase);
        string endpointHost;
        try { endpointHost = new Uri(endpoint).Host; } catch { endpointHost = "(invalid-uri)"; }

        return Results.Ok(new
        {
            status = "ok",
            time = DateTimeOffset.UtcNow,
            azureOpenAI = new { endpointHost, model },
            config = new { useAgents, agentStepTimeoutSeconds = timeout, requireLlm }
        });
    }
    catch (Exception ex)
    {
        // Devolver estado 500 con detalle del problema de configuración (sin secretos)
        return Results.Problem(
            title: "Configuración incompleta de Azure OpenAI",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Deep health: realiza una llamada mínima al modelo a través del Agent Framework
app.MapGet("/bri-agent/health/deep", async (IServiceProvider sp) =>
{
    try
    {
        // Resolver dependencias necesarias del contenedor
        var factory = sp.GetRequiredService<IAgentFactory>();
        var runner = sp.GetRequiredService<AgentRunner>();
        // Crear un agente genérico y hacer una llamada muy corta para validar que el modelo responde
        var agent = factory.CreateBasicAgent(instructions: "Responde 'pong' y nada más.");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = DateTimeOffset.UtcNow;
        var resp = await runner.RunAsync(agent, "ping", agentType: "health", model: BriAgent.Backend.Config.Credentials.Model, cancellationToken: cts.Token);
        var elapsed = DateTimeOffset.UtcNow - started;
        var text = (resp.Text ?? string.Empty).Trim();
        if (text.Length > 160) text = text.Substring(0, 160);
        return Results.Ok(new
        {
            status = "ok",
            model = BriAgent.Backend.Config.Credentials.Model,
            latencyMs = (int)elapsed.TotalMilliseconds,
            sample = text
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Deep health failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Endpoint simple para recuperar trazas/telemetría reciente (para el frontend de aprendizaje)
app.MapGet("/bri-agent/telemetry/recent", () => Results.Ok(TelemetryStore.GetAll()));

// Escuchar en puerto 5080 (HTTP)
app.Urls.Add("http://localhost:5080");

// Mapear endpoints dinámicos de demos si están disponibles, por reflexión para evitar errores de diseño
try
{
    var t = Type.GetType("BriAgent.Backend.DemosRuntime.DemoRegistry, BriAgent.Backend");
    var m = t?.GetMethod("MapAllDemoEndpoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    m?.Invoke(null, new object[] { app });
}
catch { }

app.Run();

// Exponer un marcador de programa para las pruebas de WebApplicationFactory
namespace BriAgent.Backend { public partial class ProgramMarker { } }
