# Plan MCP + Agent Framework (Filesystem)

Fecha: 2025-11-11
Alcance: Diseñar e integrar un agente (lado .NET) que se conecta a un servidor MCP de filesystem con permisos restringidos para: listar ficheros, leer ficheros y resumir su contenido. Se publicará un Controller en el Backend para exponer estas capacidades. También se incluye el diseño del servidor MCP.

Nota de nombre de archivo: en macOS el carácter ":" puede causar fricciones; por eso este documento se guarda como `mcp-plan.md` en la raíz del repo (en lugar de `mcp:plan.md`).

---

## Objetivo y criterio de éxito

- Agente en .NET (Agent Framework) capaz de:
  - Listar recursos (archivos) de una ruta permitida.
  - Leer el contenido de un archivo permitido.
  - Resumir el contenido leído mediante un LLM ya configurado en el proyecto.
- Conexión a un servidor MCP de filesystem que expone solo una carpeta whitelisted.
- Endpoints HTTP en el Backend para accionar estas capacidades desde Frontend o herramientas.
- Pruebas básicas para validar el flujo feliz y 1-2 casos de borde.

---

## Arquitectura (alto nivel)

- MCP Server (filesystem): proceso Node.js (stdio o WebSocket) que expone recursos de un directorio permitido y operaciones de lectura.
- Backend .NET (Bri-Agent/Backend):
  - Servicio `McpFileSystemService` que crea un cliente MCP, conecta y ejecuta `List` y `Read`.
  - Controller `McpController` con endpoints REST.
  - Herramientas del Agent Framework (Tools) que usan el servicio para componer flujos y, para resumen, invocan el LLM del proyecto.
- Frontend (opcional): botones para listar/leer/resumir.

---

## Rutas y configuración

- Ruta permitida (macOS): configurable por variable de entorno `MCP_FS_ALLOWED_PATH`. Ejemplo: `/Users/<user>/Documents/demo`.
- Si se quiere compatibilidad Windows, usar `C:/demo` como ejemplo en docs, pero la variable manda.
- Transporte MCP: `stdio` (recomendado para arrancar el server como proceso hijo) o `ws` si se aloja aparte.

---

## Servidor MCP (filesystem) en .NET

Requisito actualizado: el servidor MCP debe ser .NET. Implementaremos un proceso consola que habla MCP por stdio. El server expondrá handlers MCP equivalentes a:
- `resources/list`: devolver URIs de archivos bajo `MCP_FS_ALLOWED_PATH`.
- `resources/read`: devolver texto del archivo solicitado.
- `resources/delete`: borrar archivo dentro del path permitido (solo cuando llegue con token de aprobación HITL válido).
- `resources/write`: crear archivo nuevo (para almacenar chistes aprobados) evitando overwrite.
- `resources/append`: añadir información (por ejemplo, anotaciones de calidad) a un archivo existente.

Estructura propuesta del proyecto:

```
Bri-Agent/
  Backend/
  Frontend/
MCP/
  dotnet-filesystem-server/
    BriAgent.McpServer.csproj
    Program.cs
    appsettings.json (MCP_FS_ALLOWED_PATH)
    README.md
```

