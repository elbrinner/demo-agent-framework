using BriAgent.Backend.Services;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Collections.Concurrent;
using System.Text;

namespace BriAgent.Backend.Services;

public record JokeWorkflowState(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals);
public record JokeItem(string Id, string Text, int? Score = null, string? Uri = null, string? ApprovalId = null);

/// <summary>
/// Gestiona el workflow secuencial de generación/validación/jefazo/aprobación/archivo.
/// </summary>
public class JokesWorkflowService
{
    private readonly McpFileSystemService _mcp;
    private readonly JokesIndexService? _index;
    private readonly JokesCheckpointService? _checkpoint;
    private readonly JokesModerationService? _moderation;
    private readonly JokesEventBus _bus;
    private readonly ConcurrentDictionary<string, JokeWorkflowState> _states = new();
    private readonly ConcurrentDictionary<string, List<JokeItem>> _items = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _seenTexts = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _ctsByWorkflow = new();
    private readonly Random _rnd = new();
    private readonly AIFunction[] _tools;
    private readonly bool _useAgents;
    private readonly AIAgent? _generatorAgent;
    private readonly AIAgent? _reviewerAgent;
    private readonly AIAgent? _bossAgent;
    private readonly IAgentFactory? _factory;
    private readonly AgentRunner _runner;
    private readonly string? _model;
    private readonly TimeSpan _agentTimeout;
    private readonly bool _requireLlm;

    public JokesWorkflowService(McpFileSystemService mcp, JokesEventBus bus, AIFunction[] jokeTools, AgentRunner runner, IAgentFactory? factory = null, JokesIndexService? index = null, JokesCheckpointService? checkpoint = null, JokesModerationService? moderation = null)
    {
        _mcp = mcp;
        _bus = bus;
        _tools = jokeTools;
        _runner = runner;
        _factory = factory;
        _index = index;
        _checkpoint = checkpoint;
        _moderation = moderation;
    // Timeout por paso de agente configurable (segundos); por defecto 30s para dar margen al LLM
    if (!int.TryParse(Environment.GetEnvironmentVariable("AGENT_STEP_TIMEOUT_SECONDS"), out var to) || to <= 0) to = 30;
    _agentTimeout = TimeSpan.FromSeconds(to);
    // Por defecto usamos LLM para generar (a menos que USE_AGENT_JOKES explicitamente sea "false")
    var useEnv = Environment.GetEnvironmentVariable("USE_AGENT_JOKES");
    _useAgents = !string.Equals(useEnv, "false", StringComparison.OrdinalIgnoreCase);
    // Si REQUIRE_LLM_JOKES=true, no usaremos stub: fallaremos el intento y registraremos error
    _requireLlm = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_LLM_JOKES"), "true", StringComparison.OrdinalIgnoreCase);
        if (_useAgents)
        {
            _model = BriAgent.Backend.Config.Credentials.Model;
            if (_factory is not null)
            {
                _generatorAgent = _factory.CreateJokesGenerator();
                _reviewerAgent = _factory.CreateJokesReviewer();
                _bossAgent = _factory.CreateJokesBoss();
            }
            else
            {
                // Generador sin tools: usar LLM directamente para crear chistes originales en texto plano
                _generatorAgent = AgentFactory.CreateBasicAgent(
                    instructions: "Eres el Agente Generador de chistes. Crea chistes breves y originales en texto plano. No uses JSON ni herramientas.");
                _reviewerAgent = AgentFactory.CreateBasicAgent(
                    instructions: "Eres el Agente Revisor. Evalúa chistes. Usa validate_joke para obtener un score 0..10 y explica de forma breve.",
                    tools: _tools);
                _bossAgent = AgentFactory.CreateBasicAgent(
                    instructions: "Eres el Jefazo. Decide si requiere aprobación humana (HITL). Usa boss_review. Si requiere HITL, indícalo claramente.",
                    tools: _tools);
            }
        }
    }

