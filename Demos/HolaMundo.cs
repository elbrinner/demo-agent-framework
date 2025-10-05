using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using System;
using System.Threading.Tasks;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos
{
    public static class HolaMundoDemo
    {

        public static async Task RunAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Demo 1: Hola Mundo con Azure OpenAI ===");

            // Los valores esperados son:
            // - AZURE_OPENAI_ENDPOINT: URL del endpoint de Azure OpenAI (por ejemplo https://tuservidor.openai.azure.com/)
            // - AZURE_OPENAI_KEY: clave API para autenticar peticiones al servicio
            // - AZURE_OPENAI_MODEL: nombre del despliegue del modelo a usar (deployment name)
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;
            var prompt = "¿Que es el Hola mundo en programacion?";

            // Crear cliente y agente usando las librerías de Azure/Microsoft.
            // AzureOpenAIClient: cliente de alto nivel que encapsula la comunicación con el servicio Azure OpenAI.
            // ApiKeyCredential: credencial simple que envía la API key en las cabeceras de la petición.
            // GetChatClient(model).CreateAIAgent(): obtiene un cliente de chat para el despliegue especificado
            // y crea una instancia de AIAgent que proporciona métodos para ejecutar prompts y flujos de agente.
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            AIAgent agent = client.GetChatClient(model).CreateAIAgent();

            // Ejecutar una petición simple al agente.
            // RunAsync envía un prompt (cadena) al agente y recibe una respuesta completa cuando termina.
            // Esto es útil para demostraciones sencillas donde no se necesita procesar eventos parciales.
            AgentRunResponse response = await agent.RunAsync(prompt);

            // Mostrar la respuesta devuelta por el agente en la consola.
            // AgentRunResponse suele contener el texto generado por el modelo y metadatos asociados.
            Console.WriteLine(response);

            Console.WriteLine();
            Console.WriteLine("Demostración finalizada. Pulsa Enter para volver al menú principal.");
            Console.ReadLine();

        }
    }
}
