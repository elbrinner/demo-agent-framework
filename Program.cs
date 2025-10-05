// Cambiar Program.cs para mostrar un menú más visual con encabezado estilizado.
using System;
using System.Threading.Tasks;
using System.IO;
using demo_agent_framework.Demos;

// Intentar cargar .env buscando hacia arriba desde el directorio actual
void TryLoadDotEnv()
{
    try
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && dir != null; i++)
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
            {
                DotNetEnv.Env.Load(candidate);
                return;
            }
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }
    }
    catch
    {
        // ignorar errores al cargar .env
    }
}

TryLoadDotEnv();

// Dibuja un encabezado estilizado centrado en la consola
void DrawHeader()
{
    Console.Clear();
    var width = Math.Max(Console.WindowWidth, 40);
    var title = "Demo Agent Framework";
    var subtitle = "Ejemplos prácticos usando Microsoft Agent Framework";

    var border = new string('═', Math.Min(80, width - 4));
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"╔{border}╗");

    // Línea del título centrada
    var titleLine = $"  {title}  ";
    var padding = Math.Max(0, (border.Length - titleLine.Length) / 2);
    Console.WriteLine("║" + new string(' ', padding) + titleLine + new string(' ', Math.Max(0, border.Length - padding - titleLine.Length)) + "║");

    // Línea del subtítulo centrada
    var subLine = $"  {subtitle}  ";
    var padding2 = Math.Max(0, (border.Length - subLine.Length) / 2);
    Console.WriteLine("║" + new string(' ', padding2) + subLine + new string(' ', Math.Max(0, border.Length - padding2 - subLine.Length)) + "║");

    Console.WriteLine($"╠{border}╣");
    Console.ResetColor();
}

async Task ShowMenuLoop()
{
    while (true)
    {
        DrawHeader();

        Console.WriteLine();
        Console.WriteLine("  1) Hola Mundo - Demo básico Agent Framework con Azure OpenAI");
        Console.WriteLine("  2) Modo Stream");
        Console.WriteLine("  3) Creando agente en AI Azure");
        Console.WriteLine("  3) Creando agente en AI Azure");
        Console.WriteLine("  3) Creando agente en AI Azure");
        Console.WriteLine("  3) Creando agente en AI Azure");
        Console.WriteLine("  3) Creando agente en AI Azure");
        Console.WriteLine("  3) Creando agente en AI Azure");
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
            switch (choice)
            {
                case "1":
                    await HolaMundoDemo.RunAsync();
                    break;
                case "4":
                    await AiFoundryAgent.RunAsync();
                    break;
                case "2":
                    await ModoStream.RunAsync();
                    break;
                default:
                    Console.WriteLine("Opción no válida. Pulsa Enter e intenta de nuevo.");
                    Console.ReadLine();
                    break;
            }
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

Console.Title = "Demo Agent Framework";
await ShowMenuLoop();
Console.WriteLine("Saliendo...");
