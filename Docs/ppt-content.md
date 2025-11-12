# Microsoft Agent Framework - Contenido para PowerPoint

## Guía de Slides - 37 Diapositivas Totales

---

## SLIDE 1 - Portada
**Tipo:** Title Slide

### Contenido:
**Título Principal:**
# Microsoft Agent Framework
**Subtítulo:**
De Cero a Héroe en 2 Horas
Construyendo AI Agents Production-Ready en .NET

**Footer:**
[Tu Nombre] | [Tu Empresa] | 2025

**Diseño Visual:**
- Background: Gradiente azul Microsoft
- Logo de .NET y Microsoft Agent Framework
- Iconos: 🤖 🔧 ⚡

---

## SLIDE 2 - Agenda
**Tipo:** Agenda/Outline

### Contenido:
# Agenda - 2 Horas de Contenido

1️⃣ **Introducción y Fundamentos** (20 min)
   - ¿Qué son los AI Agents?
   - Historia y evolución
   - Arquitectura del framework

2️⃣ **Agents: El Cerebro del Sistema** (25 min)
   - Creación de agents básicos
   - Tools y function calling
   - Model Context Protocol (MCP)

3️⃣ **Workflows: Orquestación Inteligente** (30 min)
   - Agent vs Workflow
   - Componentes y construcción
   - Patrones de orquestación

4️⃣ **Capacidades Avanzadas** (25 min)
   - Human-in-the-Loop (HITL)
   - Background responses
   - Checkpointing

5️⃣ **Casos de Uso y Best Practices** (15 min)
   - Casos empresariales reales
   - Mejores prácticas

6️⃣ **Conclusión y Recursos** (5 min)

**Nota destacada:**
🎯 8 Demos en Vivo distribuidas durante la sesión

---

## SLIDE 3 - Sección 1 Divider
**Tipo:** Section Divider

### Contenido:
# 1
# Introducción y Fundamentos

**Subtítulo:**
Entendiendo los AI Agents desde cero

**Visual:**
- Icon grande: 🧠
- Background: Color diferenciado (azul claro)

---

## SLIDE 4 - ¿Qué es un AI Agent?
**Tipo:** Content

### Contenido:
# ¿Qué es un AI Agent?

**Definición:**
> Un sistema autónomo que usa LLMs para percibir, razonar y actuar hacia objetivos específicos

**Comparación:**

| | Chatbot Tradicional | AI Agent |
|---|---|---|
| Respuestas | ❌ Predefinidas | ✅ Generadas dinámicamente |
| Contexto | ❌ Sin memoria | ✅ Mantiene conversación |
| Herramientas | ❌ Limitado | ✅ Usa APIs, databases, etc. |
| Autonomía | ❌ Script fijo | ✅ Toma decisiones |

**Componentes Clave:**
- 🧠 **Perception**: Entiende el input
- 🤔 **Reasoning**: Decide qué hacer (LLM)
- 🔧 **Action**: Ejecuta usando tools
- 💾 **Memory**: Mantiene contexto (Thread)

---

## SLIDE 5 - El Ciclo de Vida
**Tipo:** Diagram

### Contenido:
# El Ciclo de Vida de un Agent

**Diagrama Circular:**

```
    ┌──────────┐
    │  Input   │
    └─────┬────┘
          │
          ▼
    ┌──────────┐
    │  Thread  │ ◄── Estado y memoria
    └─────┬────┘
          │
          ▼
    ┌──────────┐
    │  Agent   │ ◄── Reasoning (LLM)
    └─────┬────┘
          │
          ▼
    ┌──────────┐
    │   Tool   │ ◄── Ejecución
    └─────┬────┘
          │
          ▼
    ┌──────────┐
    │Middleware│ ◄── Interceptores
    └─────┬────┘
          │
          ▼
    ┌──────────┐
    │ Response │
    └──────────┘
```

**Nota:**
Este ciclo se repite hasta completar la tarea

---

## SLIDE 6 - Historia
**Tipo:** Timeline

### Contenido:
# Historia: De Research a Production

**Timeline Visual:**

