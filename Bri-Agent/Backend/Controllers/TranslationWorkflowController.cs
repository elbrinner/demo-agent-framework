using System.Diagnostics;
using BriAgent.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http; // asegurar IActionResult, atributos (aunque normalmente bastaría con Microsoft.AspNetCore.Mvc)

namespace BriAgent.Backend.Controllers;

// Aseguramos atributos de MVC disponibles
[ApiController]
[Route("bri-agent/translation")] // Nuevos endpoints explícitos en Controllers
public class TranslationWorkflowController : BaseAgentController
{
    public record RunRequest(string? prompt);

    // -------- SECUENCIAL (SSE) --------
    [HttpGet("seq")]
    public Task SeqGet([FromQuery] string? prompt) => SeqPost(new RunRequest(prompt));

    [HttpPost("seq")]
    public async Task SeqPost([FromBody] RunRequest? body)
    {
        SetSseHeaders();
        var ct = HttpContext.RequestAborted;
    var original = string.IsNullOrWhiteSpace(body?.prompt) ? "Hola mundo, ¿cómo estás?" : body!.prompt!;
        var wallStart = Stopwatch.StartNew();
        using var activity = StartActivity("translation.seq", original.Length);
        await WriteWorkflowStartedAsync(new { mode = "sequential", original });

        var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
        var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
        var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
        var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

        async Task<(string text,long ms)> RunStepAsync(string id, string name, AIAgent agent, string input)
        {
            var sw = Stopwatch.StartNew();
            await WriteStepStartedAsync(id, name);
            var full = string.Empty;
            await foreach (var upd in agent.RunStreamingAsync(input))
            {
                if (ct.IsCancellationRequested) break;
                var chunk = upd.Text;
                if (string.IsNullOrEmpty(chunk)) continue;
                full += chunk;
                await WriteTokenAsync(chunk, id);
            }
            sw.Stop();
            await WriteStepCompletedAsync(id, full, (long)sw.Elapsed.TotalMilliseconds);
            return (full, (long)sw.Elapsed.TotalMilliseconds);
        }

    var (fr, frMs) = await RunStepAsync("fr",   "Francés",     frenchAgent, original);
    var (pt, ptMs) = await RunStepAsync("pt",   "Portugués",  portugueseAgent, fr);
    var (de, deMs) = await RunStepAsync("de",   "Alemán",     germanAgent, pt);

    var finalInput = $"Original: {original}\nFrancés: {fr}\nPortugués: {pt}\nAlemán: {de}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
        var (final, finMs) = await RunStepAsync("final", "Final", finalAgent, finalInput);

        wallStart.Stop();
        await WriteWorkflowCompletedAsync(new {
            totalDurationMs = frMs + ptMs + deMs + finMs,
            wallDurationMs = (long)wallStart.Elapsed.TotalMilliseconds,
            aggregateStepDurationMs = frMs + ptMs + deMs + finMs,
            result = final,
            steps = new[] {
                new { id = "fr", durationMs = frMs, chars = fr.Length, tokensApprox = TokenCount(fr) },
                new { id = "pt", durationMs = ptMs, chars = pt.Length, tokensApprox = TokenCount(pt) },
                new { id = "de", durationMs = deMs, chars = de.Length, tokensApprox = TokenCount(de) },
                new { id = "final", durationMs = finMs, chars = final.Length, tokensApprox = TokenCount(final) }
            },
            tokensTotalApprox = TokenCount(fr) + TokenCount(pt) + TokenCount(de) + TokenCount(final)
        });
    }