Ejemplo mínimo (pseudo-C#) de `Program.cs` con stdio y JSON-RPC MCP:

```csharp
using McpDotNet.Server; // asumido
using McpDotNet.Protocol;
using System.Text.Json;

var root = Environment.GetEnvironmentVariable("MCP_FS_ALLOWED_PATH")
          ?? Directory.GetCurrentDirectory();

bool InRoot(string p)
{
    var rp = Path.GetFullPath(p);
    var rr = Path.GetFullPath(root);
    return rp == rr || rp.StartsWith(rr + Path.DirectorySeparatorChar);
}

var server = new McpServer(name: "filesystem-mcp", version: "1.0.0");

server.On("resources/list", async ctx =>
{
    var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(InRoot)
        .Select(f => new { uri = new Uri(f).AbsoluteUri, name = Path.GetFileName(f), mimeType = "text/plain" });
    await ctx.RespondAsync(files);
});

server.On("resources/read", async ctx =>
{
    var uri = ctx.Params.GetProperty("uri").GetString();
    if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith("file://")) throw new Exception("InvalidUri");
    var path = new Uri(uri).LocalPath;
    if (!InRoot(path)) throw new Exception("AccessDenied");
    var text = await File.ReadAllTextAsync(path);
    await ctx.RespondAsync(new { text });
});

server.On("resources/delete", async ctx =>
{
    var uri = ctx.Params.GetProperty("uri").GetString();
    var approval = ctx.Params.GetProperty("approvalToken").GetString();
    if (approval != await ApprovalTokenStore.ValidateAsync(approval)) throw new Exception("ApprovalRequired");
    var path = new Uri(uri).LocalPath;
    if (!InRoot(path)) throw new Exception("AccessDenied");
    File.Delete(path);
    await ctx.RespondAsync(new { ok = true });
});

await server.ConnectAsync(new StdioTransport());
```

Notas:
- La librería `McpDotNet.Server` es una asunción; si no está disponible, usamos una capa JSON-RPC mínima (por ejemplo, StreamJsonRpc) con el contrato MCP.
- `resources/delete` solo aceptará operaciones con `approvalToken` emitido por el Backend HITL (ver sección HITL).
- Se recomienda compilar este proyecto y ejecutarlo como proceso hijo desde el servicio cliente en el Backend.
 - Para el caso de chistes se usarán también `resources/write` y `resources/append`.

---

## Cliente MCP en .NET (servicio)

Paquete NuGet esperado: `McpDotNet.Client` (o equivalente oficial cuando esté disponible).

Contrato mínimo del servicio:
- Conectar: inicia proceso `node MCP/filesystem-server/server.js` (stdio) o se conecta a WS.
- Listar: devuelve lista de `{ uri, name, mimeType }`.
- Leer: devuelve `{ text }`.
- Reuso de conexión: singleton/transient con ciclo de vida controlado por DI.

Ejemplo base (según idea proporcionada):

```csharp
using McpDotNet.Client;
using McpDotNet.Protocol;
using McpDotNet.Protocol.Resources;

public class McpFileSystemService
{
    private McpClient? _client;

    public async Task EnsureConnectedAsync()
    {
        if (_client != null) return;
        _client = new McpClient("filesystem-client");
        // stdio: arranca el server Node
        await _client.ConnectAsync(new StdioTransport("node", "MCP/filesystem-server/server.js"));
    }

    public async Task<IReadOnlyList<ResourceDescription>> ListAsync()
    {
        await EnsureConnectedAsync();
        return await _client!.Resources.ListAsync();
    }

    public async Task<string> ReadTextAsync(string uri)
    {
        await EnsureConnectedAsync();
        var content = await _client!.Resources.ReadAsync(uri);
        return content.Text ?? string.Empty;
    }
}
```

Reintentos y ciclo de vida:
- Configurar `IHostedService` o `Lazy<...>` para que la conexión se inicie on-demand y se recupere si el proceso Node se cae.
- Política de reintento (por ejemplo, Polly): backoff exponencial con tope (3 intentos, 250/500/1000 ms).
- Cancelación con `CancellationToken` propagado desde los Controllers.

Interfaces auxiliares:
```csharp
public interface IFileSummarizer
{
  Task<string> SummarizeAsync(string text, string? prompt = null, CancellationToken ct = default);
}
```
Implementación: reutilizar el cliente/servicios LLM del proyecto (OpenAI/Azure OpenAI/Ollama) para hacer un resumen dirigido por `prompt` (con prompt por defecto si es null).

Notas:
- Ajustar la ruta al `server.js` según la ubicación real.
- Si se prefiere WS, reemplazar el transporte por WebSocket.

---

## Controller REST

Archivo: `Bri-Agent/Backend/Controllers/McpController.cs`

Endpoints propuestos:
- GET `/api/mcp/resources`
  - Respuesta: `[{ uri: string, name: string, mimeType?: string }]`
- GET `/api/mcp/read`
  - Query: `uri: string`
  - Respuesta: `{ uri: string, text: string }`
- POST `/api/mcp/summarize`
  - Body: `{ uri: string, prompt?: string }`
  - Respuesta: `{ uri: string, summary: string }`

Contrato de errores: 400 si `uri` inválido; 403 si fuera de ruta; 404 si no existe; 500 en errores internos.

### Contratos detallados

`GET /api/mcp/resources`
Respuesta 200:
```json
[
  { "uri": "file:///Users/demo/readme.txt", "name": "readme.txt", "mimeType": "text/plain" }
]
```

`GET /api/mcp/read?uri=file:///Users/demo/readme.txt`
Respuesta 200:
```json
{ "uri": "file:///Users/demo/readme.txt", "text": "Contenido completo..." }
```

Errores comunes:
```json
{ "error": "InvalidUri", "detail": "Formato de URI no válido" }
{ "error": "AccessDenied", "detail": "Fuera de la ruta permitida" }
{ "error": "NotFound", "detail": "Recurso no encontrado" }
```

`POST /api/mcp/summarize`
Cuerpo ejemplo:
```json
{ "uri": "file:///Users/demo/readme.txt", "prompt": "Resume en español en 3 viñetas" }
```
Respuesta 200:
```json
{ "uri": "file:///Users/demo/readme.txt", "summary": "• Punto 1...\n• Punto 2...\n• Punto 3..." }
```

### Esquemas (C# Records)

```csharp
public record McpResourceDto(string Uri, string Name, string? MimeType);
public record McpReadResponse(string Uri, string Text);
public record McpSummarizeRequest(string Uri, string? Prompt);
public record McpSummarizeResponse(string Uri, string Summary);
public record McpError(string Error, string Detail);
```

### Borrador de Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Net;

[ApiController]
[Route("api/mcp")] // prefijo específico
public class McpController : ControllerBase
{
    private readonly McpFileSystemService _svc;
    private readonly IFileSummarizer _summarizer; // interfaz que usará LLM

    public McpController(McpFileSystemService svc, IFileSummarizer summarizer)
    { _svc = svc; _summarizer = summarizer; }

    [HttpGet("resources")]
    public async Task<ActionResult<IEnumerable<McpResourceDto>>> Resources()
    {
        var list = await _svc.ListAsync();
        return Ok(list.Select(r => new McpResourceDto(r.Uri, r.Name, r.MimeType)));
    }

    [HttpGet("read")]
    public async Task<ActionResult<McpReadResponse>> Read([FromQuery] string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return BadRequest(new McpError("InvalidUri", "URI vacío"));
        var text = await _svc.ReadTextAsync(uri);
        return Ok(new McpReadResponse(uri, text));
    }

    [HttpPost("summarize")]
    public async Task<ActionResult<McpSummarizeResponse>> Summarize([FromBody] McpSummarizeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Uri)) return BadRequest(new McpError("InvalidUri", "URI vacío"));
        var text = await _svc.ReadTextAsync(req.Uri);
        var summary = await _summarizer.SummarizeAsync(text, req.Prompt);
        return Ok(new McpSummarizeResponse(req.Uri, summary));
    }
}
```

`IFileSummarizer` se implementará con el agente/LLM existente para permitir diferentes estilos de resumen.

---

## Herramientas del Agente (Agent Framework)

- ListFilesTool
  - Input: `{ pattern?: string }`
  - Output: lista de URIs y nombres.
- ReadFileTool
  - Input: `{ uri: string }`
  - Output: `{ text: string }`
- SummarizeFileTool
  - Input: `{ uri: string, style?: "short|detailed" }`
  - Output: `{ summary: string }`
  - Implementación: combina ReadFileTool + llamada a LLM (reusar servicio/cliente LLM que ya use el proyecto—Azure OpenAI/OpenAI/Ollama). Aplicar truncado/chunking si el archivo es grande.

Estas herramientas se registran en el agente/demos para que el planificador pueda invocarlas.

### Contratos de herramientas (pseudo)

```csharp
public record ListFilesInput(string? Pattern);
public record ListFilesOutput(IReadOnlyList<McpResourceDto> Resources);