    public JokeWorkflowState Start(int total, bool ensureHitl = false)
    {
        var wfId = Guid.NewGuid().ToString("N");
        var state = new JokeWorkflowState(wfId, total, 0, 0, 0, 0);
        _states[wfId] = state;
        _items[wfId] = new();
        _bus.Publish(wfId, new JokeWorkflowEvent(wfId, "workflow_started", null, new { total }));
        Console.WriteLine($"[JOKES] START workflowId={wfId} total={total} useAgents={_useAgents}");
        var preload = LoadExistingJokesNormalizedAsync();
        var cts = new CancellationTokenSource();
        _ctsByWorkflow[wfId] = cts;
        _ = Task.Run(() => RunAsync(state, preload, ensureHitl, cts.Token));
        return state;
    }

    public JokeWorkflowState? GetState(string workflowId)
        => _states.TryGetValue(workflowId, out var s) ? s : null;

    public IReadOnlyList<JokeItem> GetItems(string workflowId)
        => _items.TryGetValue(workflowId, out var list) ? list : Array.Empty<JokeItem>();

    private async Task RunAsync(JokeWorkflowState initial, Task<HashSet<string>> preloadTask, bool ensureHitl, CancellationToken ct)
    {
        try
        {
            // Construir conjunto de textos ya vistos (incluye existentes en disco)
            var preexisting = await preloadTask.ConfigureAwait(false);
            var preSeen = new HashSet<string>(preexisting);
            // si existe índice, úsalo para poblar rápidamente la memoria de duplicados
            if (_index is not null)
            {
                try
                {
                    var idx = await _index.ReadIndexAsync(ct);
                    foreach (var e in idx)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Normalized)) preSeen.Add(e.Normalized);
                    }
                }
                catch { }
            }
            _seenTexts[initial.WorkflowId] = preSeen;
            var forcedHitlUsed = false;
            int i = 0;
            int maxAttempts = Math.Max(50, initial.TargetTotal * 10); // cota para evitar bucles infinitos si todo se rechaza
            while (!ct.IsCancellationRequested)
            {
                // detener cuando hay suficientes guardados
                if (_states.TryGetValue(initial.WorkflowId, out var st) && st.Saved >= initial.TargetTotal) break;
                if (i >= maxAttempts) break; // seguridad
                if (ct.IsCancellationRequested) break;
                // Contador de intento/chiste para IDs únicos en UI
                i++;
                var seq = i;
                // 1) Generar chiste con LLM (Agente o tool). Guardar SOLO el texto plano del chiste.
                string? text = null;
                // Usar temas humanos de programación (evitar IDs artificiales tipo "tema-xxxx")
                var topics = new[] { "Git", "compilación", "hilos", "memoria", "APIs REST", "regex", "Docker", "bases de datos", "JavaScript", "Python", "C#", "null", "excepciones", "tipos", "recursión", "latencia", "azure", "windows" };
                var topic = topics[_rnd.Next(topics.Length)];
                if (_useAgents && _generatorAgent is not null)
                {
                    try
                    {
                        var prompt = $"Genera un chiste MUY breve y original de programación inspirado en el tema '{topic}'. No menciones literalmente el tema, IDs ni hashes; no uses comillas ni JSON; devuelve solo el chiste en texto plano.";
                        Console.WriteLine($"[GEN] prompt topic={topic} wf={initial.WorkflowId}");
                        var maybeText = await RunAgentWithTimeoutAsync(_generatorAgent, prompt, "jokes.generator", ct);
                        if (!string.IsNullOrWhiteSpace(maybeText)) text = maybeText!.Trim();
                    }
                    catch
                    {
                        // mantener fallback
                    }
                }
                else
                {
                    var genFunc = _tools.FirstOrDefault(t => t.Name.Equals("generate_joke", StringComparison.OrdinalIgnoreCase));
                    if (genFunc is not null)
                    {
                        try
                        {
                            var genArgs = new AIFunctionArguments();
                            genArgs.Add("topic", topic);
                            var result = await genFunc.InvokeAsync(genArgs);
                            if (result is IDictionary<string, object?> gdict && gdict.TryGetValue("text", out var tObj) && tObj is string tStr)
                                text = tStr.Trim();
                            else if (result is not null)
                            {
                                var str = result.ToString();
                                if (!string.IsNullOrWhiteSpace(str)) text = str!.Trim();
                            }
                        }
                        catch { }
                    }
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    // No generamos stub: registrar error y continuar con el siguiente intento
                    var jokeIdFail = $"{initial.WorkflowId}-{seq}";
                    Console.WriteLine($"[GEN][ERROR] LLM no devolvió texto (timeout/error). wf={initial.WorkflowId} id={jokeIdFail} model={_model ?? "(null)"}");
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeIdFail, new { agent = "generator", action = "error", reason = "llm_unavailable", message = "Generador sin respuesta del LLM" }));
                    Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeIdFail, new { reason = "llm_unavailable" }));
                    continue; // siguiente intento
                }
                // Limpieza: si accidentalmente vino JSON con un campo "text", extraerlo
                try
                {
                    var trimmed = text.Trim();
                    if (trimmed.StartsWith("{") && trimmed.Contains("\"text\""))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                        if (doc.RootElement.TryGetProperty("text", out var tEl) && tEl.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var inner = tEl.GetString();
                            if (!string.IsNullOrWhiteSpace(inner)) text = inner.Trim();
                        }
                    }
                }
                catch { /* ignorar parse errors */ }
                var jokeId = $"{initial.WorkflowId}-{seq}";
                // Dedupe temprano ANTES de publicar 'joke_generated'
                var normalized = text.Trim().ToLowerInvariant();
                var seen = _seenTexts.GetOrAdd(initial.WorkflowId, _ => new HashSet<string>());
                if (!seen.Add(normalized))
                {
                    Console.WriteLine($"[DEDUP] duplicate wf={initial.WorkflowId} id={jokeId} key='{(normalized.Length>80?normalized[..80]+"…":normalized)}'");
                    Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeId, new { reason = "duplicate" }));
                    continue; // ir al siguiente chiste sin validar/almacenar
                }
                var item = new JokeItem(jokeId, text);
                _items[initial.WorkflowId].Add(item);
                Update(initial.WorkflowId, s => s with { Generated = s.Generated + 1 });
                _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_generated", jokeId, new { text }));
                _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "generator", action = "generated", message = "Generador creó un chiste" }));

                // 2) Validar con Agente Revisor o tool directa
                int score = 0;
                string? reviewerNotes = null;
                if (_useAgents && _reviewerAgent is not null)
                {
                    try
                    {
                        var txt = await RunAgentWithTimeoutAsync(_reviewerAgent,
                            $"Evalúa el siguiente chiste y devuelve JSON EXACTO: {{\\\"score\\\": <0..10>, \\\"rationale\\\": \\\"frase corta\\\"}}. Chiste: '{text}'",
                            "jokes.reviewer", ct);
                        // Intentar parsear JSON: { score, rationale }
                        bool parsed = false;
                        if (!string.IsNullOrWhiteSpace(txt))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(txt);
                                if (doc.RootElement.TryGetProperty("score", out var scEl))
                                {
                                    var sc = scEl.ValueKind == System.Text.Json.JsonValueKind.Number ? scEl.GetInt32() : int.Parse(scEl.GetString() ?? "0");
                                    score = Math.Clamp(sc, 0, 10);
                                    if (doc.RootElement.TryGetProperty("rationale", out var rEl) && rEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        reviewerNotes = (rEl.GetString() ?? string.Empty).Trim();
                                    }
                                    parsed = true;
                                }
                            }
                            catch { }
                        }
                        if (!parsed)
                        {
                            var digits = new string((txt ?? "").Where(char.IsDigit).ToArray());
                            score = int.TryParse(digits, out var sc) ? Math.Clamp(sc, 0, 10) : _rnd.Next(5, 11);
                        }
                        Console.WriteLine($"[REV] score={score} wf={initial.WorkflowId} id={jokeId}");
                    }
                    catch { score = _rnd.Next(5, 11); }
                }
                else
                {
                    var valFunc = _tools.FirstOrDefault(t => t.Name.Equals("validate_joke", StringComparison.OrdinalIgnoreCase));
                    if (valFunc is not null)
                    {
                        try
                        {
                            var args = new AIFunctionArguments();
                            args.Add("text", text);
                            var result = await valFunc.InvokeAsync(args);
                            if (result is IDictionary<string, object?> dict && dict.TryGetValue("score", out var scObj) && scObj is int sc)
                                score = sc;
                            else if (result is not null && int.TryParse(result.ToString(), out var scParsed))
                                score = scParsed;
                            else
                                score = _rnd.Next(5, 11);
                        }
                        catch { score = _rnd.Next(5, 11); }
                    }
                    else
                    {
                        score = _rnd.Next(5, 11);
                    }
                }
                SetItem(initial.WorkflowId, jokeId, ji => ji with { Score = score });
                _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_scored", jokeId, new { score, rationale = reviewerNotes }));
                _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "reviewer", action = "scored", message = $"Revisor asignó score {score}" , notes = reviewerNotes }));

                // Revisión del revisor: rechazar directamente si score <= 7 (malo o mediocre).
                // Ajuste para tratar 7 como insuficiente según nueva política.
                if (score <= 7)
                {
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "reviewer", action = "reject", reason = "malo", message = "Rechazado por el revisor: score <= 7 considerado malo", notes = reviewerNotes }));
                    Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeId, new { reason = "malo", score, reviewerNotes }));
                    continue; // no pasa al jefe ni a almacenamiento
                }

                // 3) Boss review: solo requiere aprobación si score >= 9 (muy bueno) -> HITL humano confirma guardado premium
                var needsHitl = score >= 9;
                string? bossNotes = null;
                if (_useAgents && _bossAgent is not null)
                {
                    try
                    {
                        var txt = await RunAgentWithTimeoutAsync(_bossAgent,
                            $@"Decide sobre este chiste. Entrada: score={score}, rationale='{(reviewerNotes ?? string.Empty)}', texto='{text}'. Devuelve JSON EXACTO: {{""decision"": ""hitl""|""auto"", ""notes"": ""frase corta""}}.",
                            "jokes.boss", ct);
                        bool parsed = false;
                        if (!string.IsNullOrWhiteSpace(txt))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(txt);
                                if (doc.RootElement.TryGetProperty("decision", out var dEl) && dEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var d = (dEl.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                                    if (d == "hitl") needsHitl = true; else if (d == "auto") needsHitl = false;
                                    if (doc.RootElement.TryGetProperty("notes", out var nEl) && nEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        bossNotes = (nEl.GetString() ?? string.Empty).Trim();
                                    }
                                    parsed = true;
                                }
                            }
                            catch { }
                        }
                        if (!parsed)
                        {
                            var low = (txt ?? string.Empty).ToLowerInvariant();
                            if (low.Contains("hitl")) needsHitl = true; else if (low.Contains("auto")) needsHitl = false;
                        }
                    }
                    catch { /* fallback por score */ }
                }
                else
                {
                    var bossFunc = _tools.FirstOrDefault(t => t.Name.Equals("boss_review", StringComparison.OrdinalIgnoreCase));
                    if (bossFunc is not null)
                    {
                        try
                        {
                            var bargs = new AIFunctionArguments();
                            bargs.Add("score", score);
                            var bossRes = await bossFunc.InvokeAsync(bargs);
                            if (bossRes is IDictionary<string, object?> bossDict && bossDict.TryGetValue("decision", out var decObj) && decObj is string decision)
                            {
                                needsHitl = decision.Equals("hitl", StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        catch { }
                    }
                }
                Console.WriteLine($"[BOSS] needsHitl={(needsHitl?1:0)} wf={initial.WorkflowId} id={jokeId} score={score}");
                // Forzar al menos un HITL si se solicita para pruebas (solo una vez)
                if (ensureHitl && !forcedHitlUsed)
                {
                    if (score < 9)
                    {
                        score = 9;
                        SetItem(initial.WorkflowId, jokeId, ji => ji with { Score = score });
                    }
                    needsHitl = true;
                    forcedHitlUsed = true;
                }
                if (needsHitl)
                {
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "boss", action = "request_hitl", message = "Jefazo solicita aprobación humana" }));
                    var approval = ApprovalStore.Create();
                    SetItem(initial.WorkflowId, jokeId, ji => ji with { ApprovalId = approval.ApprovalId });
                    Update(initial.WorkflowId, s => s with { PendingApprovals = s.PendingApprovals + 1 });
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "approval_required", jokeId, new { approvalId = approval.ApprovalId, score, reviewerNotes, bossNotes }));
                    // Checkpointing: pausa del workflow hasta respuesta humana
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "checkpoint_paused", jokeId, new { approvalId = approval.ApprovalId }));
                    // Persistir snapshot inicial (pending)
                    if (_checkpoint is not null)
                    {
                        try
                        {
                            await _checkpoint.SaveAsync(new JokeCheckpointSnapshot(
                                ApprovalId: approval.ApprovalId,
                                WorkflowId: initial.WorkflowId,
                                JokeId: jokeId,
                                Score: score,
                                Text: item.Text,
                                Status: "pending",
                                CreatedAt: DateTimeOffset.UtcNow,
                                UpdatedAt: DateTimeOffset.UtcNow
                            ));
                        }
                        catch { }
                    }
                    // Esperar aprobación (simulado HITL)
                    try
                    {
                        // TTL configurable (segundos), por defecto 300s
                        var ttlSeconds = 0;
                        int.TryParse(Environment.GetEnvironmentVariable("APPROVAL_TTL_SECONDS"), out ttlSeconds);
                        ttlSeconds = ttlSeconds <= 0 ? 300 : ttlSeconds;
                        using var ttlCts = new CancellationTokenSource(TimeSpan.FromSeconds(ttlSeconds));
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ttlCts.Token, ct);

                        ApprovalStatus status;
                        try
                        {
                            status = await ApprovalStore.WaitAsync(approval.ApprovalId, linked.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            // TTL expirado: tratar como rechazo por expiración
                            status = ApprovalStatus.Rejected;
                            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "approval_expired", jokeId, new { approvalId = approval.ApprovalId }));
                        }
                        _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "checkpoint_resumed", jokeId, new { approvalId = approval.ApprovalId, status = status.ToString() }));
                        if (status == ApprovalStatus.Approved)
                        {
                            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "human", action = "approve", message = "Humano aprobó el chiste" }));
                            // Moderación posterior a HITL: bloquear si incumple políticas antes de persistir
                            if (_moderation is not null)
                            {
                                try
                                {
                                    var mod = await _moderation.EvaluateAsync(item.Text, ct);
                                    if (!mod.Allowed)
                                    {
                                        _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "moderation_blocked", jokeId, new { reason = mod.Reason, category = mod.Category }));
                                        // Limpiar approvalId y marcar como eliminado (no persistimos)
                                        SetItem(initial.WorkflowId, jokeId, ji => ji with { ApprovalId = null });
                                        Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                                        if (_checkpoint is not null)
                                        {
                                            try { await _checkpoint.UpdateStatusAsync(approval.ApprovalId, "rejected"); } catch { }
                                        }
                                        _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeId, new { reason = "moderation", score }));
                                        continue; // pasar al siguiente chiste sin guardar
                                    }
                                }
                                catch { }
                            }
                            await PersistJokeAsync(initial.WorkflowId, jokeId, score);
                            Console.WriteLine($"[STORE] saved wf={initial.WorkflowId} id={jokeId}");
                            // limpiar approvalId tras completar
                            SetItem(initial.WorkflowId, jokeId, ji => ji with { ApprovalId = null });
                            // snapshot
                            if (_checkpoint is not null)
                            {
                                try { await _checkpoint.UpdateStatusAsync(approval.ApprovalId, "approved"); } catch { }
                            }
                            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_stored", jokeId, new { score }));
                        }
                        else
                        {
                            // Rechazo humano sin motivo adicional (no requiere justificación)
                            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "human", action = "reject", message = "Humano rechazó el chiste" }));
                            // Borrado lógico (no persistimos)
                            // Importante: limpiar ApprovalId para que el snapshot de estado NO se muestre como 'waiting'.
                            SetItem(initial.WorkflowId, jokeId, ji => ji with { ApprovalId = null });
                            Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                            if (_checkpoint is not null)
                            {
                                try { await _checkpoint.UpdateStatusAsync(approval.ApprovalId, "rejected"); } catch { }
                            }
                            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeId, new { reason = "rejected", score }));
                        }
                    }
                    finally
                    {
                        Update(initial.WorkflowId, s => s with { PendingApprovals = s.PendingApprovals - 1 });
                    }
                }
                else
                {
                    // Nueva regla: si el Jefe no lo considera bonísimo, lo rechaza directamente (no se guarda)
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "agent_action", jokeId, new { agent = "boss", action = "reject", reason = "no_bonisimo", message = "Jefe rechazó el chiste: no le pareció lo suficientemente bueno", notes = bossNotes }));
                    Update(initial.WorkflowId, s => s with { Deleted = s.Deleted + 1 });
                    _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "joke_rejected", jokeId, new { reason = "no_bonisimo", score, reviewerNotes, bossNotes }));
                    Console.WriteLine($"[BOSS] reject no_bonisimo wf={initial.WorkflowId} id={jokeId} score={score}");
                }
            }
            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "workflow_completed", null, new { total = initial.TargetTotal }));
            Console.WriteLine($"[JOKES] COMPLETED workflowId={initial.WorkflowId} generated={_states[initial.WorkflowId].Generated} saved={_states[initial.WorkflowId].Saved} deleted={_states[initial.WorkflowId].Deleted}");
        }
        catch (Exception ex)
        {
            _bus.Publish(initial.WorkflowId, new JokeWorkflowEvent(initial.WorkflowId, "workflow_error", null, new { error = ex.Message }));
            Console.WriteLine($"[JOKES] ERROR workflowId={initial.WorkflowId} {ex}");
        }
        finally
        {
            _bus.Complete(initial.WorkflowId);
            if (_ctsByWorkflow.TryRemove(initial.WorkflowId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private async Task<string?> RunAgentWithTimeoutAsync(AIAgent agent, string prompt, string agentType, CancellationToken ct)
    {
        try
        {
            // Ejecutar con timeout y cancelación encadenada
            using var timeoutCts = new CancellationTokenSource(_agentTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var task = _runner.RunAsync(agent, prompt, agentType: agentType, model: _model, cancellationToken: linked.Token);
            var completed = await Task.WhenAny(task, Task.Delay(_agentTimeout, linked.Token)) == task;
            if (!completed)
            {
                // Cancelar la operación del agente si expiró el timeout
                try { timeoutCts.Cancel(); } catch { }
                Console.WriteLine($"[GEN][TIMEOUT] agentType={agentType} model={_model ?? "(null)"} timeout={_agentTimeout.TotalSeconds}s");
                return null;
            }
            var resp = await task.ConfigureAwait(false);
            return resp.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GEN][EXCEPTION] agentType={agentType} model={_model ?? "(null)"} error={ex.Message}");
            return null;
        }
    }

    public bool Stop(string workflowId)
    {
        if (_ctsByWorkflow.TryGetValue(workflowId, out var cts))
        {
            try
            {
                cts.Cancel();
                _bus.Publish(workflowId, new JokeWorkflowEvent(workflowId, "workflow_stopped", null, new { }));
                return true;
            }
            catch { return false; }
        }
        return false;
    }

    private async Task<HashSet<string>> LoadExistingJokesNormalizedAsync()
    {
        var result = new HashSet<string>();
        try
        {
            // Limitar el tiempo de arranque del servidor MCP: si no responde, seguimos vacío
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var list = await _mcp.ListAsync(cts.Token);
            var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var j in jokes)
            {
                try
                {
                    using var rcts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var text = await _mcp.ReadTextAsync(j.Uri, rcts.Token);
                    // Nuevo formato: sólo chiste en texto plano. Tomamos primera línea para normalización.
                    var lines = text.Split('\n');
                    var body = lines.FirstOrDefault()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(body)) result.Add(body.ToLowerInvariant());
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private async Task PersistJokeAsync(string workflowId, string jokeId, int score)
    {
        if (!_items.TryGetValue(workflowId, out var list)) return;
        var item = list.First(i => i.Id == jokeId);
        // Extraer solo el texto del chiste si vino en JSON u otro formato
        var pureText = ExtractJokeBody(item.Text);
        // Actualizar en memoria para que la UI refleje texto limpio
        if (pureText != item.Text)
        {
            SetItem(workflowId, jokeId, ji => ji with { Text = pureText });
            item = list.First(i => i.Id == jokeId); // refrescar referencia
        }
        var fileName = $"jokes/joke-{jokeId}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.txt";
        // Guardar SOLO el texto del chiste (sin metadatos ni JSON). Si es multiline, se escribe tal cual.
        var content = pureText;
        string uri = string.Empty;
        try
        {
            using var wcts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            uri = await _mcp.WriteAsync(fileName, content, wcts.Token);
        }
        catch { /* fallback más abajo */ }
        // Fallback si MCP no respondió a tiempo o devolvió vacío: escritura directa en disco
        if (string.IsNullOrWhiteSpace(uri))
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("MCP_FS_ALLOWED_PATH");
                if (string.IsNullOrWhiteSpace(root))
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    root = Path.Combine(docs, "jokes");
                }
                Directory.CreateDirectory(Path.Combine(root!, "jokes"));
                var fullPath = Path.Combine(root!, fileName.Replace("jokes/", string.Empty));
                await File.WriteAllTextAsync(fullPath, content);
                uri = new Uri(fullPath).AbsoluteUri; // normalmente file:///...
            }
            catch { /* si también falla, dejamos uri vacío pero seguimos el flujo */ }
        }
        SetItem(workflowId, jokeId, ji => ji with { Uri = uri });
        Update(workflowId, s => s with { Saved = s.Saved + 1 });
        // Actualizar índice si está disponible
        if (_index is not null)
        {
            try
            {
                var normalized = JokesIndexService.Normalize(item.Text);
                var hash = JokesIndexService.Hash(normalized);
                var entry = new JokeIndexEntry(
                    Id: jokeId,
                    Uri: uri,
                    Score: score,
                    Timestamp: DateTime.UtcNow.ToString("O"),
                    Normalized: normalized,
                    Hash: hash
                );
                await _index.AddOrUpdateAsync(entry);
            }
            catch { }
        }
    }

    private void Update(string workflowId, Func<JokeWorkflowState, JokeWorkflowState> mutator)
    {
        _states.AddOrUpdate(workflowId, _ => throw new InvalidOperationException(), (id, old) => mutator(old));
    }

    private void SetItem(string workflowId, string jokeId, Func<JokeItem, JokeItem> mutator)
    {
        if (!_items.TryGetValue(workflowId, out var list)) return;
        for (int idx = 0; idx < list.Count; idx++)
        {
            if (list[idx].Id == jokeId)
            {
                list[idx] = mutator(list[idx]);
                return;
            }
        }
    }

    // Eliminado GenerateStubJoke: no se usan mocks; solo generación real vía agentes.

    private static string ExtractJokeBody(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim();
        // Si parece JSON intentamos parsear y sacar 'text'
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                    return t.GetString()!.Trim();
            }
            catch { /* ignorar parse errors */ }
        }
        // Regex rápida para patrón "text": "..."
        var idx = trimmed.IndexOf("\"text\"", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            try
            {
                var after = trimmed.Substring(idx + 6);
                var colon = after.IndexOf(':');
                if (colon > -1)
                {
                    var slice = after.Substring(colon + 1).TrimStart();
                    if (slice.StartsWith("\""))
                    {
                        slice = slice.Substring(1);
                        var endQuote = slice.IndexOf('"');
                        if (endQuote > 0)
                        {
                            return slice.Substring(0, endQuote).Trim();
                        }
                    }
                }
            }
            catch { }
        }
        return trimmed;
    }
}