```
2023 ──────┬──────────────────────────────────────────────
           │
           ├─ Semantic Kernel (Microsoft)
           │  • Enterprise-ready framework
           │  • Estabilidad y features de producción
           │
           ├─ AutoGen (MS Research)
           │  • Multi-agent research
           │  • Patrones avanzados de orquestación
           │
2024 ──────┤
           │
           ├─ Convergencia
           │  • Unificación de ambos proyectos
           │  • Best of both worlds
           │
Oct 2025 ──┤
           │
           └─ Microsoft Agent Framework
              • Public Preview
              • Production-ready
              • Open Source
```

**¿Por qué la unificación?**
✅ Semantic Kernel aportó: Estabilidad, enterprise features
✅ AutoGen aportó: Multi-agent patterns, innovation
✅ Agent Framework: Framework unificado potente

---

## SLIDE 7 - Arquitectura
**Tipo:** Architecture Diagram

### Contenido:
# Arquitectura del Framework

**Principio Fundamental:**
> "Separar Intelligence de Orchestration"

**Capas:**

```
┌─────────────────────────────────────────┐
│  Observability Layer                    │
│  📊 OpenTelemetry, Logging, Metrics     │
└─────────────────────────────────────────┘
              ▲
              │
┌─────────────────────────────────────────┐
│  Orchestration Layer                    │
│  🔄 Workflows, Patterns, Control Flow   │
└─────────────────────────────────────────┘
              ▲
              │
┌─────────────────────────────────────────┐
│  Intelligence Layer                     │
│  🧠 Agents, LLMs, Reasoning             │
└─────────────────────────────────────────┘
              ▲
              │
┌─────────────────────────────────────────┐
│  Integration Layer                      │
│  🔧 Tools, MCP, APIs, Functions         │
└─────────────────────────────────────────┘
```

**Lifecycle Completo:**
Input → Thread → Agent → Tool → Middleware → Workflow → Events → Output

---

## SLIDE 8 - DEMO 1
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 1: Tu Primer Agent
**Duración: 3 minutos**

**Objetivo:**
Crear un HaikuBot en 10 líneas de código

**Código:**
```csharp
var agent = new AzureOpenAIClient(...)
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(
        name: "HaikuBot",
        instructions: "You write beautiful haikus"
    );

Console.WriteLine(
    await agent.RunAsync("Write about .NET")
);
```

**Resultado esperado:**
```
Agents arise,
Building the future bright—
.NET's delight.
```

---

## SLIDE 9 - Sección 2 Divider
**Tipo:** Section Divider

### Contenido:
# 2
# Agents: El Cerebro del Sistema

**Subtítulo:**
Construcción y configuración de agents inteligentes

**Visual:**
- Icon: 🤖
- Background: Color diferenciado

---

## SLIDE 10 - Tipos de Agents
**Tipo:** Content

### Contenido:
# Tipos de Agents

**Tabla comparativa:**

| Tipo | Provider | Cuándo Usar |
|------|----------|-------------|
| **ChatCompletionAgent** | Genérico | Máxima flexibilidad |
| **OpenAI Responses** | OpenAI | Optimizado para OpenAI |
| **Azure OpenAI Responses** | Azure | Integración Azure |
| **AzureAIAgent** | Azure AI Foundry | Managed service |
| **CopilotStudioAgent** | M365 Copilot | Enterprise integration |

**Key Point:**
> Todos se usan igual, solo difieren en provider y capacidades específicas

**Código básico:**
```csharp
var agent = chatClient.CreateAIAgent(
    name: "MyBot",
    instructions: "System prompt here",
    tools: [tool1, tool2]
);
```

---

## SLIDE 11 - Agent Configuration
**Tipo:** Content

### Contenido:
# Configuración de un Agent

**Propiedades Clave:**

**1. Name**
```csharp
name: "CustomerServiceBot"
```
Identificador único del agent

**2. Instructions**
```csharp
instructions: "You are a helpful assistant..."
```
System prompt que define comportamiento

**3. Description**
```csharp
description: "Handles billing queries"
```
Para workflows y orchestration

**4. Tools**
```csharp
tools: [SearchTool, DatabaseTool]
```
Funciones que puede llamar

**5. Middleware**
```csharp
middleware: [LoggingMiddleware]
```
Interceptores de comportamiento

---

## SLIDE 12 - Tools y Function Calling
**Tipo:** Content

### Contenido:
# Tools: Extendiendo Capacidades

**¿Qué son los Tools?**
> Funciones que el agent puede llamar automáticamente cuando las necesita