public record ReadFileInput(string Uri);
public record ReadFileOutput(string Text);

public record SummarizeFileInput(string Uri, string? Style);
public record SummarizeFileOutput(string Summary);

// Uso dentro de un AIAgent step/tool
// tool: "list_files" -> llama a _mcp.ListAsync()
// tool: "read_file"  -> llama a _mcp.ReadTextAsync(uri)
// tool: "summarize_file" -> combina read + _summarizer.SummarizeAsync(text, prompt)
```

Buenas prácticas:
- Añadir `maxChars` para truncar entrada al LLM.
- Añadir `pattern` server-side opcional (filtrado simple por sufijo) pero evitando exponer glob complejos.
- Cacheo simple en memoria por 30s de listados si hay alto volumen.

---

## Seguridad y límites

- Whitelist de carpeta en el servidor MCP (no confiar en el cliente).
- Normalización y validación de URIs `file://`.
- Límite de tamaño por archivo (por ejemplo, 512KB para lectura directa; si excede, chunking o rechazar).
- Redacción de PII opcional en resúmenes.
- Logs con trazas MCP (con cuidado de no loggear contenido sensible).

Validaciones concretas:
- Rechazar URIs que contengan `..` después de normalizar.
- Permitir solo `file://` sin query ni fragment.
- Registrar métricas: número de lecturas, tamaño agregado, errores.
- Implementar sanitización de salida para caracteres de control si se usa SSE posteriormente.

