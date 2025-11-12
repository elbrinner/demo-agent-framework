# Plan de Backend para Validar Microsoft Agent Framework

Este documento planifica un backend (en .NET) capaz de validar de forma sistemática las capacidades de Microsoft Agent Framework en demos y endpoints. El objetivo inmediato (M0) es catalogar TODAS las características posibles, mapearlas con lo que ya existe en esta repo y definir los siguientes pasos para cubrir lo que falta.

---

## 0) Identidad del proyecto (Bri-Agent)

Para distinguir claramente esta demo como propia, todo el trabajo (backend y frontend) se identifica como "Bri-Agent".

- Nombre del proyecto: Bri-Agent
- Prefijo de API: `/bri-agent` (todas las rutas públicas cuelgan de este prefijo)
- Identificador de demos: usar convenciones `bri-*` cuando tenga sentido (ej. `bri-mcp-client-basic`)
- Frontend: nombre de app sugerido `bri-agent-frontend`

### 0.1) Nueva estructura monorepo (Backend + Frontend)

Adoptamos una estructura dedicada bajo `Bri-Agent/` que separa claramente Backend y Frontend:

```
Bri-Agent/
  Backend/                # Proyecto .NET Web API (ASP.NET Core)
    BriAgent.Backend.csproj
    Program.cs
    Controllers/
      DemosController.cs
  Frontend/               # Proyecto React + Vite + TypeScript
    package.json
    tsconfig.json
    vite.config.ts        # Proxy de /bri-agent → http://localhost:5080
    index.html
    src/
      main.tsx
      modules/
        App.tsx
```

Notas:
- El proyecto de consola existente se mantiene como banco de pruebas de las demos actuales, pero el API público evoluciona en `Bri-Agent/Backend`.
- El frontend cambia de Angular a React (Vite) para simplificar arranque y dev-experience.

## 1) Inventario de características (catálogo de capacidades)

Agrupadas por temática, con ideas de validación y si ya hay demo base en este repo.

### A. Núcleo de agentes (AI Agents)
- Agente básico (Azure OpenAI) → Validación: respuesta simple.
  - Estado: DEMO existente `Demos/HolaMundo.cs`.
- Streaming de respuestas → Validación: `RunStreamingAsync` muestra tokens parciales.
  - Estado: DEMO existente `Demos/ModoStream.cs`.
- Memoria de conversación por hilo (AgentThread) → Validación: recordar nombre en segundo turno.
  - Estado: DEMO existente `Demos/AgentThread.cs`.
- Salida estructurada (JSON Schema) → Validación: modelar `PersonInfo` y deserializar.
  - Estado: DEMO existente `Demos/StructuredOutput.cs` + `Models/PersonInfo.cs`.
- Herramientas/Function Calling → Validación: 2 tools (clima, plato típico).
  - Estado: DEMO existente `Demos/AgentTools.cs`.
- Aprobación de herramientas (mini-HITL en tools) → Validación: `ApprovalRequiredAIFunction`.
  - Estado: DEMO existente `Demos/ApprovalRequest.cs`.
- Proveedores de modelos múltiples: Azure OpenAI / Local (Ollama) / Foundry Persistente.
  - Azure OpenAI: DEMO existente.
  - Ollama local: DEMO existente `Demos/Ollama.cs`.
  - Azure AI Foundry Agents (persistentes): DEMO existente `Demos/AiFoundryAgent.cs`.

### B. Orquestación y Workflows (Graph Workflows)
- Graph Workflows (ejecutores, aristas, builder) → Validación: grafo mínimo A→B→C.
  - Estado: PENDIENTE implementar demo con `WorkflowBuilder` + `AgentExecutor`.
- Patrones de orquestación: Secuencial, Concurrente, Handoff, Manager+Especialistas, Jerárquico.
  - Estado: PENDIENTE (ver guía `Docs/agent-framework-2h.md`).
- Agentes paralelos (concurrente) + agregación → Validación: router + aggregator y speedup.
  - Estado: PENDIENTE.
- Checkpointing por supersteps → Validación: reanudar desde checkpoint N.
  - Estado: PENDIENTE.
- Human-in-the-Loop (HITL) con pausa/resume real de workflow → `RequestInfoExecutor<TReq,TRes>`.
  - Estado: PENDIENTE (hay mini-HITL en tools, falta el de workflow).
- Background Responses / Continuation Tokens → Validación: pausar y reanudar tarea larga.
  - Estado: PENDIENTE.

### C. Herramientas externas y MCP (Model Context Protocol)
- MCP Client (stdio/HTTP) → Conectarse a servidores MCP y listar/llamar tools.
  - Estado: PENDIENTE (doc base en `Docs/mcp.md`).
