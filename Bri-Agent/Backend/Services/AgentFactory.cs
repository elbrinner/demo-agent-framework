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
    public readonly record struct AgentProfile(
        string Name,
        string Model,
        float Temperature,
        float TopP,
        int MaxOutputTokens
    );

    /// <summary>
    /// Factoría de agentes para desacoplar construcción y permitir sustituir backend (Azure OpenAI, Ollama, etc.).
    /// </summary>
    public interface IAgentFactory
    {
        // Crea un agente genérico con instrucciones y tools opcionales (modelo por defecto salvo override)
        AIAgent CreateBasicAgent(string? instructions = null, AIFunction[]? tools = null, string? modelOverride = null);

        // Atajos especializados para el workflow de chistes
        AIAgent CreateJokesGenerator();
        AIAgent CreateJokesReviewer();
        AIAgent CreateJokesBoss();

        // Exponer perfiles (informativo/telemetría)
        AgentProfile GeneratorProfile { get; }
        AgentProfile ReviewerProfile { get; }
        AgentProfile BossProfile { get; }
    }

    /// <summary>
    /// Implementación por defecto usando Azure OpenAI (config en Credentials).
    /// </summary>
    public class DefaultAgentFactory : IAgentFactory
    {
        private readonly AIFunction[] _defaultTools;
        public AgentProfile GeneratorProfile { get; }
        public AgentProfile ReviewerProfile { get; }
        public AgentProfile BossProfile { get; }

        public DefaultAgentFactory(AIFunction[] defaultTools)
        {
            _defaultTools = defaultTools;
            var model = Credentials.Model;
            GeneratorProfile = new AgentProfile(
                Name: "jokes.generator",
                Model: model,
                Temperature: 0.9f,
                TopP: 0.95f,
                MaxOutputTokens: 512
            );
            ReviewerProfile = new AgentProfile(
                Name: "jokes.reviewer",
                Model: model,
                Temperature: 0.2f,
                TopP: 0.8f,
                MaxOutputTokens: 256
            );
            BossProfile = new AgentProfile(
                Name: "jokes.boss",
                Model: model,
                Temperature: 0.1f,
                TopP: 0.7f,
                MaxOutputTokens: 128
            );
        }

        public AIAgent CreateBasicAgent(string? instructions = null, AIFunction[]? tools = null, string? modelOverride = null)
        {
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = modelOverride ?? Credentials.Model;
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            // Nota: algunos parámetros del perfil (temperature/top_p/max_tokens) no están expuestos aquí todavía.
            // Se conservarán para futuras versiones del SDK; de momento nos limitamos a instrucciones y tools.
            return client.GetChatClient(model).CreateAIAgent(instructions: instructions, tools: tools ?? _defaultTools);
        }

        public AIAgent CreateJokesGenerator()
            => CreateBasicAgent(
                instructions: """
                Eres el Agente Generador de chistes (perfil creativo y ocurrente).
                Crea chistes breves, originales y divertidos sobre programación, tecnología o temas geek.
                Usa humor inteligente, simple y sin referencias obscenas o personales.
                Devuelve SOLO el chiste en texto plano, sin explicación, sin JSON ni herramientas.
                Debe caber en una sola línea.
                """,
                tools: Array.Empty<AIFunction>()
            );

        public AIAgent CreateJokesReviewer()
            => CreateBasicAgent(
                instructions: """
                Eres el Revisor (perfil analítico) de chistes de programación.
                Evalúa el chiste y asigna un score entero 0..10.
                Política: score <= 7 es malo y debe ser rechazado por el workflow.
                Escribe una justificación corta, específica y útil para mejorar.
                Devuelve JSON EXACTO sin texto extra:
                {"score": <0..10>, "rationale": "frase corta"}
                """,
                tools: Array.Empty<AIFunction>()
            );

     
        // Prompt actualizado (versión enriquecida solicitada por el usuario)
        // Nota: se mantiene la estructura anterior para minimizar el impacto en el resto del código.
        // Si en el futuro se parametriza internacionalización, este bloque debería extraerse.
        public AIAgent CreateJokesBoss()
            => CreateBasicAgent(
                instructions: """
                Eres el Jefe (perfil exigente y serio, con un toque de humor seco).
                Regla:
                - Rechaza automáticamente (decision="auto") los chistes que no consideres realmente buenos o graciosos.
                - Solo pide aprobación humana (decision="hitl") cuando estés convencido de que el chiste es EXCEPCIONAL: muy original, divertido y bien construido (típicamente 9–10).
                
                Tu tono debe ser breve, sarcástico y con una pizca de ironía.
                Puedes usar tools MCP (p. ej., list_jokes, index_search) si necesitas contexto del repositorio de chistes.
                Devuelve **JSON EXACTO** sin texto adicional, con el siguiente formato:
                {"decision": "hitl"|"auto", "notes": "justificación muy corta, seria y con un guiño gracioso"}
                """,
                tools: _defaultTools
            );
    }

    /// <summary>
    /// Factoría perezosa que NO accede a credenciales en el constructor.
    /// Útil para escenarios de pruebas o cuando USE_AGENT_JOKES != true.
    /// Solo intentará crear clientes/modelos cuando se invoquen sus métodos.
    /// </summary>
    public class LazyAgentFactory : IAgentFactory
    {
        private readonly AIFunction[] _defaultTools;

        public LazyAgentFactory(AIFunction[] defaultTools)
        {
            _defaultTools = defaultTools;
        }

        // Perfiles informativos sin tocar credenciales; valores por defecto marcados como "lazy"
        public AgentProfile GeneratorProfile => new("jokes.generator", "lazy", 0.9f, 0.95f, 512);
        public AgentProfile ReviewerProfile  => new("jokes.reviewer",  "lazy", 0.2f, 0.80f, 256);
        public AgentProfile BossProfile      => new("jokes.boss",      "lazy", 0.1f, 0.70f, 128);

        public AIAgent CreateBasicAgent(string? instructions = null, AIFunction[]? tools = null, string? modelOverride = null)
        {
            // Acceder a credenciales sólo aquí, cuando se necesite realmente crear un agente
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = modelOverride ?? Credentials.Model;
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            return client.GetChatClient(model).CreateAIAgent(instructions: instructions, tools: tools ?? _defaultTools);
        }




    public AIAgent CreateJokesGenerator() => CreateBasicAgent( instructions: "Eres el Agente Generador de chistes (perfil creativo). Crea chistes breves y originales en TEXTO PLANO. No uses JSON ni herramientas ni funciones. Devuelve solo el chiste.", tools: Array.Empty<AIFunction>() ); public AIAgent CreateJokesReviewer() => CreateBasicAgent( instructions: "Eres el Revisor (perfil analítico) de chistes de programación. Puntúa con un entero 0..10 y da una justificación corta, específica y útil para mejorar. Política: puntuaciones de 7 o menos se consideran malas (<=7). Devuelve JSON EXACTO sin texto extra: {\"score\": <0..10>, \"rationale\": \"frase corta\"}.", tools: Array.Empty<AIFunction>() ); public AIAgent CreateJokesBoss() => CreateBasicAgent( instructions: "Eres el Jefe (perfil exigente y serio con toque de humor seco). Regla: rechaza automáticamente (decision=\"auto\") los chistes que no consideres buenos. Solo pide aprobación humana (decision=\"hitl\") cuando estés realmente seguro de que es MUY bueno y gracioso (típicamente 9-10). Puedes usar tools MCP (p. ej., list_jokes, index_search) si necesitas contexto del repositorio. Devuelve JSON EXACTO sin texto extra: {\"decision\": \"hitl\"|\"auto\", \"notes\": \"justificación muy corta, seria y con un guiño gracioso\"}.", tools: _defaultTools );





    }

    /// <summary>
    /// API estática legacy mantenida para compatibilidad con código existente; delega en DefaultAgentFactory singleton si disponible.
    /// </summary>
    public static class AgentFactory
    {
        public static AIAgent CreateBasicAgent(string? instructions = null, AIFunction[]? tools = null)
        {
            // Intentar resolver instancia registrada (para permitir mocks en tests)
            try
            {
                var fac = ServiceLocator.Get<IAgentFactory>();
                return fac.CreateBasicAgent(instructions, tools);
            }
            catch
            {
                // Fallback directo
            }
            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;
            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            return client.GetChatClient(model).CreateAIAgent(instructions: instructions, tools: tools);
        }
    }

    /// <summary>
    /// ServiceLocator muy ligero para casos donde no tenemos DI directo (evitar sobreuso).
    /// </summary>
    // Nota: Se reutiliza el ServiceLocator definido en JokesTools para evitar duplicados
}
