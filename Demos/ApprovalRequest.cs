using Azure.AI.OpenAI;
using demo_agent_framework.Config;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using Microsoft.Agents.AI;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI;

namespace demo_agent_framework.Demos
{
    public static class ApprovalRequest
    {
        public static async Task RunAsync(int caso = 3)
        {
            Console.WriteLine("=== Demo 7: Revisor literario con aprobación humana ===");

            var endpoint = Credentials.Endpoint;
            var apiKey = Credentials.ApiKey;
            var model = Credentials.Model;

            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));

            // Tool: Revisor literario
            [Description("Revisa el texto generado y decide si está listo.")]
            static string RevisarTexto([Description("Texto a revisar.")] string texto)
            {
                if (texto.Contains("aventura"))
                    return "Aprobado por el revisor.";
                if (texto.Contains("confusión") || texto.Length < 50)
                    return "Necesita revisión humana.";
                return "Texto no válido para revisión.";
            }

            // Crear agente con herramienta envuelta en aprobación
            var agent = client.GetChatClient(model).CreateAIAgent(
                instructions: "Eres un revisor literario. Evalúas textos y decides si están listos. Si tienes dudas, solicita aprobación humana.",
                tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RevisarTexto))]
            );

            // Selección de prompt según el caso
            string prompt = caso switch
            {
                1 => "Revisa este texto: 'Había una vez una aventura épica en el bosque encantado.'",
                2 => "Revisa este texto: 'La historia tiene elementos de confusión y misterio, pero no está clara.'",
                3 => "Revisa este texto: 'Hola, ¿cómo estás?'",
                _ => "Revisa este texto: 'Texto genérico sin contexto.'"
            };

            Console.WriteLine($"\n[Usuario] {prompt}");

            var hilo = agent.GetNewThread();
            var respuesta = await agent.RunAsync(prompt, hilo);
            var solicitudes = respuesta.UserInputRequests.ToList();

            #pragma warning disable MEAI001
            Console.WriteLine($"\n[LOG] ¿Solicita aprobación humana? {solicitudes.Count > 0}");

            foreach (var solicitud in solicitudes.OfType<FunctionApprovalRequestContent>())
            {
                Console.WriteLine($"[LOG] Solicitud de aprobación para función: {solicitud.FunctionCall.Name}");
                Console.WriteLine($"[LOG] Argumentos: {string.Join(", ", solicitud.FunctionCall.Arguments.Select(kv => $"{kv.Key}: {kv.Value}"))}");
                Console.Write("¿Apruebas esta acción? (Y/N): ");
                var aprobado = Console.ReadLine()?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
                var respuestaUsuario = new ChatMessage(ChatRole.User, [solicitud.CreateResponse(aprobado)]);
                respuesta = await agent.RunAsync([respuestaUsuario], hilo);
            }

            Console.WriteLine($"\n[Agente] {respuesta}");
            Console.WriteLine("\nPulsa Enter para continuar.");
            Console.ReadLine();
        }

    }
}
