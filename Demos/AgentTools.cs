using Azure.AI.OpenAI;
using demo_agent_framework.Config;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace demo_agent_framework.Demos
{
    public static class AgentTools
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== Demo 6: Herramientas funcionales con Agent Tools ===");

            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;
            var prompt = "Eres un asistente útil que puede consultar el clima y recomendar platos típicos.";    

            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));

            // Tool 1: Clima
            [Description("Obtiene el clima para una ciudad dada.")]
            static string ObtenerClima([Description("La ciudad para consultar el clima.")] string ciudad)
                => $"El clima en {ciudad} es nublado con una máxima de 15°C.";

            // Tool 2: Plato típico
            [Description("Recomienda un plato típico según la ciudad.")]
            static string RecomendarPlato([Description("Ciudad para recomendar comida.")] string ciudad)
                => ciudad.ToLower() switch
                {
                    "madrid" => "Cocido madrileño",
                    "lisboa" => "Bacalhau à Brás",
                    "parís" => "Coq au vin",
                    _ => $"No tengo datos sobre platos típicos en {ciudad}."
                };

            // Crear agente con ambas herramientas
            var agent = client.GetChatClient(model).CreateAIAgent(
                instructions: prompt,
                tools: [
                    AIFunctionFactory.Create(ObtenerClima),
            AIFunctionFactory.Create(RecomendarPlato)
                ]
            );

            // Leer pregunta desde consola
            Console.Write("\nEscribe tu pregunta: ");
            var pregunta = Console.ReadLine();

            // Ejecutar en modo streaming
            Console.WriteLine("\nRespuesta del agente:\n");
            await foreach (var update in agent.RunStreamingAsync(pregunta))
                Console.Write(update);

            Console.WriteLine("\n\nPulsa Enter para continuar.");
            Console.ReadLine();
        }
    }
}