Chunking (si se implementa):
```csharp
const int MaxChunk = 8000; // chars
var text = await ReadTextAsync(uri);
var chunks = Enumerable.Range(0, (text.Length + MaxChunk - 1) / MaxChunk)
  .Select(i => text.Substring(i * MaxChunk, Math.Min(MaxChunk, text.Length - i * MaxChunk))); 
```

PII (opcional): aplicar regex simple de correos / teléfonos y reemplazar por `[REDACTED]` antes de enviar al LLM.

---

## Pruebas (mínimas)

- Unitarias del servicio:
  - ListAsync devuelve lista no vacía cuando hay archivos.
  - ReadTextAsync lanza/retorna error controlado si `uri` fuera de ROOT.
- Integración del Controller:
  - GET /api/mcp/resources -> 200 y formato esperado.
  - POST /api/mcp/summarize -> 200 con `summary` no vacío para un archivo de muestra.
- Casos de borde:
  - Archivo vacío.
  - Archivo muy grande (esperar 413/422 según decisión).
  - URI fuera de whitelist.
  - Reintentos al caerse el proceso Node.

Ejemplo de test de servicio (pseudo):
```csharp
[Fact]
public async Task ReadTextAsync_InvalidUri_Throws()
{
    var svc = CreateService();
    await Assert.ThrowsAsync<McpAccessDeniedException>(() => svc.ReadTextAsync("file:///etc/passwd"));
}
```

---

## Pasos de implementación

1) Servidor MCP (.NET)
- Crear carpeta `MCP/dotnet-filesystem-server` con proyecto consola .NET y `Program.cs` como arriba.
- Exponer handlers `resources/list`, `resources/read`, `resources/delete`.
- Leer `MCP_FS_ALLOWED_PATH` desde entorno o appsettings.

2) Backend .NET
- Añadir paquete NuGet del cliente MCP (por confirmar nombre exacto si cambia).
- Crear servicio `McpFileSystemService` y registrarlo en DI.
- Crear `McpController` y mapear endpoints.

