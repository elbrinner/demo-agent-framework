using Microsoft.Agents.AI.OpenAI;
using Azure.AI.OpenAI;
using BriAgent.Backend.Config;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;

namespace BriAgent.Backend.Services
{
    public static class AgentFactory
    {
        public static AIAgent CreateBasicAgent(string? instructions = null, AIFunction[]? tools = null)
        {
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            return client.GetChatClient(model).CreateAIAgent(instructions: instructions, tools: tools);
        }
    }
}
