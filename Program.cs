// ---------------------------------------------------------------------------------------
// Program.cs (root project)
// Propósito: CLI de demos + (opcional) minimal API educativa. Evita conflictos con backend.
// Solución al error de build: había instrucciones de nivel superior coexistiendo con otros
// archivos o duplicados de atributos; mantenemos todo en un único punto aquí y removemos
// usings y dependencias innecesarias.
// ---------------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using demo_agent_framework.Demos;
using demo_agent_framework.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// (Header y opciones de menú movidos a UIHelpers.cs)

internal class RootCli
{
    public static async Task ShowMenuLoop()
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
}
// Las funciones UIHelpers.* contienen la lógica extraída para simplificar este archivo.

public class Program
{
    public static async Task Main(string[] args)
    {
        // Cargar .env si existe (busca hacia arriba)
        UIHelpers.TryLoadDotEnv();
        Console.Title = "Demo Agent Framework (CLI raíz)";
        Console.Title = "Demo Agent Framework (CLI raíz)";
        IHost? webHost = null;
        var enableRootApi = Environment.GetEnvironmentVariable("ENABLE_ROOT_WEBAPI");
        if (!string.IsNullOrWhiteSpace(enableRootApi) &&
            (enableRootApi.Equals("true", StringComparison.OrdinalIgnoreCase) || enableRootApi == "1"))
        {
            webHost = await StartWebServerAsync();
        }
        await RootCli.ShowMenuLoop();
        Console.WriteLine("Saliendo...");
        if (webHost is IAsyncDisposable ad) await ad.DisposeAsync();
    }

    // Iniciar servidor web Minimal API en segundo plano (opcional)
    private static async Task<IHost> StartWebServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        var app = builder.Build();

        const string prefix = "/bri-agent";
        app.MapGet(prefix + "/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }))
           .WithName("BriAgentHealth");

        app.Urls.Add("http://localhost:5080");
        _ = app.RunAsync();

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
                catch { }
                await Task.Delay(300);
            }
        });

        return app;
    }
}
