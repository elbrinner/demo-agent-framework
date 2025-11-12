using System.Net.Http.Headers;
using System.Text.Json;
using BriAgent.Backend;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Threading.Tasks;
using System;

public class WorkflowStreamingTests : IClassFixture<WebApplicationFactory<ProgramMarker>>
{
    private readonly WebApplicationFactory<ProgramMarker> _factory;

    public WorkflowStreamingTests(WebApplicationFactory<ProgramMarker> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Podríamos inyectar mocks del agente aquí si fuera necesario en el futuro
        });
    }

    [Fact(Skip = "Desactivado: SSE genérico de WorkflowController ya no es parte del flujo principal (mantenemos traducciones en Controllers)")]
    public async Task Seq_Stream_Emite_Eventos_Clave_En_Orden()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    var req = new HttpRequestMessage(HttpMethod.Get, "/bri-agent/workflows/seq?dryRun=true");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);
        bool sawStarted = false, sawStepStarted = false, sawToken = false, sawStepCompleted = false, sawWorkflowCompleted = false, sawCompleted = false;
        string? currentEvent = null;
        int guard = 0;
        while (!reader.EndOfStream && guard < 2000)
        {
            var line = await reader.ReadLineAsync();
            guard++;
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("event: "))
            {
                currentEvent = line.Substring(7).Trim();
                var ev = currentEvent;
                if (ev == "workflow_started") sawStarted = true;
                if (ev == "step_started") sawStepStarted = true;
                if (ev == "token") sawToken = true;
                if (ev == "step_completed") sawStepCompleted = true;
                if (ev == "workflow_completed") sawWorkflowCompleted = true;
                if (ev == "completed") { sawCompleted = true; break; }
            }
            else if (line.StartsWith("data: "))
            {
                // Intenta parsear la envoltura JSON para eventos enviados sólo como data.
                try
                {
                    var json = line.Substring(6);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("type", out var t))
                    {
                        var ev = t.GetString();
                        if (ev == "workflow_started") sawStarted = true;
                        if (ev == "step_started") sawStepStarted = true;
                        if (ev == "token") sawToken = true;
                        if (ev == "step_completed") sawStepCompleted = true;
                        if (ev == "workflow_completed") sawWorkflowCompleted = true;
                        if (ev == "completed") { sawCompleted = true; break; }
                    }
                }
                catch { /* ignorar errores de parse */ }
            }
        }

        Assert.True(sawStarted, "Debe emitir workflow_started");
        Assert.True(sawStepStarted, "Debe emitir step_started");
        Assert.True(sawToken, "Debe emitir al menos un token");
        Assert.True(sawStepCompleted, "Debe emitir step_completed");
        Assert.True(sawWorkflowCompleted, "Debe emitir workflow_completed");
        Assert.True(sawCompleted, "Debe emitir completed");
    }

    [Fact(Skip = "Desactivado: SSE genérico de WorkflowController ya no es parte del flujo principal (mantenemos traducciones en Controllers)")]
    public async Task Par_Stream_Emite_Eventos_Y_Tokens()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    var req = new HttpRequestMessage(HttpMethod.Get, "/bri-agent/workflows/par?dryRun=true");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);
        bool sawStarted = false, sawAnyToken = false, sawAnyStepCompleted = false, sawCompleted = false;
        string? currentEvent = null;
        int guard = 0;
        while (!reader.EndOfStream && guard < 2000)
        {
            var line = await reader.ReadLineAsync();
            guard++;
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("event: "))
            {
                currentEvent = line.Substring(7).Trim();
                var ev = currentEvent;
                if (ev == "workflow_started") sawStarted = true;
                if (ev == "token") sawAnyToken = true;
                if (ev == "step_completed") sawAnyStepCompleted = true;
                if (ev == "completed") { sawCompleted = true; break; }
            }
            else if (line.StartsWith("data: "))
            {
                try
                {
                    var json = line.Substring(6);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("type", out var t))
                    {
                        var ev = t.GetString();
                        if (ev == "workflow_started") sawStarted = true;
                        if (ev == "token") sawAnyToken = true;
                        if (ev == "step_completed") sawAnyStepCompleted = true;
                        if (ev == "completed") { sawCompleted = true; break; }
                    }
                }
                catch { }
            }
        }

        Assert.True(sawStarted, "Debe emitir workflow_started");
        Assert.True(sawAnyToken, "Debe emitir tokens en paralelo");
        Assert.True(sawAnyStepCompleted, "Debe completar al menos una rama");
        Assert.True(sawCompleted, "Debe emitir completed");
    }
}
