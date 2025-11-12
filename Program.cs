// Cambiar Program.cs para mostrar un menú más visual con encabezado estilizado y levantar un servidor web minimal API.
// Nota importante:
// - La API real y las demos runtime viven en: Bri-Agent/Backend/** (proyecto BriAgent.Backend).
// - Este proyecto raíz NO debe importar namespaces de DemosRuntime ni controladores del backend.
// - El botón "Mostrar Código" del Frontend lee archivos educativos desde Bri-Agent/Backend/Code/** (no se compilan).
using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Text.Json;
using demo_agent_framework.Demos;
using demo_agent_framework.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Cargar .env si existe (busca hacia arriba)
UIHelpers.TryLoadDotEnv();

// (Header y opciones de menú movidos a UIHelpers.cs)

async Task ShowMenuLoop()
{
    while (true)
    {
        UIHelpers.DrawHeader();

        Console.WriteLine();
        UIHelpers.DrawMenuOptions();
        Console.WriteLine();
        Console.WriteLine("  q) Salir");
        Console.WriteLine();
        Console.Write("Selecciona una opción: ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(choice))
            continue;

        if (choice.Equals("q", StringComparison.OrdinalIgnoreCase))
            break;

        try
        {
            await UIHelpers.HandleChoiceAsync(choice);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error ejecutando la demo: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("Pulsa Enter para continuar...");
            Console.ReadLine();
        }
    }
}
// Las funciones UIHelpers.* contienen la lógica extraída para simplificar este archivo.

// Iniciar servidor web Minimal API en segundo plano (opcional, solo para propósitos educativos en el proyecto raíz).
// Por defecto NO se inicia, para evitar conflicto con el backend real en Bri-Agent/Backend.
// Para habilitarlo explícitamente, establece la variable de entorno ENABLE_ROOT_WEBAPI=true
async Task<IHost> StartWebServerAsync()
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    var app = builder.Build();

    // Prefijo base para Bri-Agent
    const string prefix = "/bri-agent";

    // Endpoint de salud
    app.MapGet(prefix + "/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }))
       .WithName("BriAgentHealth");

    // (Proyecto raíz) No se usan DemoRegistry endpoints; la lógica API real está en Bri-Agent backend.
    // Si se quisieran exponer demos locales, se podría reactivar este bloque.

    // Escuchar en http://localhost:5080 por defecto
    app.Urls.Add("http://localhost:5080");

    // Ejecutar en background
    var runTask = app.RunAsync();

    // Autotest: verificar health en loop breve
    _ = Task.Run(async () =>
    {
        using var http = new HttpClient();
        for (int i = 0; i < 10; i++)
        {
            try
            {
                var res = await http.GetAsync("http://localhost:5080" + prefix + "/health");
                if (res.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Bri-Agent] Web API online en http://localhost:5080{prefix} (health OK)");
                    Console.ResetColor();
                    break;
                }
            }
            catch { /* esperar y reintentar */ }
            await Task.Delay(300);
        }
    });

    return app;
}

Console.Title = "Demo Agent Framework (CLI raíz)";

// Por defecto NO iniciar el web server del proyecto raíz.
IHost? webHost = null;
var enableRootApi = Environment.GetEnvironmentVariable("ENABLE_ROOT_WEBAPI");
if (!string.IsNullOrWhiteSpace(enableRootApi) &&
    (enableRootApi.Equals("true", StringComparison.OrdinalIgnoreCase) || enableRootApi == "1"))
{
    webHost = await StartWebServerAsync();
}
await ShowMenuLoop();

Console.WriteLine("Saliendo...");
if (webHost is IAsyncDisposable ad) await ad.DisposeAsync();
