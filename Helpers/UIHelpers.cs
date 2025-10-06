using System;
using System.IO;
using System.Threading.Tasks;
using demo_agent_framework.Demos;

namespace demo_agent_framework.Helpers
{
    public static class UIHelpers
{
    // Intentar cargar .env buscando hacia arriba desde el directorio actual
    public static void TryLoadDotEnv()
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

    // Dibuja un encabezado estilizado centrado en la consola
    public static void DrawHeader()
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

    // Muestra las opciones del menú (separado para simplificar ShowMenuLoop)
    public static void DrawMenuOptions()
    {
        Console.WriteLine("  1) Hola Mundo - Demo básico Agent Framework con Azure OpenAI");
        Console.WriteLine("  2) Modo Stream");
        Console.WriteLine("  3) AI Foundry - Persistent Agents (crear y ejecutar agente persistente)");
        Console.WriteLine("  4) Ollama - Usar modelos locales con Ollama"); 
        Console.WriteLine("  5) Contexto");
        Console.WriteLine("  6) Agentes con Tools");
        Console.WriteLine("  7) Aprobación humana");

        }

    // Extrae la lógica de elección para reducir la complejidad del método principal.
    public static async Task HandleChoiceAsync(string choice)
    {
        switch (choice)
        {
            case "1":
                await HolaMundoDemo.RunAsync();
                break;
            case "2":
                await ModoStream.RunAsync();
                break;
            case "3":
                await AiFoundryAgent.RunAsync();
                break;
            case "4":
                await Ollama.RunAsync();
                 break;
            case "5":
                await AgentThread.RunAsync();
                break;
            case "6":
                await AgentTools.RunAsync();
                break;
            case "7":
                await ApprovalRequest.RunAsync();
                break;
                default:
                Console.WriteLine("Opción no válida. Pulsa Enter e intenta de nuevo.");
                Console.ReadLine();
                break;
        }
    }

    }
}