3) Herramientas Agent Framework
- Implementar `ListFilesTool`, `ReadFileTool`, `SummarizeFileTool` y registrarlas.

4) Pruebas
- Añadir tests en `Bri-Agent/Backend.Tests` para servicio y controller.

5) Frontend (opcional)
- Botones para listar -> seleccionar -> leer -> resumir.

---

## Operación local (macOS)

```bash
# 1) Server MCP (.NET)
export MCP_FS_ALLOWED_PATH="/Users/<usuario>/Documents/demo"
dotnet run --project MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj

# 2) Backend
# (en la raíz del repo o en Bri-Agent/Backend)
dotnet build
# dotnet run --project Bri-Agent/Backend/BriAgent.Backend.csproj

# 3) Frontend (opcional)
cd Bri-Agent/Frontend
npm i
npm run dev
```

---

## Riesgos y mitigaciones

- Disponibilidad del paquete .NET MCP: si no hay paquete estable, usar JSON-RPC (StreamJsonRpc) y mapear métodos MCP. Mantener capa de abstracción en `McpFileSystemService`.
- Tamaño de archivos: aplicar límites y chunking.
- Seguridad: validar URIs y no permitir escalado de ruta.

---

## HITL: Intervención Humana en el Bucle + Checkpointing

Caso gracioso y claro: “Agente Archivista” y “Agente Limpiador” colaboran para mantener limpio el directorio demo. El Archivista detecta archivos grandes/duplicados y propone acciones; el Limpiador ejecuta acciones seguras automáticamente pero, para acciones destructivas (borrar), requiere aprobación humana.

Flujo resumido:
1) Archivista lista archivos (MCP list) y calcula candidatos a borrar por tamaño/antigüedad.
2) Propone plan al Limpiador y al usuario (SSE al Frontend) con detalle y preview de contenidos (MCP read + resumen).
3) Backend crea un Checkpoint de la conversación y emite un `approvalToken` asociado a la acción (borrado de N archivos, rutas exactas, hash de verificación).
4) Frontend muestra dos botones gigantes: “Aprobar” (verde) y “Rechazar” (rojo).
5) Si el usuario aprueba, el Backend reanuda el workflow con el token; Limpiador llama a `resources/delete` del MCP .NET pasando el token. Si rechaza, el workflow se cierra y se registra la decisión.

Endpoints Backend HITL (propuestos):
- POST `/bri-agent/hitl/start` Body: `{ action: "delete", items: ["file://...", ...], reason?: string }` -> Respuesta: `{ approvalId, approvalToken, expiresAt }` (solo token almacenable server-side; al cliente se da `approvalId`).
- POST `/bri-agent/hitl/approve` Body: `{ approvalId }` -> Respuesta: `{ ok: true }` (marca token como activable para una única operación en los próximos X minutos).
- POST `/bri-agent/hitl/reject` Body: `{ approvalId, reason?: string }` -> Respuesta `{ ok: true }`.
- GET `/bri-agent/hitl/status/{approvalId}` -> `{ status: "pending|approved|rejected", updatedAt }`.

SSE (ya disponible en proyecto):
- Canal “workflow_started”, “step_started”, “step_completed”, “completed”.
- Añadir evento “approval_required” con payload: `{ approvalId, items, summary, suggestedAction }` para que el Frontend active los botones.

Checkpointing:
- Crear `CheckpointStore` (en memoria inicialmente, interface para persistir). Guarda: `workflowId`, `step`, `context`, `approvalId`, `createdAt`.
- Al solicitar aprobación, se crea un checkpoint antes del borrado con el plan (lista de URIs, tamaños, hashes) para reanudar o auditar.
- Tras aprobación, se reanuda desde `step = ExecuteDeletion` validando hash/lista.

Cambios en el servidor MCP (.NET):
- `resources/delete` requiere `approvalToken` válido; valida que los URIs coinciden con el plan almacenado.
- Devuelve `{ deleted: n }` y errores detallados por archivo si algunos fallan.

