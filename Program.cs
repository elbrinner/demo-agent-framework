// Cambiar Program.cs para mostrar un menú más visual con encabezado estilizado.
using System;
using System.Threading.Tasks;
using System.IO;
using demo_agent_framework.Demos;
using demo_agent_framework;

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

Console.Title = "Demo Agent Framework";
await ShowMenuLoop();
Console.WriteLine("Saliendo...");
