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

Demos actuales:
- 1) Hola Mundo: petición simple al agente y muestra la respuesta
- 2) Modo Stream: recibe actualizaciones en streaming del agente
- 3) AI Foundry (Persistent Agents): crea y ejecuta un agente persistente en un servicio compatible (ej.: Azure AI Foundry / Persistent Agents). Muestra creación del agente, ejecución en streaming y uso de credenciales basadas en Service Principal o Azure CLI.

### Demo 3 — AI Foundry (Persistent Agents)

La demo "AI Foundry" (implementada en `Demos/AiFoundryAgent.cs`) muestra cómo crear un agente persistente en un servicio de Persistent Agents y ejecutar un run en modo streaming. Es útil para ver un flujo más realista donde el agente se crea, se mantiene y se ejecuta con historial.

Requisitos y notas importantes:

- Endpoint/URL: la demo usa un endpoint del servicio de Persistent Agents (en el código se define como una constante). Si tu despliegue usa una URL distinta, modifica `Demos/AiFoundryAgent.cs` o añade configuración para leerla desde `.env`.
- Credenciales: la demo intenta elegir la credencial más adecuada:
    - Si defines `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_CLIENT_SECRET`, el demo usará `ClientSecretCredential` (Service Principal) y no necesitarás hacer `az login`.
    - Si no hay credenciales de Service Principal definidas, la demo usa `AzureCliCredential` (requiere haber hecho `az login`).
- Variables de entorno adicionales (opcional):
    - `AZURE_CLIENT_ID` — id de la aplicación (Service Principal)
    - `AZURE_TENANT_ID` — id del tenant
    - `AZURE_CLIENT_SECRET` — secreto del Service Principal

Cómo funciona la demo (resumen):

1. Selecciona la opción 3 en el menú principal.
2. El programa crea un agente persistente en el servicio de administración de agentes (si no existe), obtiene el Id y crea un hilo (AgentThread).
3. Ejecuta el agente en modo streaming y escribe las actualizaciones parciales en la consola.
4. Pulsa Enter para volver al menú.

Consejos de depuración:

- Si la demo informa errores de autorización, revisa que hayas definido las variables de entorno correctamente o que hayas hecho `az login`.
- Revisa la constante `endpoint` en `Demos/AiFoundryAgent.cs` si tu servicio usa otra ruta REST.


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