    // -------- SECUENCIAL (SYNC/JSON) --------
    [HttpPost("seq-sync")]
    public async Task<IActionResult> SeqSync([FromBody] RunRequest? body, [FromQuery] bool dryRun = false)
    {
        var ct = HttpContext.RequestAborted;
    var original = string.IsNullOrWhiteSpace(body?.prompt) ? "Hola mundo, ¿cómo estás?" : body!.prompt!;
        var wallStart = Stopwatch.StartNew();
        using var activity = StartActivity("translation.seq.sync", original.Length);

        string fr, pt, de, final; long frMs, ptMs, deMs, finMs;
        if (dryRun)
        {
            fr = $"[FR] {original}"; frMs = 1;
            pt = $"[PT] {fr}";     ptMs = 1;
            de = $"[DE] {pt}";     deMs = 1;
            final = $"[FINAL] Original: {original} | Francés: {fr} | Portugués: {pt} | Alemán: {de}"; finMs = 1;
        }
        else
        {
            var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
            var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
            var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
            var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

            async Task<(string text,long ms)> RunStepAsync(AIAgent agent, string input)
            {
                var sw = Stopwatch.StartNew();
                var res = await agent.RunAsync(input, cancellationToken: ct);
                sw.Stop();
                return (res.Text ?? string.Empty, (long)sw.Elapsed.TotalMilliseconds);
            }

            (fr, frMs) = await RunStepAsync(frenchAgent, original);
            (pt, ptMs) = await RunStepAsync(portugueseAgent, fr);
            (de, deMs) = await RunStepAsync(germanAgent, pt);

            var finalInput = $"Original: {original}\nFrancés: {fr}\nPortugués: {pt}\nAlemán: {de}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
            (final, finMs) = await RunStepAsync(finalAgent, finalInput);
        }

        wallStart.Stop();
        var payload = new {
            mode = "sequential",
            original,
            result = final,
            totalDurationMs = frMs + ptMs + deMs + finMs,
            wallDurationMs = (long)wallStart.Elapsed.TotalMilliseconds,
            aggregateStepDurationMs = frMs + ptMs + deMs + finMs,
            steps = new[] {
                new { id = "fr", durationMs = frMs, chars = fr.Length, tokensApprox = TokenCount(fr), output = fr },
                new { id = "pt", durationMs = ptMs, chars = pt.Length, tokensApprox = TokenCount(pt), output = pt },
                new { id = "de", durationMs = deMs, chars = de.Length, tokensApprox = TokenCount(de), output = de },
                new { id = "final", durationMs = finMs, chars = final.Length, tokensApprox = TokenCount(final), output = final }
            },
            tokensTotalApprox = TokenCount(fr) + TokenCount(pt) + TokenCount(de) + TokenCount(final)
        };
        return Ok(payload);
    }

    // -------- PARALELO (SSE) --------
    [HttpGet("par")]
    public Task ParGet([FromQuery] string? prompt) => ParPost(new RunRequest(prompt));

    [HttpPost("par")]
    public async Task ParPost([FromBody] RunRequest? body)
    {
        SetSseHeaders();
        var ct = HttpContext.RequestAborted;
    var original = string.IsNullOrWhiteSpace(body?.prompt) ? "Hola mundo, ¿cómo estás?" : body!.prompt!;
        var wallStart = Stopwatch.StartNew();
        using var activity = StartActivity("translation.par", original.Length);
        await WriteWorkflowStartedAsync(new { mode = "parallel", original });

        var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
        var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
        var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
        var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

        async Task<(string text,long ms)> Fanout(string id, string name, AIAgent agent)
        {
            var sw = Stopwatch.StartNew();
            await WriteStepStartedAsync(id, name);
            var full = string.Empty;
            await foreach (var upd in agent.RunStreamingAsync(original))
            {
                if (ct.IsCancellationRequested) break;
                var chunk = upd.Text;
                if (string.IsNullOrEmpty(chunk)) continue;
                full += chunk;
                await WriteTokenAsync(chunk, id);
            }
            sw.Stop();
            await WriteStepCompletedAsync(id, full, (long)sw.Elapsed.TotalMilliseconds);
            return (full, (long)sw.Elapsed.TotalMilliseconds);
        }

    var frTask = Fanout("fr", "Francés", frenchAgent);
    var ptTask = Fanout("pt", "Portugués", portugueseAgent);
    var deTask = Fanout("de", "Alemán",  germanAgent);

    await Task.WhenAll(frTask, ptTask, deTask);
    var fr = frTask.Result.text;
    var pt = ptTask.Result.text;
    var de = deTask.Result.text;

    var finalInput = $"Original: {original}\nFrancés: {fr}\nPortugués: {pt}\nAlemán: {de}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
        var swFinal = Stopwatch.StartNew();
        await WriteStepStartedAsync("final", "Final");
        var finalText = string.Empty;
        await foreach (var upd in finalAgent.RunStreamingAsync(finalInput))
        {
            if (ct.IsCancellationRequested) break;
            var chunk = upd.Text;
            if (string.IsNullOrEmpty(chunk)) continue;
            finalText += chunk;
            await WriteTokenAsync(chunk, "final");
        }
        swFinal.Stop();
        await WriteStepCompletedAsync("final", finalText, (long)swFinal.Elapsed.TotalMilliseconds);

        wallStart.Stop();
        await WriteWorkflowCompletedAsync(new {
            totalDurationMs = frTask.Result.ms + ptTask.Result.ms + deTask.Result.ms + (long)swFinal.Elapsed.TotalMilliseconds,
            wallDurationMs = (long)wallStart.Elapsed.TotalMilliseconds,
            aggregateStepDurationMs = frTask.Result.ms + ptTask.Result.ms + deTask.Result.ms + (long)swFinal.Elapsed.TotalMilliseconds,
            result = finalText,
            steps = new[] {
                new { id = "fr", durationMs = frTask.Result.ms, chars = fr.Length, tokensApprox = TokenCount(fr) },
                new { id = "pt", durationMs = ptTask.Result.ms, chars = pt.Length, tokensApprox = TokenCount(pt) },
                new { id = "de", durationMs = deTask.Result.ms, chars = de.Length, tokensApprox = TokenCount(de) },
                new { id = "final", durationMs = (long)swFinal.Elapsed.TotalMilliseconds, chars = finalText.Length, tokensApprox = TokenCount(finalText) }
            },
            tokensTotalApprox = TokenCount(fr) + TokenCount(pt) + TokenCount(de) + TokenCount(finalText)
        });
    }

