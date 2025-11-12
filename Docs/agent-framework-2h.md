# Microsoft Agent Framework: Guía Completa para Charla de 2 Horas

**Versión**: 1.0  
**Duración**: 120 minutos  
**Target**: Desarrolladores .NET  
**Nivel**: Principiante a Avanzado  

---

## 📋 Tabla de Contenidos

1. [Estructura General](#estructura-general)
2. [Outline Detallado](#outline-detallado)
3. [Contenido de Slides](#contenido-de-slides)
4. [Demos Detalladas](#demos-detalladas)
5. [Código de Referencia](#código-de-referencia)
6. [Recursos Adicionales](#recursos-adicionales)

---

## Estructura General

### Distribución del Tiempo

- **Teoría**: 74 minutos (62%)
- **Demos**: 46 minutos (38%)
- **Total**: 120 minutos

### Secciones

1. **Introducción y Fundamentos** - 20 min
2. **Agents: El Cerebro del Sistema** - 25 min
3. **Workflows: Orquestación Inteligente** - 30 min
4. **Capacidades Avanzadas** - 25 min
5. **Casos de Uso y Best Practices** - 15 min
6. **Conclusión y Recursos** - 5 min

### Demos Incluidas

| # | Demo | Duración | Complejidad |
|---|------|----------|-------------|
| 1 | Primer Agent simple (Haiku Bot) | 3 min | Básico |
| 2 | Agent con múltiples proveedores | 4 min | Básico |
| 3 | Weather Agent con custom tools | 6 min | Intermedio |
| 4 | Workflow secuencial simple | 5 min | Intermedio |
| 5 | Concurrent execution paralela | 8 min | Avanzado |
| 6 | Human-in-the-Loop workflow | 6 min | Avanzado |
| 7 | Background task con pause/resume | 4 min | Avanzado |
| 8 | Sistema complejo - Order Processing | 10 min | Experto |

---

## Outline Detallado

### 1. Introducción y Fundamentos (20 min)

#### 1.1 ¿Qué son los AI Agents? (5 min)

**Puntos clave:**
- Diferencia entre chatbot tradicional y AI Agent
- Componentes: Perception, Reasoning, Action, Memory
- El ciclo: Input → Thread → Agent → Tool → Response

**Narrativa:**
> "Imagina que tienes un asistente personal que no solo responde preguntas, sino que puede usar herramientas, recordar conversaciones y tomar decisiones. Eso es un AI Agent."

**Comparación visual:**
- ❌ **Chatbot**: Respuestas predefinidas, sin contexto, limitado
- ✅ **AI Agent**: Aprende, usa tools, mantiene memoria, autónomo

#### 1.2 Historia y Evolución (5 min)

**Timeline:**
- **2023**: Semantic Kernel (Microsoft) - Framework enterprise-ready
- **2023**: AutoGen (MS Research) - Multi-agent research project
- **2024**: Convergencia de ambos proyectos
- **Oct 2025**: Microsoft Agent Framework Public Preview
- **Hoy**: Framework unificado production-ready

**Por qué la unificación:**
- Semantic Kernel aportó: estabilidad, features enterprise
- AutoGen aportó: patrones avanzados multi-agent, research innovations
- Agent Framework: lo mejor de ambos mundos

#### 1.3 Arquitectura del Framework (10 min)

**Principio fundamental:**
> "Separar Intelligence (Agents) de Orchestration (Workflows)"

**Componentes clave:**
1. **Agents** - Intelligence Layer: LLM-driven reasoning
2. **Workflows** - Orchestration Layer: Structured processes
3. **Tools & MCP** - Integration Layer: External capabilities
4. **Observability** - Monitoring Layer: OpenTelemetry

**Lifecycle completo:**
```
Input → Thread → Agent → Tool → Middleware → Workflow → Events → Output
```

**💡 DEMO 1: Primer Agent Simple (3 min)**

Mostrar el código más simple posible:

```csharp
using OpenAI;
using Azure.Identity;

var agent = new OpenAIClient(
    new BearerTokenPolicy(new AzureCliCredential(), 
        "https://ai.azure.com/.default"),
    new OpenAIClientOptions() { 
        Endpoint = new Uri("https://<resource>.openai.azure.com/openai/v1") 
    })
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(
        name: "HaikuBot", 
        instructions: "You are an upbeat assistant that writes beautifully."
    );

Console.WriteLine(await agent.RunAsync("Write a haiku about Microsoft Agent Framework."));
```

**Explicar:**
- Solo 10 líneas de código
- Cliente de Azure OpenAI
- Configuración mínima: name + instructions
- RunAsync retorna respuesta directa

---

### 2. Agents: El Cerebro del Sistema (25 min)

#### 2.1 Creación de Agents Básicos (8 min)

**Tipos de Agents:**

| Tipo | Provider | Uso |
|------|----------|-----|
| ChatCompletionAgent | Genérico | Máxima flexibilidad |
| OpenAI Responses Agent | OpenAI | Optimizado para OpenAI |
| Azure OpenAI Responses Agent | Azure | Integración Azure |
| AzureAIAgent | Azure AI Foundry | Managed service |
| CopilotStudioAgent | M365 Copilot | Enterprise integration |

**Configuración de Agent:**

```csharp
var agent = chatClient.CreateAIAgent(
    name: "CustomerServiceBot",              // Identificador
    instructions: "You help customers...",   // System prompt
    description: "Handles billing queries",  // Para orchestration
    tools: [tool1, tool2],                   // Funciones disponibles
    middleware: [loggingMiddleware]          // Interceptores
);
```

**AgentThread: Gestión de Estado**

```csharp
AgentThread thread = agent.GetNewThread();
var response1 = await agent.RunAsync("Hello", thread);
var response2 = await agent.RunAsync("What did I say?", thread); 
// Mantiene contexto de la conversación
```

**Streaming vs Non-Streaming:**

```csharp
// Non-streaming
var result = await agent.RunAsync("Question");
Console.WriteLine(result.Text);

// Streaming
await foreach (var update in agent.RunStreamAsync("Question"))
{
    Console.Write(update.Text);
}
```

**💡 DEMO 2: Agent con Múltiples Proveedores (4 min)**

Mostrar cómo cambiar de Azure a OpenAI sin modificar lógica:

```csharp
// Azure OpenAI
var azureAgent = new AzureOpenAIClient(...)
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(name: "Bot", instructions: "...");

// OpenAI directo
var openaiAgent = new OpenAIClient("<api-key>")
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(name: "Bot", instructions: "...");

// Mismo código para ambos
await azureAgent.RunAsync("Test");
await openaiAgent.RunAsync("Test");
```

#### 2.2 Tools y Function Calling (12 min)

**¿Qué son los Tools?**
> "Funciones que el agent puede llamar automáticamente cuando las necesita"

**Flujo:**
1. Usuario: "¿Qué clima hay en Madrid?"
2. LLM detecta que necesita info del clima
3. Llama automáticamente a `get_weather("Madrid")`
4. Recibe resultado: "Soleado, 22°C"
5. Compone respuesta natural: "En Madrid hace sol y 22 grados"

**Definir un Tool:**

```csharp
// Método C# estándar
public static string GetWeather(
    [Description("The city to get weather for")] 
    string location)
{
    // Simular API call
    return $"Weather in {location}: Sunny, 22°C";
}

// Crear agent con tool
var agent = chatClient.CreateAIAgent(
    name: "WeatherBot",
    instructions: "You help with weather information",
    tools: [AIFunctionFactory.Create(GetWeather)]
);
```

**Parámetros complejos:**

```csharp
public record SearchParameters(
    [Description("Search query")] string Query,
    [Description("Max results")] int Limit = 10,
    [Description("Filter by date")] DateTime? Since = null
);

public static string SearchWeb(SearchParameters params)
{
    // Implementación
}
```

**Tool selection automática:**
- El LLM analiza el contexto
- Decide qué tool usar
- Extrae parámetros del input del usuario
- Llama la función
- Usa el resultado para responder

**💡 DEMO 3: Weather Agent con Custom Tools (6 min)**

```csharp
using System.ComponentModel;
using Microsoft.Agents.AI;

// 1. Definir tools
public static string GetWeather(
    [Description("City name")] string location)
{
    var conditions = new[] { "sunny", "cloudy", "rainy", "stormy" };
    var temp = Random.Shared.Next(10, 30);
    return $"Weather in {location}: {conditions[0]}, {temp}°C";
}

public static string GetForecast(
    [Description("City name")] string location,
    [Description("Number of days")] int days = 3)
{
    return $"{days}-day forecast for {location}: ...";
}

// 2. Crear agent con tools
var agent = chatClient.CreateAIAgent(
    name: "WeatherBot",
    instructions: "You are a helpful weather assistant",
    tools: [
        AIFunctionFactory.Create(GetWeather),
        AIFunctionFactory.Create(GetForecast)
    ]
);

// 3. Test de function calling
Console.WriteLine("User: What's the weather in Madrid?");
var response1 = await agent.RunAsync("What's the weather in Madrid?");
Console.WriteLine($"Agent: {response1.Text}");

Console.WriteLine("\nUser: Give me a 5-day forecast for Barcelona");
var response2 = await agent.RunAsync("Give me a 5-day forecast for Barcelona");
Console.WriteLine($"Agent: {response2.Text}");
```

**Explicar:**
- Agent decide automáticamente qué función llamar
- Extrae parámetros del texto natural
- Llama función con parámetros correctos
- Compone respuesta natural con resultado

#### 2.3 Model Context Protocol (MCP) (5 min)

**¿Qué es MCP?**
> "Estándar abierto para que models y tools se comuniquen, creando un ecosistema de herramientas reutilizables"

**Ventajas:**
- ✅ Interoperabilidad entre frameworks
- ✅ Ecosistema de tools compartido
- ✅ No vendor lock-in
- ✅ Seguridad y aprobaciones centralizadas

**3 tipos de conexión:**

1. **MCPStdioTool** - Proceso local
```csharp
var mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new() {
        Command = "npx",
        Arguments = ["-y", "@modelcontextprotocol/server-github"]
    })
);
```

2. **MCPStreamableHTTPTool** - Servidor remoto HTTP/SSE
```csharp
var mcpTool = new MCPStreamableHTTPTool(
    name: "Microsoft Learn",
    url: "https://learn.microsoft.com/api/mcp"
);
```

3. **Hosted MCP** - Managed en Azure AI Foundry
- Aprobación previa de tools
- Authentication integrada
- Observability completa

**Ejemplos de MCP servers:**
- GitHub MCP: manage repos, issues, PRs
- Filesystem MCP: read/write files
- AWS Docs MCP: query documentation
- Slack MCP: send messages
- Database MCP: query databases

---

### 3. Workflows: Orquestación Inteligente (30 min)

#### 3.1 Diferencia: Agent vs Workflow (5 min)

**Comparación fundamental:**

| Aspecto | Agent | Workflow |
|---------|-------|----------|
| Control | LLM decide pasos | Desarrollador define flujo |
| Naturaleza | Dinámico, flexible | Predefinido, estructurado |
| Uso | Razonamiento adaptativo | Procesos de negocio |
| Ejemplo | Chatbot de soporte | Pipeline de aprobación |

**Key insight:**
> "Workflows CONTIENEN agents como componentes. Es como un director de orquesta coordinando músicos."

**Cuándo usar cada uno:**
- **Agent solo**: Tareas conversacionales, exploración, razonamiento flexible
- **Workflow**: Procesos estructurados, múltiples pasos, coordinación compleja

#### 3.2 Componentes de Workflows (10 min)

**1. Executors** - Nodos de procesamiento

```csharp
// Executor puede ser:
// - Un agent
var agentExecutor = new AgentExecutor(searchAgent);

// - Una función custom
public class CustomExecutor : Executor<InputType, OutputType>
{
    [Handler]
    public async Task<OutputType> ProcessAsync(InputType input)
    {
        // Lógica custom
        return result;
    }
}
```

**2. Edges** - Flujo de datos

```csharp
// Edge simple
.AddEdge(executor1, executor2)

// Edge condicional
.AddEdge(executor1, executor2, condition: msg => msg.Status == "approved")

// Múltiples edges (parallel)
.AddEdge(router, agent1)
.AddEdge(router, agent2)
.AddEdge(router, agent3)
```

**3. WorkflowBuilder** - Construcción del grafo

```csharp
var workflow = new WorkflowBuilder()
    .SetStartExecutor(firstExecutor)
    .AddEdge(firstExecutor, secondExecutor)
    .AddEdge(secondExecutor, thirdExecutor)
    .Build();
```

**4. Events** - Observabilidad

```csharp
await foreach (var evt in workflow.RunStreamAsync(input))
{
    switch (evt)
    {
        case WorkflowStartedEvent start:
            Console.WriteLine("Workflow started");
            break;
        case ExecutorCompleteEvent complete:
            Console.WriteLine($"{complete.ExecutorName} completed");
            break;
        case WorkflowOutputEvent output:
            Console.WriteLine($"Final output: {output.Data}");
            break;
    }
}
```

**5. Supersteps** - Ejecución por fases
- Inspirado en el modelo Pregel (Google)
- Ejecuta todos los executors de una fase
- Luego pasa a la siguiente fase
- Determinismo completo
- Puntos perfectos para checkpointing

**💡 DEMO 4: Workflow Secuencial Simple (5 min)**

```csharp
using Microsoft.Agents.Workflows;

// 1. Crear agents
var researchAgent = chatClient.CreateAIAgent(
    name: "Researcher",
    instructions: "Research topics and gather information",
    tools: [WebSearchTool]
);

var summaryAgent = chatClient.CreateAIAgent(
    name: "Summarizer",
    instructions: "Summarize information concisely"
);

var formatAgent = chatClient.CreateAIAgent(
    name: "Formatter",
    instructions: "Format output as markdown"
);

// 2. Build workflow
var workflow = new WorkflowBuilder()
    .SetStartExecutor(new AgentExecutor(researchAgent))
    .AddEdge(researchAgent, summaryAgent)
    .AddEdge(summaryAgent, formatAgent)
    .Build();

// 3. Execute
Console.WriteLine("Starting workflow: Research → Summarize → Format");
await foreach (var evt in workflow.RunStreamAsync("AI Agents in 2025"))
{
    if (evt is WorkflowOutputEvent output)
    {
        Console.WriteLine($"\nFinal Result:\n{output.Data}");
    }
}
```

**Explicar:**
- Flujo lineal A → B → C
- Cada executor procesa output del anterior
- Type-safe message passing
- Observabilidad vía events

#### 3.3 Patrones de Orquestación (15 min)

**1. Sequential Pattern** - Flujo lineal

```
Input → Agent1 → Agent2 → Agent3 → Output
```

**Uso:** Pipelines de procesamiento donde cada paso depende del anterior

---

**2. Concurrent Pattern** - Ejecución paralela

```
                ┌─→ Agent1 ─┐
Input → Router ─┼─→ Agent2 ─┼→ Aggregator → Output
                └─→ Agent3 ─┘
```

**Ventaja:** Speedup de 3x si agents son independientes

**Código:**
```csharp
var workflow = new WorkflowBuilder()
    .SetStartExecutor(router)
    // Parallel execution
    .AddEdge(router, agent1)
    .AddEdge(router, agent2)
    .AddEdge(router, agent3)
    // Convergence
    .AddEdge(agent1, aggregator)
    .AddEdge(agent2, aggregator)
    .AddEdge(agent3, aggregator)
    .Build();
```

---

**3. Handoff Pattern** - Delegación dinámica

```
Agent1 → (decide) → Agent2 o Agent3
```

**Uso:** Escalamiento a especialistas según contexto

---

**4. Magentic Pattern** - Manager + Specialists

```
Manager
  ├→ Specialist1
  ├→ Specialist2
  └→ Specialist3
```

**Uso:** Proyectos complejos donde manager planifica y delega

---

**5. Hierarchical Pattern** - Multi-nivel

```
TopManager
  ├→ DepartmentManager1
  │   ├→ Worker1
  │   └→ Worker2
  └→ DepartmentManager2
      ├→ Worker3
      └→ Worker4
```

**Uso:** Organizaciones grandes con múltiples niveles

---

**💡 DEMO 5: Concurrent Execution - Investigación Paralela (8 min)**

**Escenario:** Investigar un tema consultando 3 fuentes simultáneamente

```csharp
using Microsoft.Agents.Workflows;

// 1. Crear agents especializados
var webSearchAgent = chatClient.CreateAIAgent(
    name: "WebSearcher",
    instructions: "Search the web for information",
    tools: [WebSearchTool]
);

var dbAgent = chatClient.CreateAIAgent(
    name: "DatabaseQuery",
    instructions: "Query internal database"
);

var apiAgent = chatClient.CreateAIAgent(
    name: "APIConsumer",
    instructions: "Call external APIs for data"
);

var synthesisAgent = chatClient.CreateAIAgent(
    name: "Synthesizer",
    instructions: "Combine and synthesize information from multiple sources"
);

// 2. Create router executor
public class RouterExecutor : Executor<string, string>
{
    [Handler]
    public async Task<string> RouteAsync(string input)
    {
        // Distribuye input a todos los agents
        return input;
    }
}

// 3. Build concurrent workflow
var workflow = new WorkflowBuilder()
    .SetStartExecutor(new RouterExecutor())
    // CONCURRENT: Estos 3 ejecutan en PARALELO
    .AddEdge(router, webSearchAgent)
    .AddEdge(router, dbAgent)
    .AddEdge(router, apiAgent)
    // CONVERGENCE: Todos envían a synthesizer
    .AddEdge(webSearchAgent, synthesisAgent)
    .AddEdge(dbAgent, synthesisAgent)
    .AddEdge(apiAgent, synthesisAgent)
    .Build();

// 4. Execute y medir tiempo
var stopwatch = Stopwatch.StartNew();
Console.WriteLine("🚀 Starting concurrent research...");

await foreach (var evt in workflow.RunStreamAsync("Microsoft Agent Framework"))
{
    if (evt is ExecutorCompleteEvent complete)
    {
        Console.WriteLine($"✓ {complete.ExecutorName} completed at {stopwatch.ElapsedMilliseconds}ms");
    }
    if (evt is WorkflowOutputEvent output)
    {
        Console.WriteLine($"\n📊 Total time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Result: {output.Data}");
    }
}
```

**Explicar:**
- Sin concurrencia: 15s (5s × 3 agents)
- Con concurrencia: 5s (max de todos)
- **Speedup de 3x**
- Aggregator combina resultados
- Type-safe message passing garantiza correctitud

---

### 4. Capacidades Avanzadas (25 min)

#### 4.1 Human-in-the-Loop (HITL) (12 min)

**¿Por qué HITL?**
- ✅ Decisiones críticas requieren supervisión humana
- ✅ Aprobaciones en workflows sensibles (pagos, contratos)
- ✅ Validación de outputs del LLM antes de acción
- ✅ Feedback loop para mejora continua
- ✅ Compliance y auditoría

**Componentes del sistema HITL:**

1. **RequestInfoMessage** - Estructura tipada del request
```csharp
public record ApprovalRequest(
    string OrderId,
    decimal Amount,
    string Customer
) : RequestInfoMessage;
```

2. **RequestInfoExecutor** - Coordinador
```csharp
var approvalExecutor = new RequestInfoExecutor<ApprovalRequest>();
```

3. **RequestInfoEvent** - Evento cuando workflow pausa
```csharp
if (evt is RequestInfoEvent<ApprovalRequest> req)
{
    // Workflow está pausado, esperando respuesta
}
```

4. **SendResponseAsync** - Enviar respuesta humana
```csharp
await workflow.SendResponseAsync(requestId, approvalDecision);
// Workflow continúa desde aquí
```

**Flujo completo:**

```
1. Executor procesa → genera RequestInfoEvent
2. Workflow PAUSA automáticamente
3. UI muestra request al usuario
4. Usuario toma decisión
5. SendResponseAsync con respuesta
6. Workflow RESUME desde punto exacto
7. Siguiente executor recibe decisión
```

**Casos de uso reales:**
- Aprobación de órdenes de compra >$10k
- Revisión de emails antes de enviar
- Validación de cambios en producción
- Aprobación de generación de código
- Revisión de documentos legales

**💡 DEMO 6: HITL Workflow - Selección de Candidatos (6 min)**

**Escenario:** Sistema de búsqueda de candidatos que requiere selección humana

```csharp
using Microsoft.Agents.Workflows;

// 1. Define request message
public record CandidateSelectionRequest(
    List<string> Candidates,
    string Position
) : RequestInfoMessage;

public record CandidateSelectionResponse(
    string SelectedCandidate
);

// 2. Create agents
var searchAgent = chatClient.CreateAIAgent(
    name: "CandidateSearcher",
    instructions: "Search for job candidates based on criteria"
);

var detailAgent = chatClient.CreateAIAgent(
    name: "DetailAnalyzer",
    instructions: "Provide detailed analysis of selected candidate"
);

// 3. Create HITL executor
var selectionExecutor = new RequestInfoExecutor<
    CandidateSelectionRequest,
    CandidateSelectionResponse
>();

// 4. Build workflow
var workflow = new WorkflowBuilder()
    .SetStartExecutor(new AgentExecutor(searchAgent))
    .AddEdge(searchAgent, selectionExecutor)  // PAUSA AQUÍ
    .AddEdge(selectionExecutor, detailAgent)
    .Build();

// 5. Execute con HITL
Console.WriteLine("🔍 Starting candidate search workflow...\n");

await foreach (var evt in workflow.RunStreamAsync("Find .NET developers"))
{
    if (evt is RequestInfoEvent<CandidateSelectionRequest> req)
    {
        // ⏸️ WORKFLOW PAUSADO
        Console.WriteLine("⏸️  Workflow paused for human input");
        Console.WriteLine($"Position: {req.Data.Position}");
        Console.WriteLine("Available candidates:");
        
        for (int i = 0; i < req.Data.Candidates.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {req.Data.Candidates[i]}");
        }
        
        // Simular selección humana
        Console.Write("\n👤 Select candidate (1-3): ");
        var choice = Console.ReadLine();
        var selected = req.Data.Candidates[int.Parse(choice) - 1];
        
        // ▶️ RESUME WORKFLOW
        var response = new CandidateSelectionResponse(selected);
        await workflow.SendResponseAsync(req.RequestId, response);
        
        Console.WriteLine($"✓ Selected: {selected}");
        Console.WriteLine("▶️  Workflow resumed\n");
    }
    
    if (evt is WorkflowOutputEvent output)
    {
        Console.WriteLine($"📋 Detailed Analysis:\n{output.Data}");
    }
}
```

**Puntos clave a explicar:**
- Workflow se pausa AUTOMÁTICAMENTE al llegar a RequestInfoExecutor
- Estado se preserva completamente
- UI puede ser web, consola, mobile, etc.
- Type-safe: el tipo del request y response están validados
- Resume es instantáneo con continuation

#### 4.2 Background Responses y Continuation Tokens (8 min)

**El problema:**
- 🔴 Operaciones largas (generar reporte de 50 páginas)
- 🔴 Network timeouts interrumpen
- 🔴 Usuario cierra laptop
- 🔴 Usuario necesita hacer otra consulta rápida
- 🔴 Empezar de cero = desperdicio de tokens y tiempo

**La solución: Background Responses**
> "Continuation tokens son como bookmarks del estado del agent"

**Cómo funciona:**

1. Habilitar background responses
```csharp
var options = new AgentRunOptions 
{ 
    AllowBackgroundResponses = true 
};
```

2. Agent retorna continuation token en cada update
```csharp
await foreach (var update in agent.RunStreamAsync(input, options))
{
    Console.Write(update.Text);
    var token = update.ContinuationToken; // Guardar
}
```

3. Resume desde token
```csharp
options.ContinuationToken = savedToken;
await agent.RunStreamAsync(options); // Continúa desde bookmark
```

**Casos de uso:**
- ✅ Generación de código complejo (30+ min)
- ✅ Research reports largos
- ✅ Resiliencia ante network issues
- ✅ Workflows interactivos (pause para clarification)
- ✅ Mobile apps con conexiones inestables

**Non-Streaming approach:**

```csharp
// Start
var response = await agent.RunAsync(input, options);

// Poll hasta completar
while (response.ContinuationToken != null)
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    options.ContinuationToken = response.ContinuationToken;
    response = await agent.RunAsync(options);
}

Console.WriteLine(response.Text);
```

**💡 DEMO 7: Background Responses con Pause/Resume (4 min)**

```csharp
using Microsoft.Agents.AI;

var agent = chatClient.CreateAIAgent(
    name: "LongTaskBot",
    instructions: "You write very detailed, long-form content"
);

var options = new AgentRunOptions 
{ 
    AllowBackgroundResponses = true 
};

string? savedToken = null;

// 1. Start long operation
Console.WriteLine("🚀 Starting long task (writing novel)...\n");
int chunkCount = 0;

await foreach (var update in agent.RunStreamAsync(
    "Write a very long novel about space otters", 
    options))
{
    Console.Write(update.Text);
    savedToken = update.ContinuationToken;
    
    chunkCount++;
    if (chunkCount == 10) // Simular interrupción
    {
        Console.WriteLine("\n\n⏸️  PAUSING... (network issue simulated)");
        break;
    }
}

// 2. Do other quick work
Console.WriteLine("\n\n💬 Quick question while long task is paused:");
var quickResponse = await agent.RunAsync("What's 2+2?");
Console.WriteLine($"Agent: {quickResponse.Text}");

// 3. Resume from token
Console.WriteLine("\n\n▶️  RESUMING long task from saved token...\n");
options.ContinuationToken = savedToken;

await foreach (var update in agent.RunStreamAsync(options))
{
    Console.Write(update.Text); // Continúa desde donde quedó
    
    if (update.ContinuationToken == null)
    {
        Console.WriteLine("\n\n✅ Task completed!");
        break;
    }
}
```

**Explicar:**
- Sin continuation tokens: empezar de cero, perder progreso
- Con continuation tokens: pause/resume sin pérdida
- Quick queries intercaladas
- Network resilience
- Costo optimizado (no regenerar)

#### 4.3 Checkpointing y State Management (5 min)

**¿Qué es Checkpointing?**
> "Guardar el estado COMPLETO del workflow en puntos específicos para recuperación"

**Cuándo se crean checkpoints:**
- Al final de cada **superstep**
- Después de operaciones críticas
- Antes/después de HITL requests
- En puntos definidos por desarrollador

**Qué se guarda:**
- ✅ Estado de todos los executors
- ✅ Mensajes pendientes en la cola
- ✅ Shared state entre executors
- ✅ Pending requests/responses
- ✅ Metadata del workflow

**Uso básico:**

```csharp
// 1. Create checkpoint manager
var checkpointManager = new CheckpointManager();
var checkpoints = new List<CheckpointInfo>();

// 2. Run with checkpointing
var checkpointedRun = await InProcessExecution
    .StreamAsync(workflow, input, checkpointManager);

await foreach (var evt in checkpointedRun.Run.WatchStreamAsync())
{
    // Capturar checkpoints
    if (evt is SuperStepCompletedEvent superStep)
    {
        checkpoints.Add(new CheckpointInfo(
            superStep.SuperStepId,
            superStep.Timestamp
        ));
    }
}

// 3. Resume from checkpoint
var checkpoint = checkpoints[2]; // Elegir punto
var resumed = await InProcessExecution.StreamAsync(
    workflow,
    checkpoint: checkpoint,
    checkpointManager: checkpointManager
);
```

**Casos de uso:**
- 🔧 **Failure recovery**: Server crash → resume desde último checkpoint
- 🕐 **Time-travel debugging**: Replay desde checkpoint X
- 🏢 **Auditing**: Ver estado exacto en momento T
- 🌍 **Migration**: Mover workflow entre entornos
- 📊 **Long-running jobs**: Progreso incremental (batch nocturno)

**Beneficios:**
- No perder horas de procesamiento por un fallo
- Debugging preciso de workflows complejos
- Compliance y trazabilidad
- Optimización de costos

---

### 5. Casos de Uso y Best Practices (15 min)

#### 5.1 Casos de Uso Empresariales (8 min)

**1. Customer Service Multi-Agent**

**Arquitectura:**
```
User Query → Router Agent
  ├→ Billing Specialist Agent
  ├→ Technical Support Agent
  ├→ Account Management Agent
  └→ Escalation Agent (HITL)
```

**Features:**
- Concurrent triage de consulta
- Handoff a especialista correcto
- HITL para casos complejos
- Checkpointing de conversación
- Integration con CRM via MCP

---

**2. Research & Reporting**

**Workflow:**
```
Research Request
  ├→ Web Search (concurrent)
  ├→ Database Query (concurrent)
  ├→ API Calls (concurrent)
  └→ Synthesis Agent
      → Summary Agent
        → Format Agent
          → Output
```

**Features:**
- Concurrent data gathering (3x speedup)
- Background processing para large datasets
- Checkpointing cada etapa
- HITL para review antes de publicar

---

**3. Workflow Automation - Orden de Compra**

**Stages:**
```
Stage 1: Validación (concurrent)
  - Inventory check
  - Credit check
  - Compliance check

Stage 2: Approval (HITL)
  - Manager approval si >$10k
  
Stage 3: Execution (background)
  - Payment processing
  - Shipment creation
  - Invoice generation
  
Stage 4: Notification
  - Email customer
  - Update CRM
  - Log audit trail
```

---

**4. Data Processing Pipeline**

**ETL Workflow:**
```
Extract (MCP connectors)
  → Transform (agents + validation)
    → HITL Review (if anomalies detected)
      → Load (background batch)
        → Checkpoint
```

---

**5. Integration Hub**

**Architecture:**
```
Central Orchestrator
  ├→ Salesforce MCP
  ├→ SAP MCP
  ├→ Database MCP
  ├→ Email MCP
  └→ Analytics MCP
```

**Value:** Unified agent interface para sistemas legacy

---

**💡 DEMO 8: Sistema Complejo - Order Processing (10 min)**

**Objetivo:** Integrar TODOS los conceptos aprendidos

```csharp
using Microsoft.Agents.Workflows;

// ============================================================
// STAGE 1: CONCURRENT VALIDATION AGENTS
// ============================================================

var inventoryAgent = chatClient.CreateAIAgent(
    name: "InventoryChecker",
    instructions: "Check if products are in stock",
    tools: [CheckInventoryTool]
);

var pricingAgent = chatClient.CreateAIAgent(
    name: "PricingCalculator",
    instructions: "Calculate final price with discounts and taxes",
    tools: [CalculatePriceTool]
);

var customerAgent = chatClient.CreateAIAgent(
    name: "CustomerValidator",
    instructions: "Validate customer credit and history",
    tools: [CheckCreditTool]
);

// ============================================================
// STAGE 2: HUMAN APPROVAL (HITL)
// ============================================================

public record ApprovalRequest(
    string OrderId,
    decimal Amount,
    string Customer,
    List<string> Items
) : RequestInfoMessage;

public record ApprovalDecision(
    bool Approved,
    string Reason
);

var approvalExecutor = new RequestInfoExecutor<
    ApprovalRequest,
    ApprovalDecision
>();

// ============================================================
// STAGE 3: BACKGROUND PROCESSING
// ============================================================

var paymentAgent = chatClient.CreateAIAgent(
    name: "PaymentProcessor",
    instructions: "Process payment securely",
    tools: [ProcessPaymentTool]
);

var shippingAgent = chatClient.CreateAIAgent(
    name: "ShippingCoordinator",
    instructions: "Create shipment and generate label",
    tools: [CreateShipmentTool]
);

var notificationAgent = chatClient.CreateAIAgent(
    name: "NotificationSender",
    instructions: "Send confirmation emails",
    tools: [SendEmailTool]
);

// ============================================================
// STAGE 4: CONFIRMATION
// ============================================================

var confirmationAgent = chatClient.CreateAIAgent(
    name: "OrderConfirmation",
    instructions: "Generate final order confirmation"
);

// ============================================================
// WORKFLOW CONSTRUCTION
// ============================================================

// Aggregators
var validationAggregator = new AggregatorExecutor();
var processingAggregator = new AggregatorExecutor();

var workflow = new WorkflowBuilder()
    // Start
    .SetStartExecutor(new OrderInputExecutor())
    
    // STAGE 1: Concurrent validation
    .AddEdge(orderInput, inventoryAgent)
    .AddEdge(orderInput, pricingAgent)
    .AddEdge(orderInput, customerAgent)
    .AddEdge(inventoryAgent, validationAggregator)
    .AddEdge(pricingAgent, validationAggregator)
    .AddEdge(customerAgent, validationAggregator)
    
    // STAGE 2: HITL approval
    .AddEdge(validationAggregator, approvalExecutor)
    
    // STAGE 3: Background processing
    .AddEdge(approvalExecutor, paymentAgent)
    .AddEdge(paymentAgent, shippingAgent)
    .AddEdge(shippingAgent, notificationAgent)
    .AddEdge(notificationAgent, processingAggregator)
    
    // STAGE 4: Confirmation
    .AddEdge(processingAggregator, confirmationAgent)
    
    .Build();

// ============================================================
// EXECUTION WITH ALL FEATURES
// ============================================================

var checkpointManager = new CheckpointManager();
var checkpoints = new List<CheckpointInfo>();

var options = new WorkflowRunOptions
{
    AllowBackgroundResponses = true,
    CheckpointManager = checkpointManager
};

var orderData = new OrderInput
{
    OrderId = "ORD-12345",
    Customer = "Acme Corp",
    Items = new[] { "Widget A", "Gadget B" },
    Amount = 15000m
};

Console.WriteLine("🛒 Starting Order Processing Workflow\n");
Console.WriteLine($"Order ID: {orderData.OrderId}");
Console.WriteLine($"Customer: {orderData.Customer}");
Console.WriteLine($"Amount: ${orderData.Amount:N2}\n");

await foreach (var evt in workflow.RunStreamAsync(orderData, options))
{
    switch (evt)
    {
        case WorkflowStartedEvent:
            Console.WriteLine("▶️  Workflow started");
            break;
            
        case ExecutorCompleteEvent complete:
            Console.WriteLine($"  ✓ {complete.ExecutorName} completed");
            break;
            
        case RequestInfoEvent<ApprovalRequest> approval:
            // ⏸️ HUMAN APPROVAL REQUIRED
            Console.WriteLine("\n⏸️  WORKFLOW PAUSED - Manager Approval Required");
            Console.WriteLine($"Order: {approval.Data.OrderId}");
            Console.WriteLine($"Amount: ${approval.Data.Amount:N2}");
            Console.WriteLine($"Customer: {approval.Data.Customer}");
            Console.WriteLine("Items:");
            foreach (var item in approval.Data.Items)
            {
                Console.WriteLine($"  - {item}");
            }
            
            // Simular decisión de manager
            Console.Write("\n👤 Approve order? (y/n): ");
            var decision = Console.ReadLine()?.ToLower() == "y";
            
            var response = new ApprovalDecision(
                Approved: decision,
                Reason: decision ? "Order approved by manager" : "Exceeds credit limit"
            );
            
            await workflow.SendResponseAsync(approval.RequestId, response);
            Console.WriteLine(decision 
                ? "✅ Order approved - continuing workflow" 
                : "❌ Order rejected - workflow terminated");
            Console.WriteLine("▶️  Workflow resumed\n");
            break;
            
        case SuperStepCompletedEvent checkpoint:
            // 💾 CHECKPOINT SAVED
            checkpoints.Add(new CheckpointInfo(
                checkpoint.SuperStepId,
                checkpoint.Timestamp
            ));
            Console.WriteLine($"  💾 Checkpoint saved: Step {checkpoint.SuperStepId}");
            break;
            
        case WorkflowOutputEvent output:
            Console.WriteLine($"\n✅ WORKFLOW COMPLETED\n");
            Console.WriteLine($"Final confirmation:\n{output.Data}");
            Console.WriteLine($"\nCheckpoints saved: {checkpoints.Count}");
            break;
    }
}

Console.WriteLine("\n📊 Workflow Statistics:");
Console.WriteLine($"  Total checkpoints: {checkpoints.Count}");
Console.WriteLine($"  Execution time: {executionTime}");
Console.WriteLine($"  Agents involved: 9");
Console.WriteLine($"  Patterns used: Concurrent, HITL, Background");
```

**Puntos clave a destacar:**
1. **Concurrent validation** - 3 agents en paralelo (Stage 1)
2. **HITL approval** - Manager review para órdenes >$10k (Stage 2)
3. **Background processing** - Payment, shipping en background (Stage 3)
4. **Checkpointing** - Estado guardado cada stage para recovery
5. **Type safety** - Mensajes tipados entre executors
6. **Observability** - Events completos para UI
7. **Error handling** - Graceful degradation si falla un step
8. **Audit trail** - Todo loggeable para compliance

**Este es un sistema PRODUCTION-READY**

#### 5.2 Best Practices (7 min)

**🎯 Design Principles**

1. **Separar Intelligence de Orchestration**
   ```
   ✅ DO: Workflows coordinan, Agents razonan
   ❌ DON'T: Agents que hacen orchestration
   ```

2. **Single Responsibility**
   ```
   ✅ DO: Un agent, una responsabilidad clara
   ❌ DON'T: Agent que hace todo
   ```

3. **Type Safety**
   ```csharp
   ✅ DO: 
   public record OrderMessage(string Id, decimal Amount);
   
   ❌ DON'T:
   Dictionary<string, object> data
   ```

4. **Explicit Error Handling**
   ```csharp
   ✅ DO:
   try {
       await agent.RunAsync(input);
   }
   catch (AgentException ex) {
       // Handle gracefully
   }
   ```

---

**🔍 Observability**

1. **OpenTelemetry Integration**
   ```csharp
   var tracerProvider = Sdk.CreateTracerProviderBuilder()
       .AddSource("agent-telemetry-source")
       .AddConsoleExporter()
       .Build();
   
   var agent = chatClient.CreateAIAgent(...)
       .AsBuilder()
       .UseOpenTelemetry(sourceName: "agent-telemetry-source")
       .Build();
   ```

2. **Log All Events**
   ```csharp
   await foreach (var evt in workflow.RunStreamAsync(input))
   {
       logger.LogInformation(
           "Event: {EventType}, Data: {Data}", 
           evt.GetType().Name, 
           evt
       );
   }
   ```

3. **Monitor Token Usage**
   ```csharp
   var metrics = new AgentMetrics();
   metrics.TrackTokenUsage(agent, thread);
   ```

4. **Distributed Tracing**
   - Trace agent-to-agent calls
   - Trace tool invocations
   - Correlate logs con traces

---

**🔐 Security**

1. **Input Validation**
   ```csharp
   ✅ DO: Validate antes de pasar a agent
   if (string.IsNullOrEmpty(input) || input.Length > 10000)
       throw new ValidationException();
   ```

2. **PII Filtering Middleware**
   ```csharp
   public class PIIFilterMiddleware : IAgentMiddleware
   {
       public async Task OnInvokeAsync(AgentContext context)
       {
           // Detectar y redactar PII
           context.Input = RedactPII(context.Input);
           await context.NextAsync();
       }
   }
   ```

3. **Rate Limiting**
   ```csharp
   var rateLimiter = new RateLimiter(
       requestsPerMinute: 60,
       burstSize: 10
   );
   
   agent.UseMiddleware(rateLimiter);
   ```

4. **Audit Trail**
   ```csharp
   // Log todas las decisiones y acciones
   logger.LogAudit(
       "Agent {AgentName} executed tool {ToolName} with params {Params}",
       agentName, toolName, parameters
   );
   ```

---

**🧪 Testing**

1. **Unit Tests para Executors**
   ```csharp
   [Fact]
   public async Task Executor_ShouldProcessInput()
   {
       var executor = new MyExecutor();
       var result = await executor.ProcessAsync(testInput);
       Assert.Equal(expectedOutput, result);
   }
   ```

2. **Mock LLM Responses**
   ```csharp
   var mockClient = new MockChatClient();
   mockClient.SetupResponse("Expected response");
   var agent = mockClient.CreateAIAgent(...);
   ```

3. **Integration Tests**
   ```csharp
   [Fact]
   public async Task Workflow_ShouldCompleteSuccessfully()
   {
       var workflow = BuildTestWorkflow();
       var result = await workflow.RunAsync(testInput);
       Assert.NotNull(result);
   }
   ```

4. **Test con Datos Reales**
   - Production-like scenarios
   - Edge cases
   - Failure scenarios

---

**⚡ Performance**

1. **Concurrent cuando sea posible**
   ```
   ✅ DO: Parallel para operaciones independientes
   ❌ DON'T: Sequential para todo
   ```

2. **Cache responses**
   ```csharp
   var cache = new ResponseCache(ttl: TimeSpan.FromMinutes(5));
   agent.UseMiddleware(cache);
   ```

3. **Batch requests**
   ```csharp
   var requests = new[] { req1, req2, req3 };
   var results = await agent.RunBatchAsync(requests);
   ```

4. **Monitor y optimize token usage**
   - Optimizar system prompts
   - Usar modelos adecuados (gpt-4o-mini vs gpt-4)
   - Comprimir contexto cuando sea posible

---

### 6. Conclusión y Recursos (5 min)

#### 6.1 ¿Por qué Agent Framework? (2 min)

**Ventajas Competitivas:**

| Feature | Agent Framework | LangGraph | CrewAI | AutoGen |
|---------|----------------|-----------|---------|---------|
| **Open Source** | ✅ | ✅ | ✅ | ✅ |
| **Multi-Language** | ✅ .NET+Python | Python only | Python only | Python only |
| **Graph Workflows** | ✅ Type-safe | ✅ | ✅ Role-based | Conversation |
| **HITL Built-in** | ✅ | ✅ | Limited | ✅ |
| **Checkpointing** | ✅ | ✅ | ❌ | Limited |
| **MCP Native** | ✅ | Via adapters | ❌ | ❌ |
| **Background Tasks** | ✅ | Limited | ❌ | ❌ |
| **OpenTelemetry** | ✅ | Via LangSmith | Basic | Basic |
| **Enterprise Ready** | ✅ | ✅ | Partial | Partial |
| **Azure Integration** | ✅ Native | Via connectors | ❌ | ❌ |

**Unique Selling Points:**
- 🌐 **Open Standards**: MCP, A2A, OpenAPI first-class
- 🔄 **Multi-lenguaje consistente**: .NET y Python con misma API
- 🏢 **Production-ready desde día 1**: Observability, durability, compliance
- 🔗 **Ecosistema Microsoft**: Azure AI Foundry, M365 Copilot, GitHub Copilot
- 🔬 **Research meets Enterprise**: Lo mejor de AutoGen + Semantic Kernel
- ⚡ **Developer Experience**: Reduce context-switching, stay in flow

#### 6.2 Recursos y Next Steps (3 min)

**📚 Documentación Oficial**
- Repo: [github.com/microsoft/agent-framework](https://github.com/microsoft/agent-framework)
- Docs: [learn.microsoft.com/agent-framework](https://learn.microsoft.com/agent-framework)
- Training: Microsoft Learn modules
- Videos: YouTube Agent Framework channel

**💬 Community**
- Discord: Agent Framework community server
- GitHub Discussions: Q&A y feature requests
- Blog: devblogs.microsoft.com/foundry
- Twitter: #AgentFramework

**🎯 Ejemplos y Samples**
- [.NET Examples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples)
- [Python Examples](https://github.com/microsoft/agent-framework/tree/main/python/packages)
- [Generative AI for Beginners (.NET)](https://github.com/microsoft/Generative-AI-for-beginners-dotnet)
- [Community Samples](https://github.com/topics/agent-framework)

**☁️ Deploy a Producción**
- **Azure AI Foundry**: Hosted agent service
- **Application Insights**: Monitoring y tracing
- **Azure AD**: Authentication y authorization
- **Container Apps**: Hosting escalable
- **Azure Functions**: Serverless agents

**🚀 Primeros Pasos**

```bash
# 1. Install
dotnet add package Microsoft.Agents.AI --prerelease

# 2. Create first agent
var agent = chatClient.CreateAIAgent(
    name: "MyFirstAgent",
    instructions: "You are helpful"
);

# 3. Run
await agent.RunAsync("Hello!");

# 4. Explore samples
git clone https://github.com/microsoft/agent-framework
cd agent-framework/dotnet/samples

# 5. Contribute!
# Fork, create branch, make changes, PR
```

**📊 What to Build Next:**
1. Start simple: basic agent con 1-2 tools
2. Add workflow: sequential pipeline
3. Try concurrent: parallel execution
4. Implement HITL: aprobaciones
5. Add checkpointing: resilience
6. Production deploy: Azure AI Foundry

**💡 Ideas de Proyectos:**
- Customer support bot con routing inteligente
- Research assistant con parallel search
- Code review agent con HITL approval
- Data pipeline con validation
- Document processing con checkpointing
- Integration hub con MCP connectors

---

## Demos Detalladas

### DEMO 1: Primer Agent Simple (3 min)

**Objetivo:** Mostrar lo fácil que es crear un agent básico

**Código completo:**
```csharp
using OpenAI;
using Azure.Identity;

var agent = new OpenAIClient(
    new BearerTokenPolicy(new AzureCliCredential(), 
        "https://ai.azure.com/.default"),
    new OpenAIClientOptions() { 
        Endpoint = new Uri("https://<resource>.openai.azure.com/openai/v1") 
    })
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(
        name: "HaikuBot", 
        instructions: "You are an upbeat assistant that writes beautifully."
    );

Console.WriteLine(await agent.RunAsync("Write a haiku about Microsoft Agent Framework."));
```

**Puntos a destacar:**
- Solo necesitas endpoint, deployment y credentials
- CreateAIAgent con name + instructions
- RunAsync es blocking, retorna string
- Para streaming usar RunStreamAsync

**Ejemplo de output:**
```
Agents arise,
Building the future bright—
.NET's delight.
```

---

### DEMO 2: Agent con Múltiples Proveedores (4 min)

**Ver código en sección 2.1**

**Objetivo:** Mostrar portabilidad entre Azure y OpenAI

**Puntos clave:**
- Mismo código de agent
- Solo cambia el client constructor
- Azure: mejor para enterprise (compliance, regional)
- OpenAI: más simple para prototipos

---

### DEMO 3: Weather Agent con Tools (6 min)

**Ver código completo en sección 2.2**

**Objetivo:** Demostrar function calling automático

**Flow de ejecución:**
1. Usuario: "What's the weather in Madrid?"
2. LLM detecta necesita weather tool
3. Llama GetWeather("Madrid")
4. Función retorna: "Sunny, 22°C"
5. LLM compone: "The weather in Madrid is sunny with..."

**Ejercicio interactivo:**
- Preguntar varios climas
- Probar forecast multi-día
- Ver en logs las tool calls

---

### DEMO 4: Workflow Secuencial (5 min)

**Ver código en sección 3.2**

**Objetivo:** Mostrar pipeline básico A → B → C

**Diagrama visual:**
```
Input: "AI Agents in 2025"
  ↓
ResearchAgent (busca en web)
  ↓
SummaryAgent (condensa info)
  ↓
FormatAgent (markdown output)
  ↓
Output: Reporte formateado
```

---

### DEMO 5: Concurrent Execution (8 min)

**Ver código completo en sección 3.3**

**Objetivo:** Demostrar speedup de ejecución paralela

**Comparación:**
```
Sequential:  [Agent1: 5s] → [Agent2: 5s] → [Agent3: 5s] = 15s total
Concurrent:  [Agent1: 5s]
             [Agent2: 5s]  } En paralelo = 5s total
             [Agent3: 5s]

Speedup: 3x
```

**Mostrar en tiempo real:**
- Timestamp cuando cada agent completa
- Total time al final
- Comparar con versión sequential

---

### DEMO 6: Human-in-the-Loop (6 min)

**Ver código completo en sección 4.1**

**Objetivo:** Mostrar pause/resume con input humano

**Flow visual:**
```
1. SearchAgent → Lista de candidatos
2. ⏸️ PAUSE → Mostrar al usuario
3. 👤 Usuario selecciona candidato #2
4. ▶️ RESUME → DetailAgent analiza candidato
5. Output → Análisis detallado
```

**Interactivo:**
- UI muestra opciones reales
- Usuario hace selección
- Workflow continúa seamlessly

---

### DEMO 7: Background Responses (4 min)

**Ver código en sección 4.2**

**Objetivo:** Pause/resume con continuation tokens

**Scenario:**
1. Start: "Write a novel" (tarea larga)
2. Después de 10 chunks → PAUSE
3. Quick question: "What's 2+2?" → Respuesta inmediata
4. RESUME novel desde token → Continúa exacto donde quedó

**Beneficio:** Network resilience sin perder progreso

---

### DEMO 8: Order Processing Completo (10 min)

**Ver código completo en sección 5.1**

**Objetivo:** Integrar TODAS las capabilities

**Stages:**
1. ⚡ Concurrent validation (Inventory + Pricing + Customer)
2. ⏸️ HITL approval (Manager decision si >$10k)
3. 🔄 Background processing (Payment + Shipping + Notification)
4. 💾 Checkpointing (Estado guardado cada stage)
5. ✅ Confirmation (Output final)

**Métricas a mostrar:**
- Execution time por stage
- Checkpoints guardados
- Events emitidos
- Token usage

**Esta es la demo ESTRELLA - mostrar sistema production-ready completo**

---

## Código de Referencia

### Setup del Proyecto

```bash
# Crear proyecto
dotnet new console -n AgentFrameworkDemo
cd AgentFrameworkDemo

# Instalar packages
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Azure.Identity
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console

# Variables de entorno
export AZURE_OPENAI_ENDPOINT="https://<resource>.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
export AZURE_OPENAI_API_VERSION="2024-08-01-preview"

# Opcional: usar API key en vez de Azure CLI credential
export AZURE_OPENAI_API_KEY="<your-key>"
```

### Helpers y Utilities

```csharp
// ChatClientProvider.cs
using OpenAI;
using Azure.Identity;

public static class ChatClientProvider
{
    public static OpenAIClient GetChatClient()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        
        return new OpenAIClient(
            new BearerTokenPolicy(new AzureCliCredential(), 
                "https://ai.azure.com/.default"),
            new OpenAIClientOptions() { 
                Endpoint = new Uri(endpoint) 
            }
        );
    }
}

// LoggingMiddleware.cs
using Microsoft.Agents.AI;

public class LoggingMiddleware : IAgentMiddleware
{
    public async Task OnInvokeAsync(AgentContext context)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Agent: {context.Agent.Name}");
        Console.WriteLine($"Input: {context.Input}");
        
        await context.NextAsync();
        
        Console.WriteLine($"Output: {context.Output}");
    }
}

// RateLimiter.cs
public class RateLimiter : IAgentMiddleware
{
    private readonly SemaphoreSlim _semaphore;
    
    public RateLimiter(int requestsPerMinute)
    {
        _semaphore = new SemaphoreSlim(requestsPerMinute, requestsPerMinute);
    }
    
    public async Task OnInvokeAsync(AgentContext context)
    {
        await _semaphore.WaitAsync();
        try
        {
            await context.NextAsync();
        }
        finally
        {
            _ = Task.Delay(TimeSpan.FromMinutes(1))
                .ContinueWith(_ => _semaphore.Release());
        }
    }
}
```

---

## Recursos Adicionales

### Repositorios de Referencia

1. **Tu POC actual**: https://github.com/elbrinner/demo-agent-framework
   - Excelente base para partir
   - Agregar demos de esta guía

2. **Oficial Microsoft**: https://github.com/microsoft/agent-framework
   - Referencia completa
   - Samples oficiales

3. **Generative AI for Beginners**: https://github.com/microsoft/Generative-AI-for-beginners-dotnet
   - Tutoriales paso a paso
   - Incluye Agent Framework

### Links Útiles

- [Microsoft Learn - Agent Framework](https://learn.microsoft.com/agent-framework)
- [Azure AI Foundry](https://ai.azure.com)
- [MCP Specification](https://modelcontextprotocol.io)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)

### Papers y Research

- AutoGen: Enabling Next-Gen LLM Applications
- Multi-Agent Orchestration Patterns
- Human-in-the-Loop AI Systems

---

## Notas para el Presentador

### Tips de Presentación

1. **Empezar con impacto**: Demo 1 en primeros 5 minutos
2. **Intercalar teoría y práctica**: Teoría → Demo → Teoría
3. **Historias reales**: Mencionar KPMG, casos de uso enterprise
4. **Interactividad**: Preguntar a la audiencia sus casos de uso
5. **Tiempo buffer**: Dejar 5 min extra por si demos tardan

### Troubleshooting Común

**Error: Authentication failed**
```bash
# Solución: Login con Azure CLI
az login
az account set --subscription <subscription-id>
```

**Error: Model deployment not found**
```bash
# Verificar deployment name
az cognitiveservices account deployment list \
  --name <resource-name> \
  --resource-group <rg-name>
```

**Error: Rate limit exceeded**
- Usar RateLimiter middleware
- Considerar tier de Azure OpenAI

### Slide Deck Recommendations

- **Slide 1-10**: Fundamentos (menos código, más conceptos)
- **Slide 11-20**: Agents (50/50 código y conceptos)
- **Slide 21-30**: Workflows (más diagramas)
- **Slide 31-37**: Best practices (bullets y tablas)

**Formato visual:**
- Syntax highlighting para código
- Diagramas de flujo para workflows
- Tablas comparativas para features
- Screenshots de resultados

---

## Checklist Pre-Presentación

### 1 Semana Antes
- [ ] Revisar todo el contenido
- [ ] Preparar slides
- [ ] Crear repositorio con demos
- [ ] Testear todas las demos
- [ ] Preparar datos de ejemplo
- [ ] Crear Azure resources necesarios

### 1 Día Antes
- [ ] Re-test todas las demos
- [ ] Verificar credenciales Azure
- [ ] Preparar backup de demos (videos)
- [ ] Revisar timing de cada sección
- [ ] Preparar Q&A comunes

### Día de la Presentación
- [ ] Llegar temprano para setup
- [ ] Verificar proyector y audio
- [ ] Test internet connection
- [ ] Tener backup plan (hotspot)
- [ ] Agua y energía ☕

---

**¡Éxito en tu presentación! 🚀**

Esta guía te proporciona todo lo necesario para una charla completa, amena y técnicamente sólida sobre Microsoft Agent Framework. Adapta según tu audiencia y estilo personal.
