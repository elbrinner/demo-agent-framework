using Azure.AI.OpenAI;
using demo_agent_framework.Config;
using Microsoft.Agents.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Threading.Tasks;

namespace demo_agent_framework.Demos
{
    public static class ModoStream
    {
        public static async Task RunAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Demo 2: Hola Mundo con Azure OpenAI y Stream ===");

            // Los valores esperados son:
            // - AZURE_OPENAI_ENDPOINT: URL del endpoint de Azure OpenAI (por ejemplo https://tuservidor.openai.azure.com/)
            // - AZURE_OPENAI_KEY: clave API para autenticar peticiones al servicio
            // - AZURE_OPENAI_MODEL: nombre del despliegue del modelo a usar (deployment name)
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;
            var prompt = "¿Qué es el modo stream en los LLMs y para qué sirve?";
            // Crear cliente y agente usando las librerías de Azure/Microsoft.
            // AzureOpenAIClient: cliente de alto nivel que encapsula la comunicación con el servicio Azure OpenAI.
            // ApiKeyCredential: credencial simple que envía la API key en las cabeceras de la petición.
            // GetChatClient(model).CreateAIAgent(): obtiene un cliente de chat para el despliegue especificado
            // y crea una instancia de AIAgent que proporciona métodos para ejecutar prompts y flujos de agente.
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            AIAgent agent = client.GetChatClient(model).CreateAIAgent();

            //NUEVO: Ejecutar una petición al agente en modo streaming.
            await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync(prompt))
            {
                Console.Write(update);
            }

            Console.WriteLine();
            Console.WriteLine("Demostración finalizada. Pulsa Enter para volver al menú principal.");
            Console.ReadLine();
        }
    }
}