- MCP Server (C#) → Exponer tools y prompts desde nuestro backend.
  - Estado: PENDIENTE (doc base en `Docs/mcp.md`).
- MCP Native con Agents → Usar tools MCP dentro de agentes AF.
  - Estado: PENDIENTE.
- Ejemplos útiles MCP: GitHub, Filesystem, Web/Docs, Database, Slack.
  - Estado: PENDIENTE (escoger 1-2 para demo).

### D. Búsqueda, RAG y Azure AI Search
- Integración con Azure AI Search como tool del agente (búsqueda + citas).
  - Estado: PENDIENTE.
- Pipeline de embeddings e indexación (docs del repo) → RAG end-to-end.
  - Estado: PENDIENTE.
- Búsqueda multi-fuente en paralelo (AI Search + Web + FS) + agregación.
  - Estado: PENDIENTE.

### E. Observabilidad y telemetría
- OpenTelemetry para agentes, tools y workflows (trazas distribuidas + métricas).
  - Estado: PENDIENTE (base en `Docs/agent-framework-2h.md`).
- Registro de eventos del workflow (Started, ExecutorComplete, Failure, Output).
  - Estado: PENDIENTE (se hará en las demos de workflows).
- Medición de uso de tokens y latencia por step.
  - Estado: PENDIENTE.

### F. Seguridad, compliance y gobernanza
- Aprobaciones (ya cubierto en tools) y políticas de aprobación para acciones críticas.
  - Estado: DEMO parcial (`ApprovalRequiredAIFunction`).
- Filtros de contenido/PII y validación de input/output vía middleware.
  - Estado: PENDIENTE.
- Auditoría centralizada: quién hizo qué, cuándo y con qué inputs/outputs.
  - Estado: PENDIENTE.

### G. Despliegue y entorno
- Backend en ASP.NET Minimal API para exponer cada feature como endpoint.
  - Estado: PENDIENTE (actualmente consola). 
- Opciones de despliegue: App Service / Container Apps / AI Foundry (hosted agents) / VM.
  - Estado: PENDIENTE (plan en `Docs/mcp.md`).
- Config centralizada (`Config/Credentials.cs`) y secretos por env vars.
  - Estado: EXISTENTE.

### H. Extras y avanzados
- A2A (agent-to-agent): coordinación entre agentes por mensajes.
  - Estado: PENDIENTE.
- Agente coordinador/manager (manager + specialists) y handoff programático.
  - Estado: PENDIENTE.
- Multi-modal (si el modelo lo permite): imagen → texto, etc.
  - Estado: PENDIENTE.
- Evaluación / pruebas automáticas de prompts (smoke tests rápidos).
  - Estado: PENDIENTE.

---

## 2) Mapeo rápido: demos actuales → capacidades

- `Demos/HolaMundo.cs` → Agent básico con Azure OpenAI.
- `Demos/ModoStream.cs` → Streaming.
- `Demos/AgentThread.cs` → Memoria de conversación (threads).
- `Demos/AgentTools.cs` → Tools/Function calling con 2 funciones.
- `Demos/ApprovalRequest.cs` → Aprobación de tools (mini-HITL orientado a función).
- `Demos/Ollama.cs` → Proveedor local (Ollama) como `ChatClientAgent`.
- `Demos/AiFoundryAgent.cs` → Persistent Agents (Azure AI Foundry).
- `Demos/StructuredOutput.cs` → JSON Schema / respuesta tipada.

Gaps principales: Workflows, MCP, AI Search, OpenTelemetry, Checkpointing, Background Responses, A2A y patrones de manager/handoff.

---

## 3) Diseño propuesto del backend (visión)

Implementaremos una Web API (ASP.NET Core) en el proyecto `Bri-Agent/Backend` con rutas por feature. Cada endpoint ejecuta un caso mínimo “verde” que demuestre la capability. El prefijo estándar continúa siendo `/bri-agent`.

Nota: todas las rutas se publicarán bajo el prefijo `/bri-agent` para mantener el namespacing de Bri-Agent.

- `GET /bri-agent/health` → Liveness/Readiness.
- `POST /bri-agent/agents/basic` → Hola mundo.
- `POST /bri-agent/agents/stream` → Streaming (server-sent events opcional o chunked text).
- `POST /bri-agent/agents/thread` → Memoria (envía turnos en el mismo threadId).
- `POST /bri-agent/agents/tools` → Function calling (clima/plato típico).
- `POST /bri-agent/agents/structured` → JSON Schema (retorna JSON tipado).
- `POST /bri-agent/models/ollama` → Ollama local (si está disponible).
- `POST /bri-agent/foundry/persistent-agent` → Ejecuta un agente persistente.
- `POST /bri-agent/workflows/seq` → Workflow secuencial A→B→C con events.
- `POST /bri-agent/workflows/parallel` → Paralelo con aggregator y métricas de speedup.
- `POST /bri-agent/workflows/hitl` → HITL RequestInfo (pausa/resume con token de request).
- `POST /bri-agent/workflows/checkpoint` → Reanudar desde checkpoint N.
- `POST /bri-agent/background/long-task` → Continuation tokens pause/resume.
- `POST /bri-agent/mcp/client/list-tools` → Conectar a un server MCP y listar tools.
- `POST /bri-agent/mcp/client/call-tool` → Invocar tool MCP y devolver resultado.
- `POST /bri-agent/mcp/server/start` → Levantar servidor MCP propio con 1-2 herramientas.
- `POST /bri-agent/search/ai` → Consultar Azure AI Search (tool + citations).
- `POST /bri-agent/search/rag` → RAG end-to-end: indexar y consultar docs locales del repo.
- `GET /bri-agent/telemetry/traces` → Endpoint de prueba para ver trazas OTel (export console/AppInsights).

Notas:
- Cada endpoint incluirá logs y, donde aplique, `IAsyncEnumerable` para streaming.
- Para HITL real, el endpoint devolverá un `requestId` y 
  se ofrecerá `POST /workflows/hitl/respond` para continuar.

Estado inicial ya creado:
- `GET /bri-agent/health` (en `Program.cs`).
- `GET /bri-agent/demos/list` (en `Controllers/DemosController.cs`).

---

## 4) Roadmap por hitos (M0 → M8)

- M0 Catálogo (este documento) → Listo.
- M1 Workflows base: secuencial, paralelo con aggregator y eventos. Checkpoint scaffolding.
- M2 HITL real (RequestInfoExecutor) y Background Responses (continuation tokens).
- M3 MCP: cliente (stdio/HTTP) + servidor simple; integrar tools MCP como tools del agente.
- M4 Azure AI Search + RAG minimal (indexación de Docs/ y consultas con citas).
- M5 OpenTelemetry end-to-end: traces, métricas, logs, token usage.
- M6 A2A & patrones: manager+specialists, handoff; agente coordinador.
- M7 Minimal API pública con todos los endpoints; scripts de despliegue a Azure.
- M8 Extras: seguridad/PII middleware, auditoría, multi-modal, tests automáticos.

---

## 5) Requisitos y dependencias

- .NET 9 (ya en el proyecto).
- Paquetes Agent Framework (Microsoft.Agents.AI) y, para workflows, paquete correspondiente.
- OpenTelemetry: `OpenTelemetry`, `OpenTelemetry.Exporter.Console` y/o App Insights.
- Azure AI Search SDK (cuando implementemos búsqueda).
- MCP C# SDK según guía en `Docs/mcp.md`.
- Configuración por `Config/Credentials.cs` + variables de entorno para secretos.

---

## 6) Validación por feature (criterios de aceptación)

- Cada endpoint devuelve 200 y un payload mínimo verificable (texto/JSON/eventos).
- Workflows: se emiten `WorkflowStartedEvent`, `ExecutorCompleteEvent`, `WorkflowOutputEvent` y, si aplica, `SuperStepCompletedEvent`.
- HITL: retorno de `requestId`, espera y reanudación exitosa.
- Background: pausa y reanuda sin perder progreso (mismo contenido continúa).
- MCP: `list-tools` y `call-tool` operativos contra un server conocido (p. ej. server-everything).
- AI Search/RAG: respuesta con citas y ranking básico.
- OTel: se observan spans/traces por agente, tool y executor.

---

## 7) Próximos pasos inmediatos

1) Consolidar Web API en `Bri-Agent/Backend` (CORS habilitado para `http://localhost:5173`).
2) Implementar endpoints de agentes básicos (basic/stream/thread/tools/structured) bajo `/bri-agent`.
3) Añadir módulo de workflows con ejemplo secuencial y paralelo.
4) Preparar HITL (RequestInfo) con endpoint de respuesta.
5) Integrar OpenTelemetry básico (export console) para todos los endpoints.
6) Migrar el mecanismo dinámico de demos (`IApiDemo` + reflexión) al nuevo Backend.

