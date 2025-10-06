using Azure.AI.OpenAI;
using demo_agent_framework.Config;
using demo_agent_framework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace demo_agent_framework.Demos
{
    public static class StructuredOutput
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== Demo 8: Salida estructurada con JSON Schema ===");

            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;
            var prompt = "Por favor, proporciona información sobre María López, una ingeniera de datos de 29 años.";


            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));

            // Configurar el agente para devolver JSON según el esquema de PersonInfo
            var agentOptions = new ChatClientAgentOptions(name: "AgenteEstructurado", instructions: "Eres un asistente que responde con información estructurada sobre personas.")
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
                }
            };

            var agent = client.GetChatClient(model).CreateAIAgent(agentOptions);

            // Ejecutar en modo streaming
            var updates = agent.RunStreamingAsync(prompt);
            var fullResponse = await updates.ToAgentRunResponseAsync();
            var personInfo = fullResponse.Deserialize<PersonInfo>(JsonSerializerOptions.Web);

            Console.WriteLine("\n=== Respuesta estructurada (modo streaming) ===");
            Console.WriteLine($"Nombre: {personInfo.Name}");
            Console.WriteLine($"Edad: {personInfo.Age}");
            Console.WriteLine($"Ocupación: {personInfo.Occupation}");

            Console.WriteLine("Demostración finalizada. Pulsa Enter para volver al menú principal.");
            Console.ReadLine();
        }
    }
}
