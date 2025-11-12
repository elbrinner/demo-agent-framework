using Azure.AI.OpenAI;
using demo_agent_framework.Config;
using Microsoft.Agents.AI;
using OpenAI;
using System;
using System.ClientModel;
using Azure.Identity;

namespace demo_agent_framework.Demos
{
    public static class AgentThread
    {
        public static async Task RunAsync()
        {
             var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;

            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            var agent = client.GetChatClient(model).CreateAIAgent(
                instructions: "Responde en español y recuerda lo que el usuario dice.",
                name: "Narrador"
            );

            var contexto = agent.GetNewThread();

            // Primer turno
            await foreach (var update in agent.RunStreamingAsync("Hola, soy Elbrinner. Me encanta enseñar IA.", contexto))
                Console.Write(update);

            // Segundo turno: validamos el contexto
            await foreach (var update in agent.RunStreamingAsync("¿Quién soy?", contexto))
                Console.Write(update);


        }


    }
}