Una vez cubierto M1–M2, seguimos con MCP y AI Search (M3–M4).

---

## 8) MCP Detallado (profundización y ejemplos previstos)

El objetivo es no solo “conectar” con MCP sino demostrar claramente los 3 ángulos: (1) Cliente MCP (stdio / HTTP), (2) Servidor MCP propio en .NET, (3) Integración nativa dentro de agentes y workflows.

### 8.1 Escenarios MCP a cubrir
| Escenario | Descripción | Tipo | Prioridad |
|-----------|-------------|------|-----------|
| Cliente STDIO básico | Conectarse a `@modelcontextprotocol/server-everything` y listar tools | Client | Alta |
| Invocación tool simple | Llamar `echo` y mostrar request/response | Client | Alta |
| Tool con parámetros | Llamar una tool con argumentos (ej: búsqueda) | Client | Alta |
| Cliente HTTP/SSE | Conectar a servidor MCP expuesto vía HTTP (stream de eventos) | Client | Media |
| Servidor MCP mínimo | Exponer 1 tool (`EchoTool`) y 1 prompt (`haiku`) | Server | Alta |
| Servidor con dependencia externa | Tool que hace fetch HTTP y resume (ej: `SummarizeUrl`) | Server | Media |
| Registro dinámico de tools | Agregar nueva tool en runtime (hot registration) | Server | Media |
| Integración Agent + MCP Tool | Agent que usa tool remota vía MCP para obtener datos | Agent+Client | Alta |
| Workflow multi-MCP | Paso 1: GitHub MCP → Paso 2: Filesystem MCP → Paso 3: Agente sintetiza | Workflow | Media |
| Seguridad / Aprobación | Tool marcada como “requiere aprobación” antes de ejecutar | Client | Media |
| Cache de resultados MCP | Cache local para evitar repetir llamadas costosas | Client | Baja |
| Observabilidad OTel | Span por cada llamada a tool MCP con tags (provider, latency) | Cross | Alta |