**Flujo:**
```
1. Usuario: "¿Qué clima hay en Madrid?"
2. LLM detecta necesidad de info
3. Llama get_weather("Madrid")
4. Recibe: "Soleado, 22°C"
5. Compone: "En Madrid hace sol..."
```

**Definir un Tool:**
```csharp
public static string GetWeather(
    [Description("City name")] string location)
{
    // API call
    return $"Weather in {location}: Sunny, 22°C";
}

var agent = chatClient.CreateAIAgent(
    tools: [AIFunctionFactory.Create(GetWeather)]
);
```

**El LLM decide automáticamente:**
- ¿Qué tool usar?
- ¿Con qué parámetros?
- ¿Cuándo llamarlo?

---

## SLIDE 13 - DEMO 2
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 2: Multi-Provider Agent
**Duración: 4 minutos**

**Objetivo:**
Mostrar portabilidad entre Azure OpenAI y OpenAI

**Azure OpenAI:**
```csharp
var azureAgent = new AzureOpenAIClient(...)
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(...);
```

**OpenAI directo:**
```csharp
var openaiAgent = new OpenAIClient("<api-key>")
    .GetOpenAIResponseClient("gpt-4o-mini")
    .CreateAIAgent(...);
```

**Key Point:**
✅ Mismo código de agent
✅ Solo cambia el constructor del client
✅ Azure: mejor para enterprise
✅ OpenAI: más simple para prototipos

---

## SLIDE 14 - DEMO 3
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 3: Weather Agent
**Duración: 6 minutos**

**Objetivo:**
Function calling automático en acción

**Tools definidos:**
- `GetWeather(location)` - Clima actual
- `GetForecast(location, days)` - Pronóstico

**Tests:**
```
👤 User: "What's the weather in Madrid?"
🤖 Agent: [Llama GetWeather("Madrid")]
🤖 Agent: "In Madrid it's sunny, 22°C"

👤 User: "5-day forecast for Barcelona"
🤖 Agent: [Llama GetForecast("Barcelona", 5)]
🤖 Agent: "Here's the 5-day forecast..."
```

**Magia:**
El LLM extrae parámetros del texto natural

---

## SLIDE 15 - Model Context Protocol
**Tipo:** Content

### Contenido:
# Model Context Protocol (MCP)

**¿Qué es MCP?**
> Estándar abierto para que models y tools se comuniquen

**Ventajas:**
- ✅ Interoperabilidad entre frameworks
- ✅ Ecosistema de tools compartido
- ✅ No vendor lock-in
- ✅ Seguridad centralizada

**3 Tipos de Conexión:**

**1. MCPStdioTool** - Proceso local
```csharp
Command: "npx"
Args: ["@modelcontextprotocol/server-github"]
```

**2. MCPStreamableHTTPTool** - HTTP/SSE
```csharp
URL: "https://api.example.com/mcp"
```

**3. Hosted MCP** - Azure AI Foundry
- Aprobación previa
- Authentication managed

**Ejemplos:**
GitHub MCP | Filesystem MCP | AWS Docs MCP | Slack MCP

---

## SLIDE 16 - Sección 3 Divider
**Tipo:** Section Divider

### Contenido:
# 3
# Workflows: Orquestación Inteligente

**Subtítulo:**
Coordinando múltiples agents en flujos complejos

**Visual:**
- Icon: 🔄
- Background: Color diferenciado

---

## SLIDE 17 - Agent vs Workflow
**Tipo:** Comparison

### Contenido:
# Agent vs Workflow

**Tabla comparativa:**

|  | **Agent** | **Workflow** |
|---|---|---|
| **Control** | LLM decide pasos | Desarrollador define flujo |
| **Naturaleza** | Dinámico, flexible | Predefinido, estructurado |
| **Uso** | Razonamiento adaptativo | Procesos de negocio |
| **Ejemplo** | Chatbot de soporte | Pipeline de aprobación |

**Visual Diagrams:**

**Agent:**
```
Input → [🤖 LLM decide] → Tools? → Output
          ↑______________|
```

**Workflow:**
```
Input → [A] → [B] → [C] → Output
        ↓     ↓     ↓
     Agent  Func  Agent
```

**Key Insight:**
> Workflows CONTIENEN agents como componentes