    // -------- PARALELO (SYNC/JSON) --------
    [HttpPost("par-sync")]
    public async Task<IActionResult> ParSync([FromBody] RunRequest? body, [FromQuery] bool dryRun = false)
    {
        var ct = HttpContext.RequestAborted;
    var original = string.IsNullOrWhiteSpace(body?.prompt) ? "Hola mundo, ¿cómo estás?" : body!.prompt!;
        var wallStart = Stopwatch.StartNew();
        using var activity = StartActivity("translation.par.sync", original.Length);

        string fr, pt, de, final; long frMs, ptMs, deMs, finMs;
        if (dryRun)
        {
            fr = $"[FR] {original}"; frMs = 1;
            pt = $"[PT] {original}"; ptMs = 1;
            de = $"[DE] {original}"; deMs = 1;
            final = $"[FINAL] Original: {original} | Francés: {fr} | Portugués: {pt} | Alemán: {de}"; finMs = 1;
        }
        else
        {
            var frenchAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al francés.");
            var portugueseAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al portugués.");
            var germanAgent = AgentFactory.CreateBasicAgent("Eres un asistente de traducción que traduce el texto proporcionado al alemán.");
            var finalAgent  = AgentFactory.CreateBasicAgent("Eres un asistente final. Devuelve el texto en el idioma original del usuario (inglés), incluyendo las traducciones intermedias (francés / portugués / alemán). Formatea las secciones de forma clara.");

            async Task<(string text,long ms)> RunStepAsync(AIAgent agent, string input)
            {
                var sw = Stopwatch.StartNew();
                var res = await agent.RunAsync(input, cancellationToken: ct);
                sw.Stop();
                return (res.Text ?? string.Empty, (long)sw.Elapsed.TotalMilliseconds);
            }

            var frTask = RunStepAsync(frenchAgent, original);
            var ptTask = RunStepAsync(portugueseAgent, original);
            var deTask = RunStepAsync(germanAgent, original);
            await Task.WhenAll(frTask, ptTask, deTask);
            fr = frTask.Result.text; frMs = frTask.Result.ms;
            pt = ptTask.Result.text; ptMs = ptTask.Result.ms;
            de = deTask.Result.text; deMs = deTask.Result.ms;

            var finalInput = $"Original: {original}\nFrancés: {fr}\nPortugués: {pt}\nAlemán: {de}\nDevuelve un resumen en inglés que incorpore todas las traducciones.";
            (final, finMs) = await RunStepAsync(finalAgent, finalInput);
        }

        wallStart.Stop();
        var payload = new {
            mode = "parallel",
            original,
            result = final,
            totalDurationMs = frMs + ptMs + deMs + finMs,
            wallDurationMs = (long)wallStart.Elapsed.TotalMilliseconds,
            aggregateStepDurationMs = frMs + ptMs + deMs + finMs,
            steps = new[] {
                new { id = "fr", durationMs = frMs, chars = fr.Length, tokensApprox = TokenCount(fr), output = fr },
                new { id = "pt", durationMs = ptMs, chars = pt.Length, tokensApprox = TokenCount(pt), output = pt },
                new { id = "de", durationMs = deMs, chars = de.Length, tokensApprox = TokenCount(de), output = de },
                new { id = "final", durationMs = finMs, chars = final.Length, tokensApprox = TokenCount(final), output = final }
            },
            tokensTotalApprox = TokenCount(fr) + TokenCount(pt) + TokenCount(de) + TokenCount(final)
        };
        return Ok(payload);
    }

    private static int TokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // aproximación rápida: separar por espacios/puntuación simple
    var parts = System.Text.RegularExpressions.Regex.Split(text.Trim(), "\\s+");
        return parts.Count(p => p.Length > 0);
    }
}