Frontend (vitest/vite):
- Nueva vista “Limpieza segura” con:
  - Lista de candidatos con checkbox.
  - Botón “Solicitar aprobación” -> crea `approvalId` y muestra botones gigantes “Aprobar” (verde) / “Rechazar” (rojo).
  - SSE para actualizar estado y mostrar resultados.

Pruebas HITL:
- Unitarias: creación/expiración de approvalToken; validación al invocar `resources/delete`.
- Integración: flujo completo hasta borrar un archivo de prueba bajo directorio temporal.

Contratos de datos (C#):
```csharp
public record ApprovalStartRequest(string Action, string[] Items, string? Reason);
public record ApprovalStartResponse(string ApprovalId, DateTimeOffset ExpiresAt);
public record ApprovalStatus(string ApprovalId, string Status, DateTimeOffset UpdatedAt);
```

Errores/Edge cases:
- Aprobación expirada -> 409/410 y reintento.
- Archivo desapareció entre aprobación y ejecución -> reportar parcial y seguir.
- Usuario rechaza -> workflow finaliza sin cambios.

## Siguientes pasos

- Confirmar/instalar paquete MCP .NET definitivo.
- Crear carpeta `MCP/filesystem-server` y validar server básico.
- Implementar servicio y controller.
- Añadir herramienta de resumen conectada al LLM ya configurado en el repo.

---

## Caso de uso ampliado: Fábrica de Chistes Multi-Agente con HITL

Objetivo: Demostrar coordinación de 4 agentes, intervención humana (botones verde/rojo), checkpointing y uso del MCP para persistencia y auditoría.

Agentes:
1. ContadorDeChistesAgent (Generador): produce hasta 10 chistes cortos secuencialmente.
2. ValidadorDeChistesAgent (Validador): evalúa cada chiste con score 0..10 y razones (estructura, remate, creatividad, no ofensivo).
3. JefazoAgent (Boss): si score < 9 considera que el chiste no está “bonísimo” y lanza evento de aprobación humana antes de eliminar o desechar. Si score >= 9 pasa directo a guardado.
4. ArchivistaAgent (Archivista): guarda, lista y resume chistes aprobados usando MCP (`resources/write`, `resources/list`, `resources/read`).

Directorio de chistes:
- Subcarpeta `jokes/` dentro de `MCP_FS_ALLOWED_PATH`.
- Nombre de archivo: `joke-<n>-<yyyyMMdd-HHmmssfff>.txt` donde `<n>` es índice secuencial.
- Contenido ejemplo:
  ```
  timestamp=2025-11-12T10:35:22.123Z|score=9|revisor=boss-approved
  ¿Por qué el bit se deprimió? Porque estaba sin par… (badum tss)
  ```

Workflow por chiste (estado):
`Generated -> Scored -> (BossPass | BossNeedsHuman) -> (HumanApprove | HumanReject) -> (Stored | Deleted)`

Eventos SSE nuevos (además de los ya definidos):
- `joke_generated` { workflowId, jokeId, text }
- `joke_scored` { workflowId, jokeId, score, reasons }
- `boss_review` { workflowId, jokeId, approvedDirect: bool }
- `approval_required` { workflowId, jokeId, approvalId, score, preview }
- `joke_saved` { workflowId, jokeId, uri }
- `joke_deleted` { workflowId, jokeId }
- `factory_progress` { workflowId, generated, saved, deleted, pendingApproval }
- `factory_completed` { workflowId, total, saved, deleted }

Endpoints Backend propuestos (prefijo `api/jokes`):
- POST `/api/jokes/start` Body: `{ total?: 10, topic?: string }` -> `{ workflowId }` (inicia pipeline y comienza a emitir SSE).
- GET `/api/jokes/status/{workflowId}` -> estado actual y métricas.
- POST `/api/jokes/hitl/approve` Body: `{ approvalId }` -> `{ ok: true }` (reanuda workflow y almacena chiste).
- POST `/api/jokes/hitl/reject` Body: `{ approvalId }` -> `{ ok: true }` (marca chiste para descarte; no se escribe archivo).
- GET `/api/jokes/list` -> lista archivos en `jokes/`.
- GET `/api/jokes/summary` Query: `limit?=N` -> resumen LLM de últimos N chistes.

Herramientas (Agent Framework):
- `generate_joke` -> Input `{ topic?: string }` Output `{ text }`
- `validate_joke` -> Input `{ text }` Output `{ score:int, reasons:string[] }`
- `boss_review` -> Input `{ text, score }` Output `{ approved:bool, reason }`
- `store_joke` -> Input `{ text, score, jokeId }` Output `{ uri }`
- `list_jokes` -> Output `{ jokes: Resource[] }`
- `summarize_jokes` -> Input `{ limit?:int }` Output `{ summary }`

Lógica de puntuación (heurística simple):
- Longitud aceptable (entre 20 y 140 caracteres) -> +2 puntos.
- Presencia de juego de palabras detectado (regex simple: "(bit|byte|array|null|stack|cache)") -> +3 puntos.
- Remate con onomatopeya ("tss" o "ba dum") -> +1 punto.
- No contiene palabras ofensivas (lista vetada) -> +2 puntos.
- Creatividad subjetiva (LLM classification) -> +0..2 puntos.

Checkpointing específico:
- Guardar tras cada `joke_scored`: `{ jokeId, text, score }`.
- En `approval_required`: añadir `approvalId` y tiempo de solicitud.
- Al aprobar: registrar `approvedAt` y continuar; al rechazar marcar `rejectedAt`.

HITL UX (Frontend):
- Vista “Fábrica de Chistes” con:
  - Botón principal “Empezar a fabricar chistes”.
  - Lista/timeline reactiva de chistes (cards) con estado y score.
  - Sección de aprobación activa: cuando hay un chiste pendiente se muestran dos botones enormes:
    - Verde: “Guardar chiste (score=7) — me hizo sonreír”.
    - Rojo: “Eliminar chiste — no da risa”.
  - Panel lateral: métricas (generados, guardados, eliminados, pendientes) + lista de archivos guardados + resumen dinámico.
  - Auto-scroll al último evento.

Persistencia adicional (opcional):
- Registrar historial en un `factory-log.txt` usando `resources/append` para auditoría.

Edge cases relevantes:
- Score extremo (10) -> guardado directo, sin HITL.
- Score muy bajo (<4) -> `boss_review` podría auto-rechazar y saltar a eliminación sin intervención (configurable si queremos forzar intervención al menos una vez).
- Falta de conexión MCP -> workflow se pausa y emite evento `error` hasta reconectar.
- Límite alcanzado (10 chistes) -> emite `factory_completed` y cierra.

Pruebas del flujo de chistes (resumen):
- Generar 3 chistes con scores variados y verificar almacenamiento de los >=9.
- Simular aprobación humana para un score 7 y ver archivo creado.
- Rechazar otro score 6 y comprobar ausencia de archivo.
- Solicitar resumen y verificar que incluye líneas de los chistes aprobados.

Seguridad / Contenido:
- Filtro básico para palabras insultantes antes de guardar.
- Evitar inyección de rutas: sólo escribir dentro de `jokes/`.
- Limitar longitud máxima del chiste a 300 caracteres (truncar si excede).

Observabilidad:
- Contador Prometheus (opcional) o métricas internas: `jokes_generated_total`, `jokes_saved_total`, `jokes_deleted_total`, `hitl_requests_total`.
- Trazas por agente: actividad `joke.generate`, `joke.validate`, `joke.boss`, `joke.store`.

Resumen de valor demo:
- Muestra coordinación multi-agente.
- Ejemplifica HITL y checkpointing claramente visibles.
- Usa MCP para persistencia transparente y auditable.
- Fácil de extender a otros dominios (p.ej. revisión de commits, clasificación de documentos).