---

## SLIDE 18 - Componentes de Workflows
**Tipo:** Architecture

### Contenido:
# Componentes de Workflows

**4 Componentes Clave:**

**1. Executors** 🎯
Nodos de procesamiento
- Agents
- Funciones custom
- Lógica de negocio

**2. Edges** ➡️
Flujo de datos
- Conectan executors
- Type-safe
- Condicionales

**3. WorkflowBuilder** 🏗️
Constructor del grafo
```csharp
new WorkflowBuilder()
    .SetStartExecutor(first)
    .AddEdge(first, second)
    .Build()
```

**4. Events** 📡
Observabilidad
- WorkflowStartedEvent
- ExecutorCompleteEvent
- WorkflowOutputEvent

**Supersteps:**
Ejecución por fases (estilo Pregel)
→ Determinismo + Checkpointing

---

## SLIDE 19 - Patrones de Orquestación
**Tipo:** Patterns

### Contenido:
# Patrones de Orquestación

**1. Sequential** - Flujo lineal
```
A → B → C → Output
```
Uso: Pipelines con dependencias

**2. Concurrent** - Paralelo
```
     ┌→ A ┐
In →┤→ B ├→ Agg → Out
     └→ C ┘
```
Uso: Speedup 3x para independientes

**3. Handoff** - Delegación
```
A → (decide) → B o C
```
Uso: Escalamiento a especialistas

**4. Magentic** - Manager
```
Manager
├→ Specialist1
├→ Specialist2
└→ Specialist3
```
Uso: Proyectos complejos

**5. Hierarchical** - Multi-nivel
```
Top
├→ Dept1 → Workers
└→ Dept2 → Workers
```
Uso: Orgs grandes

---

## SLIDE 20 - DEMO 4
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 4: Workflow Secuencial
**Duración: 5 minutos**

**Objetivo:**
Pipeline básico A → B → C

**Flow:**
```
Input: "AI Agents in 2025"
  ↓
ResearchAgent (busca web)
  ↓
SummaryAgent (condensa)
  ↓
FormatAgent (markdown)
  ↓
Output: Reporte formateado
```

**Código:**
```csharp
var workflow = new WorkflowBuilder()
    .SetStartExecutor(researchAgent)
    .AddEdge(researchAgent, summaryAgent)
    .AddEdge(summaryAgent, formatAgent)
    .Build();

await workflow.RunStreamAsync(input);
```

**Observability:**
Events muestran progreso de cada executor

---

## SLIDE 21 - DEMO 5
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 5: Concurrent Execution
**Duración: 8 minutos**

**Objetivo:**
Demostrar speedup 3x con ejecución paralela

**Comparación:**
```
Sequential:
[A:5s] → [B:5s] → [C:5s] = 15s

Concurrent:
[A:5s]
[B:5s] } En paralelo = 5s
[C:5s]

Speedup: 3x ⚡
```

**Arquitectura:**
```
           ┌─→ WebSearch ─┐
Input → R ─┼─→ Database  ─┼→ Agg → Out
           └─→ API      ─┘
```

**Resultado:**
Investigación completa en 1/3 del tiempo

---

## SLIDE 22 - Sección 4 Divider
**Tipo:** Section Divider

### Contenido:
# 4
# Capacidades Avanzadas

**Subtítulo:**
Features enterprise para producción

**Visual:**
- Icon: ⚡
- Background: Color diferenciado

---

## SLIDE 23 - Human-in-the-Loop
**Tipo:** Content

### Contenido:
# Human-in-the-Loop (HITL)

**¿Por qué HITL?**
- ✅ Decisiones críticas requieren supervisión
- ✅ Aprobaciones en workflows sensibles
- ✅ Validación de outputs del LLM
- ✅ Feedback loop para mejora
- ✅ Compliance y auditoría

**Componentes:**

**RequestInfoMessage**
Estructura tipada del request

**RequestInfoExecutor**
Coordinador que pausa workflow

**RequestInfoEvent**
Evento cuando workflow espera

**SendResponseAsync**
Envío de respuesta humana

**Flujo:**
```
1. Executor → RequestInfoEvent
2. Workflow ⏸️ PAUSA
3. UI muestra a usuario
4. Usuario decide
5. SendResponseAsync
6. Workflow ▶️ RESUME
```

