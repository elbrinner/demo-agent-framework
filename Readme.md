# Demo Agent Framework

Proyecto demo para mostrar cómo usar Microsoft Agent Framework con Azure OpenAI. El objetivo es proporcionar ejemplos sencillos (demos) que ilustren distintas capacidades: petición simple, streaming y animación/voz del agente. El proyecto está diseñado para crecer y añadir nuevas demos de forma modular.

## Contenido

- `Demos/` : carpeta con las demos (HolaMundo, ModoStream, Animation, Speech, ...)
- `Config/Credentials.cs` : clase centralizada para leer credenciales desde variables de entorno o un archivo `.env`.
- `Program.cs` : menú de consola que permite seleccionar y ejecutar las demos.

## Requisitos

- .NET 9 SDK instalado
- Cuenta y recurso de Azure OpenAI con un deployment del modelo (o el servicio que use Microsoft Agent Framework)
- Variables de entorno o archivo `.env` con las credenciales necesarias

También recomendamos consultar el repositorio oficial del framework:

- Microsoft Agent Framework: https://github.com/microsoft/agent-framework/tree/main

## Instalación

1. Clona este repositorio y sitúate en la carpeta del proyecto:

   cd <ruta-del-proyecto>

2. Restaura las dependencias y compila:

   dotnet restore
   dotnet build

3. El proyecto ya incluye referencias a los paquetes relevantes en `demo-agent-framework.csproj`. Si quieres instalar adicionalmente paquetes o cambiar versiones, usa `dotnet add package <PackageName>`.

## Configuración de credenciales (.env)

Para evitar incrustar credenciales en el código se usa un único lugar central: `Config/Credentials.cs` que lee variables de entorno. Para desarrollo local puedes crear un archivo `.env` en la raíz del proyecto (no subirlo al repositorio).

Ejemplo de `.env` (añádelo a `.gitignore` si aún no está):

```
AZURE_OPENAI_ENDPOINT=https://<tu-endpoint>.openai.azure.com/
AZURE_OPENAI_KEY=<tu_api_key>
AZURE_OPENAI_MODEL=<nombre_del_despliegue_del_modelo>
```

- `AZURE_OPENAI_ENDPOINT`: URL del endpoint de Azure OpenAI.
- `AZURE_OPENAI_KEY`: clave API para autenticar las peticiones.
- `AZURE_OPENAI_MODEL`: nombre del deployment (deployment name) del modelo a usar.

El código del proyecto intenta cargar `.env` automáticamente y también acepta variables definidas en el entorno del sistema (por ejemplo, para CI/producción).

### Alternativas más seguras

- Para desarrollo: `dotnet user-secrets` (no se sube al repo).
- Para producción: Azure Key Vault y Managed Identity.

## Uso

1. Asegúrate de tener el `.env` o las variables de entorno definidas.
2. Ejecuta la aplicación:

   dotnet run --project demo-agent-framework.csproj

3. Usa el menú interactivo para seleccionar la demo que quieras ejecutar.

Demos actuales (detalle de las 6 demos)

Esta sección describe brevemente las demos disponibles en `Demos/` y qué esperar al ejecutarlas desde el menú principal.

- 1) Hola Mundo — `Demos/HolaMundo.cs`
    - Propósito: demo mínima que envía un prompt simple y muestra la respuesta completa (no streaming).
    - Comportamiento: crea un `AzureOpenAIClient` con `AZURE_OPENAI_ENDPOINT` y `AZURE_OPENAI_KEY`, construye un `AIAgent` y llama a `agent.RunAsync(prompt)`.
    - Uso: buena para verificar credenciales y que el endpoint/model estén bien configurados.

- 2) Modo Stream — `Demos/ModoStream.cs`
    - Propósito: demostrar la API de streaming del agente para recibir actualizaciones parciales mientras se genera la respuesta.
    - Comportamiento: usa `agent.RunStreamingAsync(prompt)` y itera `await foreach` mostrando `AgentRunResponseUpdate` en la consola.
    - Uso: útil para ver latencia/flujo de tokens y para interfaces que renderizan texto incrementalmente.

