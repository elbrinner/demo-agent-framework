# Checklist de validación de demos (Bri-Agent)

Este documento sirve como lista de verificación para probar cada demo end‑to‑end. Usa las casillas para marcar lo completado.

Referencia de alcance: además de las demos actuales, incluye las planificadas en `plan.md` (Workflows, MCP, Search/RAG, HITL, Background, Ollama, Foundry, etc.).

---
## Leyenda rápida
- [ ] No iniciado
- [~] En progreso / validación parcial
- [x] Validado

---
## Demos actuales (según `/bri-agent/demos/list`)

### 1) bri-basic-agent
- [x] Backend
   - [x] POST `/bri-agent/agents/basic` devuelve 200
   - [x] `response` no vacía y `usage.promptChars` correcto
   - [x] Telemetría registrada (`basic-agent`)
   - [x] `meta.ui.mode == single` y `stream == false`
- [x] Frontend
   - [x] Input acepta prompt, Enter lo envía, limpia campo y mantiene foco
   - [x] Panel de respuesta muestra texto completo
   - [x] TraceId/SpanId visibles cuando existen
   - [x] No aparece StreamPanel
- [x] Ver código
   - [x] Viewer abre con archivos persistidos en `Bri-Agent/Backend/Code/bri-basic-agent/Demos/HolaMundo.cs`
   - [x] Badges correctos: `origin=code`, `persisted` (según corresponda)
   - [x] Archivo clave presente: `Bri-Agent/Backend/Code/bri-basic-agent/Demos/HolaMundo.cs`

### 2) streaming-agent
- [x] Backend
   - [x] SSE emite `started` → `token`(n) → `completed`
   - [x] `meta.ui.mode == streaming`, `stream == true`
- [x] Frontend
   - [x] Botón Stream activa conexión y muestra “conectando…” breve
   - [x] StreamPanel recibe tokens acumulados y rate tok/s
   - [x] Input deshabilitado solo durante conexión inicial
   - [x] Estado “completado” al cierre
   - [x] No se muestra botón "Ejecutar" (solo Stream SSE)
- [x] Ver código
   - [x] Archivo educativo cargado desde `Bri-Agent/Backend/Code/streaming-agent/StreamingAgent.cs`
   - [x] Badges visibles (origin/persisted) y rutas válidas

### 3) thread-agent
- [x] Backend
   - [x] POST `/bri-agent/agents/thread` guarda historial (ThreadStore)
   - [x] GET `/bri-agent/agents/thread/stream` emite tokens y luego historial
   - [x] GET `/bri-agent/agents/thread/{id}/history` alterna usuario/agente
   - [x] `meta.ui.mode == thread`, `history == true`
- [x] Frontend
   - [x] Historial renderizado (Usuario vs Agente) y turns correctos
   - [x] Reset Thread limpia `threadId` e historial
   - [x] Input limpia y foco tras enviar
- [x] Ver código
   - [x] `Controllers/ThreadAgentController.cs`, `Services/ThreadStore.cs` presentes y cargados

### 4) structured-agent
- [x] Backend
   - [x] `/bri-agent/demos/structured-agent/run` retorna `personInfo` con campos esperados
   - [x] `meta.ui.mode == structured`
- [x] Frontend
   - [x] Respuesta JSON formateada/legible (viewer genérico por ahora)
   - [x] Input limpia y foco tras enviar
- [x] Ver código
   - [x] `DemosRuntime/StructuredAgentApiDemo.cs`, `Models/PersonInfo.cs`

### 5) tools-agent
- [x] Backend
   - [x] `/bri-agent/demos/tools-agent/run` retorna respuesta textual
   - [x] `meta.ui.mode == tools`
- [x] Frontend
   - [x] Muestra respuesta completa; input limpieza y foco
- [x] Ver código
   - [x] `DemosRuntime/ToolsAgentApiDemo.cs`

### 6) sequential-workflow
- [x] Backend
   - [x] SSE emite `workflow_started`, `step_started`, `token`, `step_completed`, `workflow_completed`
   - [x] `meta.ui.mode == workflow`
- [x] Frontend
   - [x] Vista de workflow refleja pasos en orden y estado (activo/completo)
   - [x] Tokens por step visibles (si aplica)
- [x] Ver código
   - [x] `DemosRuntime/SequentialWorkflowApiDemo.cs`, `Controllers/WorkflowController.cs`

### 7) parallel-workflow
- [ ] Backend
   - [ ] SSE como secuencial pero intercalando tokens con `stepId`
   - [ ] `meta.ui.mode == workflow`
- [ ] Frontend
   - [ ] Agrupa tokens por step y visualiza ejecución simultánea
- [ ] Ver código
   - [ ] `DemosRuntime/ParallelWorkflowApiDemo.cs`, `Controllers/WorkflowController.cs`

---
## Demos planificadas (de `plan.md`)