---

## SLIDE 24 - DEMO 6
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 6: HITL Workflow
**Duración: 6 minutos**

**Escenario:**
Búsqueda de candidatos con selección humana

**Flow:**
```
1. SearchAgent
   → Lista de candidatos
   
2. ⏸️ RequestInfoExecutor
   → Workflow PAUSA
   → Muestra opciones
   
3. 👤 Usuario selecciona #2
   
4. ▶️ SendResponse
   → Workflow RESUME
   
5. DetailAgent
   → Análisis del candidato
```

**Key Point:**
Estado se preserva completamente durante pausa

---

## SLIDE 25 - Background Responses
**Tipo:** Content

### Contenido:
# Background Responses

**El Problema:**
- 🔴 Operaciones largas (reportes de 50 páginas)
- 🔴 Network timeouts
- 🔴 Usuario cierra laptop
- 🔴 Necesita hacer otra consulta rápida

**La Solución:**
> Continuation tokens = bookmarks del estado

**Cómo funciona:**

1. **Enable**
```csharp
AllowBackgroundResponses = true
```

2. **Capture token**
```csharp
update.ContinuationToken // Guardar
```

3. **Resume**
```csharp
options.ContinuationToken = savedToken
```

**Casos de Uso:**
- ✅ Generación de código (30+ min)
- ✅ Research reports
- ✅ Network resilience
- ✅ Workflows interactivos
- ✅ Mobile apps

---

## SLIDE 26 - DEMO 7
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 7: Background Responses
**Duración: 4 minutos**

**Escenario:**
Tarea larga con pause/resume

**Timeline:**
```
1. Start: "Write novel" 🚀
   ↓ (10 chunks)
2. ⏸️ PAUSE (simular network issue)
3. Quick: "What's 2+2?" → "4" ⚡
4. ▶️ RESUME from token
5. Continúa desde chunk 11
6. ✅ Complete
```

**Sin continuation tokens:**
❌ Perder progreso, empezar de cero

**Con continuation tokens:**
✅ Resume exacto, cero pérdida

**Beneficio:**
Network resilience + Costo optimizado

---

## SLIDE 27 - Checkpointing
**Tipo:** Content

### Contenido:
# Checkpointing

**¿Qué es?**
> Guardar estado COMPLETO del workflow para recuperación

**Cuándo se crean:**
- Al final de cada superstep
- Después de operaciones críticas
- Antes/después de HITL
- Puntos definidos por dev

**Qué se guarda:**
- ✅ Estado de todos los executors
- ✅ Mensajes pendientes
- ✅ Shared state
- ✅ Pending requests/responses
- ✅ Metadata

**Casos de Uso:**
- 🔧 **Failure recovery**: Resume desde checkpoint
- 🕐 **Time-travel**: Replay desde punto X
- 🏢 **Auditing**: Ver estado en momento T
- 🌍 **Migration**: Mover entre entornos
- 📊 **Long-running**: Progreso incremental

---

## SLIDE 28 - Sección 5 Divider
**Tipo:** Section Divider

### Contenido:
# 5
# Casos de Uso y Best Practices

**Subtítulo:**
Llevando agents a producción

**Visual:**
- Icon: 🏢
- Background: Color diferenciado

---

## SLIDE 29 - Casos de Uso Empresariales
**Tipo:** Use Cases

### Contenido:
# Casos de Uso Empresariales

**1. Customer Service** 🎧
Multi-agent con routing inteligente
- Router → Billing | Tech | Account | Escalation
- HITL para casos complejos
- CRM integration vía MCP

**2. Research & Reporting** 📊
Investigación paralela + síntesis
- Concurrent: Web + DB + APIs
- Background processing
- Checkpointing por etapa

**3. Workflow Automation** ✅
Aprobaciones multi-nivel
- Validación concurrente
- HITL para aprovals >$10k
- Audit trail completo

**4. Data Processing** 📁
ETL con validación humana
- MCP connectors
- Transform agents
- HITL si anomalías

**5. Integration Hub** 🔗
Unified interface para legacy systems
- Salesforce + SAP + Database
- MCP para cada sistema
- Orchestrator central

---

## SLIDE 30 - DEMO 8
**Tipo:** Demo Slide

### Contenido:
# 🚀 DEMO 8: Order Processing
**Duración: 10 minutos**

