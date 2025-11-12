using BriAgent.Backend;
using Microsoft.OpenApi.Models;
using BriAgent.Backend.DemosRuntime;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using BriAgent.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

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

// OpenTelemetry: Tracing básico
builder.Services.AddOpenTelemetry()
    .WithTracing(tracer => tracer
        .AddSource(Telemetry.ActivitySourceName)
        //.ConfigureResource(r => r.AddAttribute("service.name", "BriAgent.Backend"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter()
        .AddOtlpExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.MapGet("/bri-agent/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

// Endpoint simple para recuperar trazas/telemetría reciente (para el frontend de aprendizaje)
app.MapGet("/bri-agent/telemetry/recent", () => Results.Ok(TelemetryStore.GetAll()));

// Escuchar en puerto 5080 (HTTP)
app.Urls.Add("http://localhost:5080");

// Mapear endpoints dinámicos de demos
DemoRegistry.MapAllDemoEndpoints(app);

app.Run();

// Exponer un marcador de programa para las pruebas de WebApplicationFactory
namespace BriAgent.Backend { public partial class ProgramMarker { } }