### A) Núcleo de agentes / Proveedores
- [ ] Azure OpenAI (básico) — ya cubierto por basic/stream
- [ ] Ollama local (`Demos/Ollama.cs`)
   - [ ] Backend: endpoint dedicado para invocar modelo local
   - [ ] Frontend: selector de modelo y manejo de errores si no disponible
   - [ ] Ver código: `Demos/Ollama.cs`
- [ ] Azure AI Foundry Agents persistentes (`Demos/AiFoundryAgent.cs`)
   - [ ] Backend: endpoint que usa agente persistente
   - [ ] Frontend: badge “persistente” visible
   - [ ] Ver código: `Demos/AiFoundryAgent.cs`
- [ ] Approval en tools (`Demos/ApprovalRequest.cs`)
   - [ ] Backend: requiere confirmación antes de ejecutar función
   - [ ] Frontend: modal HITL simple para aprobar/denegar
   - [ ] Ver código: `Demos/ApprovalRequest.cs`

### B) Workflows y orquestación
- [ ] Grafo mínimo A→B→C (builder + ejecutores)
   - [ ] Backend: builder y ejecución secuencial con eventos
   - [ ] Frontend: representación simple de grafo
- [ ] Patrones: Concurrente, Handoff, Manager+Especialistas, Jerárquico
   - [ ] Backend: rutas de demo por patrón con SSE
   - [ ] Frontend: visualización de estados por nodo
- [ ] Agregación y speedup paralelo (router + aggregator)
   - [ ] Backend: cálculo de speedup estimado
   - [ ] Frontend: métrica visible
- [ ] Checkpointing por supersteps
   - [ ] Backend: reanudar desde checkpoint N
   - [ ] Frontend: control para reanudar / mostrar checkpoint actual
- [ ] HITL en Workflow (RequestInfoExecutor)
   - [ ] Backend: pausa y resume con `requestId`
   - [ ] Frontend: modal de intervención humana
- [ ] Background Responses / Continuation Tokens
   - [ ] Backend: pausa/resume de tarea larga
   - [ ] Frontend: panel background con token de continuación

### C) MCP (Model Context Protocol)
- [ ] Cliente STDIO: conectar y listar tools
- [ ] Cliente: invocar tool (echo) y con parámetros (schema)
- [ ] Cliente HTTP/SSE: conectar por HTTP y recibir eventos
- [ ] Servidor MCP mínimo (EchoTool + Prompt)
- [ ] Registro dinámico de tools (hot add)
- [ ] Integración Agents + MCP como tools
- [ ] Workflow multi‑MCP (cadena de pasos)
- [ ] Aprobación previa en tool sensible
- [ ] Cache de resultados de tool
- [ ] Observabilidad OTel por tool MCP

### D) Search, RAG y Azure AI Search
- [ ] Tool búsqueda AI Search con citas
- [ ] Pipeline embeddings + indexación (RAG end‑to‑end)
- [ ] Búsqueda multi‑fuente en paralelo + agregación

### E) Observabilidad
- [ ] OpenTelemetry para agentes, tools y workflows (trazas + métricas)
- [ ] Registro de eventos de workflow (Started, ExecutorComplete, Output)
- [ ] Uso de tokens y latencia por step

### F) Seguridad y gobernanza
- [ ] Filtros de contenido/PII
- [ ] Políticas de aprobación para acciones críticas
- [ ] Auditoría centralizada

### G) Despliegue y entorno
- [ ] Minimal API pública consolidada
- [ ] Opciones de despliegue (App Service / Container Apps / Foundry / VM)
- [ ] Config por env vars (ya existe base en `Config/Credentials.cs`)

### H) Extras
- [ ] A2A (agent‑to‑agent)
- [ ] Manager + Specialists / Handoff
- [ ] Multi‑modal (imagen→texto, etc.)
- [ ] Evaluación automatizada de prompts

---
## Criterios globales Frontend (aplican a todas las demos)
- [ ] Input limpia tras enviar y mantiene foco
- [ ] Placeholder uniforme: “su consulta aqui”
- [ ] Indicador envío/conexión no invasivo
- [ ] Toast de errores en POST y SSE
- [ ] Usa `meta.ui` para ajustar vista recomendada
- [ ] Muestra rate de tokens cuando haya streaming

## Validación genérica del botón “Ver código”
- [ ] GET `/bri-agent/demos/{id}/code` retorna `files[].found == true`
- [ ] Si `persisted == true`, la ruta base es `Bri-Agent/Backend/Code/{id}`
- [ ] Badges: `origin` y `persisted` correctos
- [ ] Archivos clave presentes y contenido > 0 chars
- [ ] README educativo enlazable si existe

---
Notas:
- Para detalles y roadmap completo, ver `plan.md`.
- Se recomienda automatizar esta checklist con tests (xUnit para API y Playwright para UI).