**Sistema End-to-End Production-Ready**

**Stage 1: Concurrent Validation** ⚡
Inventory + Pricing + Customer en paralelo

**Stage 2: HITL Approval** ⏸️
Manager decision si >$10k

**Stage 3: Background Processing** 🔄
Payment + Shipping + Notification

**Stage 4: Checkpoint** 💾
Estado guardado post-pago

**Stage 5: Confirmation** ✅
Output final

**Features integradas:**
✅ Concurrent orchestration
✅ Human-in-the-Loop
✅ Background responses
✅ Checkpointing
✅ Type-safe messages
✅ Observability completa

**Esta es la DEMO ESTRELLA** 🌟

---

## SLIDE 31 - Best Practices
**Tipo:** Best Practices

### Contenido:
# Best Practices

**🎯 Design**
- ✅ Separar intelligence de orchestration
- ✅ Un agent, una responsabilidad
- ✅ Type-safe messages
- ✅ Explicit error handling

**🔍 Observability**
- ✅ OpenTelemetry integration
- ✅ Log all events
- ✅ Trace agent decisions
- ✅ Monitor token usage

**🔐 Security**
- ✅ Validate todos los inputs
- ✅ PII filtering middleware
- ✅ Rate limiting por agent
- ✅ Audit trail completo

**🧪 Testing**
- ✅ Unit tests para executors
- ✅ Integration tests para workflows
- ✅ Mock LLM responses
- ✅ Test con datos reales

**⚡ Performance**
- ✅ Concurrent cuando posible
- ✅ Cache responses
- ✅ Batch requests
- ✅ Optimize token usage

---

## SLIDE 32 - Sección 6 Divider
**Tipo:** Section Divider

### Contenido:
# 6
# Conclusión y Recursos

**Subtítulo:**
Tu viaje apenas comienza

**Visual:**
- Icon: 🚀
- Background: Color diferenciado

---

## SLIDE 33 - Comparación de Frameworks
**Tipo:** Comparison Table

### Contenido:
# ¿Por qué Agent Framework?

| Feature | Agent Framework | LangGraph | CrewAI |
|---------|----------------|-----------|---------|
| Open Source | ✅ | ✅ | ✅ |
| Multi-Language | ✅ .NET+Python | Python | Python |
| Graph Workflows | ✅ Type-safe | ✅ | ✅ Role |
| HITL Built-in | ✅ | ✅ | Limited |
| Checkpointing | ✅ | ✅ | ❌ |
| MCP Native | ✅ | Adapters | ❌ |
| Background Tasks | ✅ | Limited | ❌ |
| OpenTelemetry | ✅ | LangSmith | Basic |
| Enterprise Ready | ✅ | ✅ | Partial |
| Azure Integration | ✅ Native | Via conn | ❌ |

**Learning Curve:**
Agent Framework: Media | LangGraph: Alta | CrewAI: Baja

---

## SLIDE 34 - Ventajas Clave
**Tipo:** Advantages

### Contenido:
# Ventajas Clave de Agent Framework

**🌐 Open Standards**
MCP, A2A, OpenAPI como ciudadanos de primera clase

**🔄 Multi-lenguaje Consistente**
.NET y Python con misma API y patrones

**🏢 Production-Ready desde Día 1**
Observability, durability, compliance built-in

**🔗 Ecosistema Microsoft**
Integración nativa con Azure AI Foundry, M365 Copilot

**🔬 Research meets Enterprise**
Lo mejor de AutoGen + Semantic Kernel

**⚡ Developer Experience**
Reduce context-switching, stay in flow

**💰 Cost Optimized**
Background responses, checkpointing = menos re-runs

**🛡️ Security First**
Middleware, validation, audit trails integrados

---

## SLIDE 35 - Recursos
**Tipo:** Resources

### Contenido:
# Recursos y Next Steps

**📚 Documentación Oficial**
- 📦 Repo: github.com/microsoft/agent-framework
- 📚 Docs: learn.microsoft.com/agent-framework
- 🎓 Training: Microsoft Learn modules
- 🎥 Videos: YouTube Agent Framework

**💬 Community**
- 💬 Discord: Agent Framework community
- 🐛 Issues: GitHub discussions
- 📝 Blog: devblogs.microsoft.com
- 🐦 Twitter: #AgentFramework