### 8.2 Contratos Propuestos

Endpoints adicionales (bajo prefijo `/bri-agent`):
- `POST /bri-agent/mcp/client/connect` → (body: { "transport": "stdio"|"http", config }) retorna `connectionId`.
- `GET /bri-agent/mcp/client/{connectionId}/tools` → Lista tools y metadatos.
- `POST /bri-agent/mcp/client/{connectionId}/call` → { toolName, arguments } devuelve resultado y timing.
- `POST /bri-agent/mcp/server/start` → Levanta servidor local (se mantiene en memoria). Devuelve `serverId` y puerto/stdio pid.
- `GET /bri-agent/mcp/server/{serverId}/status` → Estado, tools activas.
- `POST /bri-agent/mcp/server/{serverId}/register-tool` → Registro dinámico (nombre + dll/class o delegado inline para entorno demo).

Formato de tool metadata (respuesta parcial):
```json
{
  "name": "summarize_url",
  "description": "Descarga y resume una página web",
  "inputSchema": {"type": "object", "properties": {"url": {"type": "string"}}},
  "requiresApproval": false
}
```

### 8.3 Integración con Agents
Adapter para envolver herramientas MCP como `AIFunction`:
1. Resolver tool metadata del MCP client.
2. Generar dinámicamente un `MethodInfo` / delegado que acepte un `Dictionary<string,object>` y llame `CallToolAsync`.
3. Exponerlo en `tools: [ ... ]` al crear el `AIAgent`.
4. Opcional: interceptar para logging y latencia.

### 8.4 Diseño Interno Cliente MCP (STDIO)
Componentes:
- `McpProcessManager` (lanza proceso, controla stdin/stdout, reinicio si falla).
- `McpMessageRouter` (JSON-RPC frames → Observables internos).
- `McpToolCatalog` (cache tools + expiración configurable).

### 8.5 Servidor MCP en .NET
Estructura propuesta:
```
/Mcp
  /Server
    McpServerHost.cs
    Tools/
      EchoTool.cs
      SummarizeUrlTool.cs
      TimeTool.cs
    Prompts/
      HaikuPrompt.cs
```
`McpServerHost` expondrá:
```csharp
public Task<RunningMcpServer> StartAsync(McpServerOptions opts);
```
Donde `RunningMcpServer` incluye `StopAsync()`, `Port`, `ProcessId`.

### 8.6 Ejemplo de Código (borrador cliente + agente)
```csharp
// Obtener tools desde MCP y adaptarlas a Agent
var mcp = await McpClientFactory.ConnectStdioAsync("server-everything");
var tools = await mcp.ListToolsAsync();

var adapted = tools.Select(t => McpToolAdapter.CreateAIFunction(mcp, t)).ToList();
var agent = chatClient.CreateAIAgent(
    name: "McpAugmentedAgent",
    instructions: "Usa las tools MCP si son útiles",
    tools: adapted
);
var resp = await agent.RunAsync("Resume https://example.com y dime el título");
```

---

## 9) Plan de Frontend Angular (resumen integrado)
Cambios: se sustituye Angular por React + Vite para el frontend (`Bri-Agent/Frontend`). Objetivos clave: visualización de eventos, grafo de workflows, inspector de memoria, panel de código y exploración MCP.

### 9.1 Estructura Carpetas
```
`/Bri-Agent/Frontend` (React + TS + Vite)
  - `src/modules/App.tsx` (demo list + navegación inicial)
  - `src/main.tsx` (bootstrap)
  - `vite.config.ts` (proxy `/bri-agent` → `http://localhost:5080`)
  - Próximos módulos: `features/demos`, `features/workflows`, `features/mcp`, `features/hitl`, `features/background`, `features/metrics`, `features/search`.