- 3) AI Foundry (Persistent Agents) — `Demos/AiFoundryAgent.cs`
    - Propósito: ejemplo de creación/uso de agentes persistentes (Persistent Agents) en un servicio compatible (p.ej. Azure AI Foundry / Persistent Agents).
    - Comportamiento clave:
        - Selecciona credenciales: usa `ClientSecretCredential` si están definidas (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`) o `AzureCliCredential` como fallback (requiere `az login`).
        - Crea el agente con `PersistentAgentsClient.Administration.CreateAgentAsync(...)` si no existe, guarda el Id en `.agents/{name}.id` y luego hace runs en streaming con `agent.RunStreamingAsync(prompt)`.
    - Notas: el endpoint del servicio está definido como constante en el fichero; considera moverlo a `.env` si necesitas configurarlo.

- 4) Ollama — `Demos/Ollama.cs`
    - Propósito: mostrar ejecución con un motor local (Ollama) para modelos desplegados en la máquina local.
    - Requisitos: tener Ollama instalado y modelos locales disponibles; por defecto usa `http://localhost:11434` y `llama3.1:8b` en el código.
    - Comportamiento: crea un `OllamaApiClient` y un `ChatClientAgent`, luego usa streaming para mostrar la respuesta.

- 5) AgentThread — `Demos/AgentThread.cs`
    - Propósito: enseñar el uso de hilos/threads del agente para mantener contexto entre turns.
    - Comportamiento: crea un `AIAgent` con instrucciones iniciales y obtiene `agent.GetNewThread()` para mantener el historial; ejecuta múltiples turns en streaming y demuestra que el agente recuerda el contexto.

- 6) AgentTools — `Demos/AgentTools.cs`
    - Propósito: ejemplo conceptual de uso de herramientas / funciones (tools) que el agente puede invocar (ej.: consultar el clima, recomendar platos).
    - Comportamiento: define funciones locales que actúan como herramientas (p. ej. `ObtenerClima`, `RecomendarPlato`) y crea un agente con dichas herramientas; luego ejecuta en streaming.
    - Nota: el código es ilustrativo y puede requerir adaptaciones según la versión del SDK (fábrica de funciones, firmas o forma de registrar tools pueden variar). Si ves errores al compilar esta demo, revisa las APIs del SDK que estés usando y activa `USE_REAL_AGENT` sólo si tienes las dependencias correctas.

Cómo ejecutar cualquiera de las demos

1. Asegúrate de haber configurado las variables en `.env` o en el entorno (al menos: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_KEY`, `AZURE_OPENAI_MODEL` para demos que usan Azure).
2. Desde la raíz del proyecto ejecuta:

     dotnet run --project demo-agent-framework.csproj

3. En el menú interactivo selecciona la demo (1–6) que quieras probar.

Notas comunes y recomendaciones

- Muchas demos usan las mismas variables `AZURE_OPENAI_*` centrales (revisa `Config/Credentials.cs`).
- Para la demo 3 (AI Foundry) la demo puede usar credenciales de Service Principal o `az login` según tu entorno; revisa `Demos/AiFoundryAgent.cs` si necesitas adaptar el endpoint.
- Para demos que usan motores locales (Ollama), asegúrate de tener el servicio corriendo localmente y que el puerto/endpoint en el código coincida con tu configuración.
- Si vas a subir el proyecto a un repositorio público, no subas tu `.env` ni claves; usa `.gitignore` y considera `dotnet user-secrets` o Key Vault para entornos compartidos.


## Cómo añadir nuevas demos

1. Crea un nuevo archivo en la carpeta `Demos/` con una clase estática que exponga `public static async Task RunAsync()`.
2. Implementa la lógica de la demo dentro de `RunAsync`.
3. Registra la nueva demo en `Program.cs` añadiendo una opción en el menú que llame a `TuNuevaDemo.RunAsync()`.

Ejemplo mínimo de demo:
```csharp
namespace demo_agent_framework.Demos
{
    public static class MiNuevaDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("Mi demo...");
            await Task.Delay(100);
        }
    }
}
```

## Habilitar el código real del agente

El proyecto contiene código protegido por el símbolo de compilación `USE_REAL_AGENT` para evitar errores si no se han instalado o configurado las librerías reales. Para activar el código real:

1. Añade las referencias/paquetes NuGet necesarios en `demo-agent-framework.csproj` (por ejemplo `Azure.AI.OpenAI`, `Microsoft.Agents.AI.OpenAI`, etc.).
2. Define la constante de compilación `USE_REAL_AGENT` en el `.csproj` o desde el IDE (por ejemplo dentro de un `PropertyGroup`):

```xml
<DefineConstants>USE_REAL_AGENT</DefineConstants>
```

3. Sustituye los valores placeholder de endpoint/key/model por tus credenciales o usa `Config/Credentials.cs` para leerlos desde el entorno.

## Seguridad y buenas prácticas

- Nunca subas `.env` ni claves al repositorio. Usa `.gitignore` (este proyecto ya ignora `.env`).
- Para equipos, usa `dotnet user-secrets` en desarrollo y Key Vault en producción.
- Limita permisos de las claves y rota las claves periódicamente.

## Extensión del proyecto

Este repositorio es un punto de partida. Puedes añadir demos adicionales que muestren:

- Integración con síntesis/voz y reconocimiento
- Animación de agentes y control de estados
- Flujos más complejos con prompt engineering y almacenamiento de contexto

## Recursos

- Microsoft Agent Framework: https://github.com/microsoft/agent-framework/tree/main
- Azure OpenAI: https://learn.microsoft.com/azure/cognitive-services/openai/