**🎯 Samples**
- .NET: github.com/.../dotnet/samples
- Python: github.com/.../python/packages
- Community: github.com/topics/agent-framework

**☁️ Deploy a Producción**
- Azure AI Foundry: Hosted agents
- Application Insights: Monitoring
- Azure AD: Auth
- Container Apps: Hosting

---

## SLIDE 36 - Call to Action
**Tipo:** CTA

### Contenido:
# ¡Empieza Hoy!

**5 Pasos para Comenzar:**

**1️⃣ Install**
```bash
dotnet add package Microsoft.Agents.AI --prerelease
```

**2️⃣ Create**
```csharp
var agent = chatClient.CreateAIAgent(...)
```

**3️⃣ Experiment**
Tools → Workflows → Patterns

**4️⃣ Contribute**
Fork → Branch → PR al proyecto open source

**5️⃣ Share**
Tus casos de uso con la community

**💡 Ideas de Proyectos:**
- Customer support bot
- Research assistant
- Code review agent
- Data pipeline
- Document processor
- Integration hub

**Quote:**
> "The best time to start building agents was yesterday.
> The second best time is now."

---

## SLIDE 37 - Thank You
**Tipo:** Thank You

### Contenido:
# ¡Gracias!

**¿Preguntas?**

**Contacto:**
📧 [tu-email@ejemplo.com]
🐦 [@tu_twitter]
🔗 linkedin.com/in/tu-perfil
💻 github.com/tu-usuario

**Links útiles:**
🌐 aka.ms/agent-framework
📚 aka.ms/agent-framework/docs
🎯 aka.ms/agent-framework/samples

**QR Code:**
[Generar QR a tu repo de demos]

---

## Notas de Diseño Visual

### Paleta de Colores
- **Primary**: #0078D4 (Azure Blue)
- **Secondary**: #50E6FF (Light Blue)
- **Accent**: #FFB900 (Yellow)
- **Dark**: #1E1E1E
- **Light**: #F3F2F1

### Tipografía
- **Títulos**: Segoe UI Bold, 48pt
- **Subtítulos**: Segoe UI Semibold, 32pt
- **Body**: Segoe UI Regular, 20pt
- **Code**: Consolas, 16pt

### Iconografía
- 🤖 AI Agent
- 🔧 Tools
- 🔄 Workflows
- ⚡ Performance
- 🏢 Enterprise
- 💾 Storage
- 🔐 Security
- 📊 Analytics

### Layouts Recomendados
- **Content slides**: 60% texto, 40% visual
- **Demo slides**: Código syntax highlighted
- **Diagram slides**: Flowcharts con mermaid style
- **Comparison**: Tablas con iconos

### Animaciones
- **Section dividers**: Fade in + slide from right
- **Bullets**: Appear one by one
- **Diagrams**: Build piece by piece
- **Code**: Type writer effect (opcional)

### Templates PowerPoint Recomendados
- Microsoft Azure Template
- .NET Modern Template
- Tech Presentation Template

---

## Tips para Exportar a PowerPoint

1. **Usar Master Slides** para consistencia
2. **Code blocks**: Usar formato de código con syntax highlighting
3. **Diagramas**: Usar SmartArt o draw.io
4. **Icons**: Usar Fluent Icons de Microsoft
5. **Transiciones**: Simples y profesionales (Fade, Push)
6. **Speaker Notes**: Agregar notas en cada slide
7. **Timing**: Marcar duración estimada por slide

---

## Checklist de Contenido

### Cada Slide debe tener:
- [ ] Número de slide
- [ ] Título claro
- [ ] Contenido balanceado (no sobrecargado)
- [ ] Visuals o diagramas
- [ ] Footer con info del presentador
- [ ] Speaker notes (opcional)

### Sección de Demos debe incluir:
- [ ] Objetivo claro
- [ ] Duración estimada
- [ ] Código legible
- [ ] Resultado esperado
- [ ] Puntos clave a destacar

### Overall Presentation:
- [ ] Flujo lógico entre slides
- [ ] Transiciones suaves entre secciones
- [ ] Balance teoría/práctica
- [ ] Calls-to-action claros
- [ ] Recursos al final

---

**¡Slides listos para crear tu PowerPoint! 🎯**