```

### 9.2 Componentes Clave (React)
- `demo-selector` (lista dinámica `/bri-agent/demos/list`).
- `prompt-console` (input, historial, streaming tokens).
- `agent-events-timeline` (timeline unificado tool/events).
- `workflow-graph` (Cytoscape/D3: nodos coloreados por estado).
- `hitl-modal` (aprobaciones).
- `background-panel` (continuation tokens + pausa/reanuda).
- `code-viewer` (snippets + copy + highlight).
- `mcp-browser` (listar tools, ejecutar, ver schema y resultado).
- `metrics-dashboard` (tokens, latencia, errores, tool calls/min).

### 9.3 Estrategia de Streaming
- SSE para: `/workflows/*/events`, `/agents/stream`, `/background/long-task`.
- Reintento exponencial si la conexión cae.
- Buffer en memoria con límite (evitar crecimiento infinito) + poda antigua.

### 9.4 Estado Global
Estado con React Context + reducers (y posibilidad de RTK si crece): `agentSession`, `workflows`, `mcp`, `code`, `hitl`, `metrics`.

### 9.5 Facilidad para añadir nuevas demos
- Endpoint backend `/demos/register` (solo dev) acepta JSON metadata → hot reload lista.
- Front consulta `/demos/list` al iniciar; cada demo describe: id, título, descripción, tags, endpoints involucrados, archivos.

---

## 10) Mecanismo para Añadir Nuevas Demos (Backend + Frontend)

### 10.1 Backend
Directorio estándar: `DemosRuntime/` para demos API (separar de las de consola actuales). Cada demo expone una clase que implementa interfaz:
```csharp
public interface IApiDemo
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    IEnumerable<string> Tags { get; }
    void MapEndpoints(IEndpointRouteBuilder app); // Registra /api/demos/{Id}/run, etc.
    IEnumerable<string> SourceFiles { get; } // Rutas relativas para panel de código
}
```
Registro automático al arranque con reflexión:
```csharp
var demos = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => typeof(IApiDemo).IsAssignableFrom(t) && !t.IsInterface)
    .Select(Activator.CreateInstance)
    .Cast<IApiDemo>()
    .ToList();
```
Exponer:
```csharp
app.MapGet("/bri-agent/demos/list", () => demos.Select(d => new {
  d.Id, d.Title, d.Description, d.Tags
}));
```

### 10.2 Frontend
`DemoRegistryService`:
1. Carga `/demos/list`.
2. Genera menú dinámico.
3. Para cada demo carga configuración (endpoints, modo stream o single-shot).
4. Code viewer llama `/code/{demoId}` para los archivos listados en `SourceFiles`.

Actualizar rutas a prefijo:
- 1) Carga `/bri-agent/demos/list`
- 4) Code viewer: `/bri-agent/code/{demoId}`

Dev server y CORS:
- Backend escucha en `http://localhost:5080` (configurable). Frontend Vite en `http://localhost:5173`.
- `vite.config.ts` ya configura proxy para `/bri-agent` hacia el Backend, evitando CORS.

### 10.3 Añadir Demo Nueva (Checklist)
1. Crear clase `MyNewDemo.cs` que implemente `IApiDemo`.
2. Implementar `MapEndpoints` con endpoint(s) REST/SSE.
3. Añadir archivos a `SourceFiles`.
4. (Opcional) Si necesita recursos (MCP connect), reusar servicios compartidos (singleton).
5. Front recibe automáticamente en el selector.

Esto elimina edición manual de menús y reduce fricción.

---

## 11) Roadmap Unificado (Backend + Frontend Reforzado)

| Iter | Backend | Frontend | MCP Enfoque |
|------|---------|----------|-------------|
| M1 | Minimal API base + endpoints agentes básicos + `/demos/list` | Shell Angular + DemoSelector + PromptConsole | Diseño contratos |
|    | (Ya iniciado en `Bri-Agent/Backend`)                           | (Shell React + lista dinámica creada)        |                  |
| M2 | Workflows secuencial y paralelo + eventos SSE | WorkflowGraph (estático) + Timeline básica | Cliente STDIO listar tools |
| M3 | HITL Workflow + Background Responses | HITL Modal + Background Panel | Invocar tool `echo` y tool con params |
| M4 | Checkpointing + reanudar | Mostrar supersteps y checkpoints | Servidor MCP mínimo (Echo + Prompt) |
| M5 | OpenTelemetry spans + métricas | MetricsDashboard + resaltado latencias | Integrar tool MCP en Agent |
| M6 | AI Search + RAG | Search/RAG UI + citas | Cliente HTTP MCP + Observabilidad |
| M7 | A2A patrones + Manager/Specialists | Actualizaciones dinámicas grafo | Workflow multi-MCP |
| M8 | Seguridad (PII, approvals avanzadas) + Auditoría | Panel de auditoría + filtros | Registro dinámico de tools / caché |
| M9 (extra) | Multi-modal + evaluación prompts | Panel comparativo respuestas | Trazas avanzadas por tool |

---

## 12) Matriz de Ejemplos MCP Propuestos

| Demo | Descripción | Código Clave | Valor Didáctico |
|------|-------------|--------------|-----------------|
| mcp-client-basic | Conexión stdio, listar tools | `McpClientFactory.ConnectStdioAsync` | Primer contacto |
| mcp-client-call-echo | Llamar tool `echo` | `CallToolAsync("echo", {"message":"hola"})` | Request/response básico |
| mcp-client-call-args | Tool con JSON schema y validación | Adaptador argumentos → JSON | Comprender parámetros |
| mcp-server-min | Servidor local con EchoTool + Prompt | `McpServerHost.StartAsync` | Cómo exponer tools |
| mcp-server-summarize | Tool que hace HTTP fetch y resume | HttpClient + LLM | Integración externa |
| mcp-agent-integration | Agent usa tools MCP | Adapter → `AIFunctionFactory` | Enriquecer reasoning |
| mcp-workflow-chain | Workflow que encadena 2 servers MCP | Multi client + aggregator | Composición distribuida |
| mcp-approval-tool | Tool marcada “requiere aprobación” | Decorador approval | Gobernanza |
| mcp-cache | Cache local results | MemoryCache decorator | Optimización |
| mcp-otel | Spans y atributos (toolName, latency) | OTel instrumentation | Observabilidad |

---

## 13) Resumen de Cambios (Esta Revisión)
- Añadida sección MCP profunda (escenarios, contratos, arquitectura cliente/servidor, integración).
- Añadido plan frontend Angular (estructura, componentes, streaming, estado).
- Definido mecanismo de registro dinámico de demos (`IApiDemo` + reflexión).
- Roadmap unificado extendido (M1–M9) combinando backend, frontend y MCP.
- Matriz de ejemplos MCP para guiar implementación incremental.
- Incorporada identidad de proyecto "Bri-Agent" y estandarizado prefijo `/bri-agent` en rutas y endpoints.

---

## 14) Próximas Acciones Recomendadas
1. Implementar interfaz `IApiDemo` y endpoint `/demos/list` (base auto-registro).
2. Crear proyecto `frontend/` (Angular) con módulo `features/demos` + servicio inicial.
3. Implementar primera demo MCP cliente básico (listar tools) para validar patrón adapter.
4. Añadir servidor MCP mínimo y exponer tool local.
5. Instrumentar OTel mínimo (span por llamada a tool) antes de crecer complejidad.

### 14.1) Progreso alcanzado (Monorepo React + Backend API)

- Se creó estructura `Bri-Agent/Backend` (ASP.NET Core Web API) y `Bri-Agent/Frontend` (React + Vite + TS).
- Endpoints activos: `GET /bri-agent/health`, `GET /bri-agent/demos/list`, `POST /bri-agent/agents/basic`, `POST /bri-agent/demos/bri-basic-agent/run`.
- Registro dinámico de demos (`IApiDemo` + `DemoRegistry`) operativo para la demo básica.
- CORS + proxy configurados para desarrollo local (`5173` → `5080`).
- Plan actualizado para reflejar migración de Angular → React.

### 14.2) Siguiente bloque (Iteración inmediata)

1. Añadir endpoint `POST /bri-agent/agents/stream` (SSE) y validar tokens parciales.
2. Implementar `POST /bri-agent/agents/thread` basado en la demo `AgentThread` (memoria conversacional). 
3. Añadir endpoints para herramientas y structured output.
4. Esqueleto Workflow secuencial (`/bri-agent/workflows/seq`).
5. Documentar en README cómo agregar nuevas demos API.
6. Iniciar pruebas automatizadas (xUnit) para health y basic agent.
7. Definir contrato SSE claro (prefijo 'event:' opcional, flush por chunk).

### 14.3) Riesgos / Consideraciones

- Credenciales Azure OpenAI necesarias para todos los endpoints de agente; manejar errores explicativos.
- Streaming: asegurar flush manual y back-pressure mínimo.
- Thread/memoria: elegir estrategia de almacenamiento (in-memory diccionario por `threadId`).
- Escalabilidad: mover en el futuro agent factory y thread store a servicios singleton registrados vía DI.

### 14.4) Métricas Iniciales a Instrumentar (Futuro cercano)

- Latencia por endpoint.
- Tokens usados por respuesta básica.
- Conteo de llamadas a demos dinámicas.
- Errores por tipo (tool-error, agent-error, validation-error).

Con esto aseguramos cimientos sólidos y facilidad para iterar rápidamente.

---

## 15) Refactor de Controladores (Separación y Base Común)

### 15.1 Objetivo
Reducir acoplamiento de `AgentsController`, mejorar legibilidad pedagógica y habilitar evolución futura (workflows, MCP, HITL, background tasks) sin inflar un único archivo. Cada ejemplo tendrá su propio controlador y todos heredarán utilidades comunes de un controlador base.

### 15.2 Controladores Propuestos
| Controlador | Rutas (manteniendo prefijo /bri-agent) | Responsabilidad Principal |
|-------------|----------------------------------------|---------------------------|
| `BasicAgentController` | `POST /agents/basic` | Llamada simple sin estado |
| `StreamingAgentController` | `POST /agents/stream`, `GET /agents/stream` | Streaming tokens SSE |
| `ThreadAgentController` | `POST /agents/thread`, `GET /agents/thread/stream`, `GET /agents/thread/{id}/history` | Memoria conversacional (contexto real) |
| `ToolsAgentController` | `POST /agents/tools` | Function calling y approvals simples |
| `StructuredAgentController` | `POST /agents/structured` | Salida JSON Schema |
| (Futuro) `WorkflowController` | `/workflows/*` | Orquestación secuencial/paralela/HITL |
| (Futuro) `McpController` | `/mcp/*` | Cliente/servidor MCP, adaptación de tools |
| (Futuro) `SearchController` | `/search/*` | AI Search, RAG |
| (Futuro) `BackgroundController` | `/background/*` | Tareas largas, continuation tokens |

### 15.3 BaseAgentController (borrador de interfaz)
Métodos protegidos reutilizables:
- `SetSseHeaders(HttpResponse response)`
- `Task WriteStartedAsync(object payload)`
- `Task WriteTokenAsync(string text)`
- `Task WriteCompletedAsync(object? payload = null)`
- `Task WriteErrorAsync(string message)`
- `Activity StartTelemetry(string name, int promptLength)` + helper para registrar métricas en `TelemetryStore` al finalizar.
- `AIAgent CreateBasicAgent()` (delegado a `AgentFactory`).

Inyección sugerida por constructor: `AgentFactory`, `ThreadStore`, `TelemetryStore`, `ILogger<T>`, `Credentials`.

### 15.4 Migración Incremental (sin romper frontend)
1. Crear `BaseAgentController` (abstracto) sin eliminar nada de `AgentsController`.
2. Extraer streaming a `StreamingAgentController`; mantener rutas originales. (El antiguo método puede marcarse `[Obsolete]` durante transición.)
3. Mover endpoints de thread.
4. Mover basic, tools y structured.
5. Eliminar o dejar vacío `AgentsController` cuando todo se verifique.
6. Añadir nuevos controladores para features futuras directamente.

### 15.5 Riesgos y Mitigaciones
| Riesgo | Mitigación |
|--------|------------|
| Pérdida de telemetría consistente | Centralizar en BaseAgentController |
| Fuga de diferencias SSE (headers/event names) | Unificar métodos de escritura SSE |
| Regresión en frontend por cambios de payload | Mantener schema actual y añadir campo nuevo `meta` no disruptivo |
| Duplicación de código durante migración | Fase corta; remover métodos viejos tras verificación |

### 15.6 Pruebas de Regresión Mínimas
- Basic: status 200, campos `prompt`, `response`.
- Stream: secuencia started→token(s)→completed.
- Thread: persistencia de historial entre turnos (nombre recordado en segundo turno).
- Tools: presencia de respuesta que combine tool outputs.
- Structured: JSON válido deserializable a `PersonInfo`.

---

## 16) Parámetro de Comportamiento para el Frontend (meta UI)

### 16.1 Motivación
El frontend necesita saber cómo renderizar cada respuesta/stream (chat con memoria, panel estructurado, viewer de workflow, inspector de tools, etc.) sin lógica condicional dura por endpoint. Añadimos un campo estándar `meta.ui` en respuestas JSON y eventos `started` de SSE.

### 16.2 Especificación del Campo `meta`
Estructura general:
```json
{
  "data": { /* payload actual existente */ },
  "meta": {
    "version": "v1",
    "demoId": "thread-agent",
    "controller": "ThreadAgentController",
    "ui": {
      "mode": "thread",             // single | streaming | thread | workflow | tools | structured | mcp | hitl | background | checkpoint
      "stream": true,                 // ¿Emite tokens parciales?
      "history": true,                // ¿Debe mostrar historial conversacional?
      "structured": false,            // ¿Render JSON schema?
      "tools": ["clima", "plato"],  // Lista de tools relevantes (si aplica)
      "recommendedView": "ThreadChat", // Sugerencia de componente frontend
      "capabilities": ["memory","streaming"]
    }
  }
}
```

Para SSE, el primer evento `started` incluirá:
```json
event: started
data: {
  "prompt": "Hola...",
  "threadId": "abc123",
  "meta": {
    "version": "v1",
    "demoId": "thread-agent",
    "ui": { "mode": "thread", "stream": true, "history": true, "recommendedView": "ThreadChat" }
  }
}
```

### 16.3 Taxonomía de Modos UI
| Modo | Descripción | Componentes Clave |
|------|-------------|-------------------|
| single | Respuesta única | PromptConsole |
| streaming | Tokens en tiempo real | StreamingViewer |
| thread | Chat con memoria | ThreadChat, HistoryPanel |
| tools | Llamadas a funciones | ToolCallInspector |
| structured | JSON con schema | StructuredViewer |
| workflow | Estados y eventos | WorkflowGraph, EventsTimeline |
| mcp | Exploración de tools MCP | McpBrowser |
| hitl | Pausa/aprobación humana | HitlModal |
| background | Tarea larga reanudable | BackgroundPanel |
| checkpoint | Reanudar ejecución | CheckpointResumeView |

### 16.4 Compatibilidad y Fallback
Si `meta` no existe (endpoints antiguos): frontend asume `mode=single`. Implementar util `deriveUiProfile(response)` que:
1. Busca `response.meta.ui.mode`.
2. Si no, inspecciona campos (`threadId` → thread, `person` → structured, presencia de eventos SSE tokens → streaming).

### 16.5 Ejemplos Adicionales
Structured Output:
```json
{
  "person": {"name":"Ana","age":32,"occupation":"Ingeniera"},
  "meta": {"version":"v1","demoId":"structured-agent","ui":{"mode":"structured","structured":true,"recommendedView":"StructuredViewer","capabilities":["structured"]}}
}
```

Tools:
```json
{
  "question": "¿Clima y plato en Madrid?",
  "response": "El clima ... Cocido madrileño",
  "meta": {"version":"v1","demoId":"tools-agent","ui":{"mode":"tools","tools":["ObtenerClima","RecomendarPlato"],"recommendedView":"ToolCallInspector","capabilities":["tools"]}}
}
```

### 16.6 Pasos de Implementación
1. Definir record/DTO `UiMeta` y `UiProfile` en Backend compartido.
2. Añadir helper para envolver respuestas: `Wrap(payload, uiProfile)`.
3. Extender eventos SSE `started` para inyectar `meta`.
4. Modificar cada controlador nuevo para construir su `UiProfile`.
5. Frontend: crear hook `useUiProfile(responseOrEvent)` y componentes condicionales.
6. Documentar en README sección “Meta UI Contract”.

### 16.7 Edge Cases
- Respuestas de error: incluir `meta` si fue posible construirlo, de lo contrario fallback global.
- Streaming + Structured (caso futuro): primero `started` con `structured=true`, al final evento `completed` con JSON final.
- Workflow con HITL: cuando se emite un evento de pausa, enviar evento `hitl-request` con `meta.ui.mode="hitl"`.

### 16.8 Métricas sobre Meta UI (futuro)
Agregar contadores por `mode` para evaluar uso y priorizar mejoras UX.

---

## 17) Plan de Pruebas para Refactor y Meta UI

### 17.1 Objetivos
Garantizar que la introducción de controladores separados y `meta.ui` no rompe contratos existentes y habilita comportamiento dinámico en front.

### 17.2 Pruebas Sugeridas (xUnit / Playwright)
- Basic: assert 200 y `meta.ui.mode == single`.
- Streaming: capturar evento `started` y verificar `meta.ui.stream == true`.
- Thread: segunda llamada conserva historial (turns incrementan) y `meta.ui.mode == thread`.
- Structured: deserializar persona y `meta.ui.structured == true`.
- Tools: respuesta contiene outputs combinados y `meta.ui.tools` lista ambos.

### 17.3 Estrategia Incremental
1. Añadir tests para actuales endpoints sin meta (baseline).
2. Introducir meta en 1 controlador (streaming) y actualizar tests.
3. Expandir a thread y structured.
4. Migrar remaining (basic, tools) y eliminar tests antiguos.

---

## 18) Roadmap de Refactor (Sprint Nivel)
| Sprint | Objetivo | Entregables |
|--------|----------|-------------|
| S1 | BaseAgentController + Streaming separado | Clase base + StreamingAgentController + meta.ui en streaming |
| S2 | Thread + Basic separados | ThreadAgentController, BasicAgentController, tests thread |
| S3 | Tools + Structured | ToolsAgentController, StructuredAgentController con meta.ui |
| S4 | Workflow inicial + meta.ui workflow | WorkflowController (secuencial), SSE events con meta |
| S5 | MCP cliente + servidor | McpController, adapter a tools, meta.ui modo mcp |
| S6 | Background + Checkpoint | BackgroundController, Checkpoint endpoints |
| S7 | HITL + Aprobaciones Workflow | hitl events, meta.ui hitl |
| S8 | Search/RAG + Observabilidad refinada | SearchController, métricas por mode |

---

## 19) Resumen Ejecutivo Añadido
Se define un refactor controlado por fases para separar responsabilidades en controladores temáticos con una capa base reutilizable, y se introduce un contrato estándar `meta.ui` que guía el frontend sobre cómo renderizar cada respuesta o stream. Esto habilita escalabilidad funcional (workflows, MCP, HITL, background) sin aumentar complejidad accidental en el lado cliente.

