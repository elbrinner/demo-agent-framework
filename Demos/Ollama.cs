using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace demo_agent_framework.Demos
{
    public static class Ollama
    {
        public static async Task RunAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Demo 4 - Ollama ===");

            var model = "llama3.1:8b";
            var prompt = "Que son los modelos locales con Ollama";
            var endpoint = "http://localhost:11434";
            try
            {
                IChatClient client = new OllamaApiClient(endpoint, model);
                AIAgent agent = new ChatClientAgent(client);

                await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync(prompt))
                {
                    Console.Write(update);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Debe tener instalado Ollama y modelos locales");
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Demostración finalizada. Pulsa Enter para volver al menú principal.");
            Console.ReadLine();
        }
    }
}
