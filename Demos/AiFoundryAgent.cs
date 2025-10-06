using System;
using System.Threading.Tasks;
using System.IO;
using Azure.Core;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;
using demo_agent_framework.Config;

namespace demo_agent_framework.Demos
{
    public static class AiFoundryAgent
    {
        public static async Task RunAsync()
        {
            // Encabezado de la demo
            Console.WriteLine();
            Console.WriteLine("=== Demo 3 - AI Foundry Agent ===");

            // --------------------------------------------------
            // Configuración básica de la demo
            // --------------------------------------------------
            // Endpoint del servicio de Persistent Agents (puedes cambiar por tu endpoint o leerlo desde .env)
            const string endpoint = "https://bri-ai-openai.services.ai.azure.com/api/projects/AIBRI";
            // El modelo se lee desde las credenciales centralizadas (Config/Credentials.cs)
            var model = Credentials.Model;

            // Prompt de ejemplo para ejecutar en el agente
            var prompt = "Ayudarme a crear un agente para mi tienda de informática";

            // Metadatos del agente que queremos crear (nombre, descripción, system prompt)
            var aiFoundryAgentName = "AgenteBri";
            var aiFoundryAgentDescription = "Agente de prueba para la tienda de informática de Bri";
            var aiFoundryAgentSystemPrompt = "Eres un agente experto en tiendas de informática y ayudas a los clientes a encontrar productos y resolver dudas técnicas.";

            // --------------------------------------------------
            // Selección de credencial para autenticación con Azure
            // --------------------------------------------------
            // Preferimos credenciales de Service Principal (ClientSecretCredential) si están definidas en .env
            // Si no, hacemos fallback a AzureCliCredential (requiere `az login`).
            TokenCredential credential;
            var clientId = Credentials.ClientId;
            var tenantId = Credentials.TenantId;
            var clientSecret = Credentials.ClientSecret;

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientSecret))
            {
                // Usar credenciales de aplicación (Service Principal)
                Console.WriteLine("Usando ClientSecretCredential con credenciales de aplicación.");
                credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }
            else
            {
                // Fallback a credenciales del CLI de Azure (usuario debe haber hecho `az login`)
                Console.WriteLine("Usando AzureCliCredential. Asegúrate de haber hecho 'az login' previamente.");
                credential = new AzureCliCredential();
            }

            // --------------------------------------------------
            // Crear cliente de Persistent Agents con la credencial seleccionada
            // --------------------------------------------------
            PersistentAgentsClient client = new PersistentAgentsClient(endpoint, credential);

            try
            {
                // --------------------------------------------------
                // Almacenamiento local simple para evitar crear agentes duplicados
                // - Guardamos el Id del agente en .agents/{name}.id la primera vez que lo creamos
                // - En ejecuciones posteriores, leemos ese fichero para recuperar el Id y usar el agente existente
                // --------------------------------------------------
                var repoRoot = Directory.GetCurrentDirectory();
                var agentsDir = Path.Combine(repoRoot, ".agents");
                Directory.CreateDirectory(agentsDir);
                var agentIdFile = Path.Combine(agentsDir, aiFoundryAgentName + ".id");

                AIAgent agent = null;

                // 1) Intentar usar Id guardado si existe
                if (File.Exists(agentIdFile))
                {
                    var existingId = File.ReadAllText(agentIdFile).Trim();
                    if (!string.IsNullOrEmpty(existingId))
                    {
                        try
                        {
                            // Obtener AIAgent por Id guardado
                            agent = await client.GetAIAgentAsync(existingId);
                            Console.WriteLine($"Agente con nombre '{aiFoundryAgentName}' ya existe (id guardado). Id: {existingId}");
                        }
                        catch
                        {
                            // Si no se puede obtener por id (p. ej. borrado o id inválido), continuamos para crear uno nuevo
                            Console.WriteLine("Id de agente guardado no válido o agente no encontrado. Se intentará crear uno nuevo.");
                            agent = null;
                        }
                    }
                }

                // 2) Si no encontramos un agente por id guardado, intentar buscar por nombre (opcional)
                //    NOTA: el SDK puede no exponer una lista directa; si tu servicio lo permite puedes implementar
                //    una búsqueda por nombre aquí. Para simplicidad usamos el fichero local + creación si no existe.

                // 3) Si todavía no tenemos un AIAgent, crear uno nuevo
                if (agent == null)
                {
                    // Crear el agente persistente en el servicio de administración
                    var createResponse = await client.Administration.CreateAgentAsync(
                        model,
                        aiFoundryAgentName,
                        aiFoundryAgentDescription,
                        aiFoundryAgentSystemPrompt
                    );

                    var createdMeta = createResponse.Value;
                    Console.WriteLine($"Agente creado. Id: {createdMeta.Id}");

                    // Guardar id localmente para próximas ejecuciones
                    try
                    {
                        File.WriteAllText(agentIdFile, createdMeta.Id);
                    }
                    catch
                    {
                        // Ignorar errores al escribir el fichero, no es crítico para la demo
                    }

                    // Obtener AIAgent a partir del Id creado
                    agent = await client.GetAIAgentAsync(createdMeta.Id);
                }

                // --------------------------------------------------
                // Ejecutar la interacción con el agente (usando AIAgent obtenido o creado)
                // --------------------------------------------------
                if (agent is not null)
                {
           

                    // Ejecutar en modo streaming para recibir actualizaciones parciales
                    await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync(prompt))
                    {
                        // Cada 'update' puede contener texto parcial o eventos; lo mostramos en consola
                        Console.Write(update);
                    }
                }
                else
                {
                    Console.WriteLine("No se pudo obtener ni crear el agente. Revisa permisos y configuración.");
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores genérico: mostrar detalle para depuración
                Console.WriteLine("Error en la operación de agentes persistentes:");
                Console.WriteLine(ex.ToString());
            }

            // Fin de la demo: esperar que el usuario pulse Enter para volver al menú
            Console.WriteLine();
            Console.WriteLine("Demostración finalizada. Pulsa Enter para volver al menú principal.");
            Console.ReadLine();
        }
    }
}