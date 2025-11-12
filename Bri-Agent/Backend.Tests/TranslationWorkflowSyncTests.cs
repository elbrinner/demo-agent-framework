using System.Net.Http.Headers;
using System.Text.Json;
using BriAgent.Backend;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Threading.Tasks;
using System;

public class TranslationWorkflowSyncTests : IClassFixture<WebApplicationFactory<ProgramMarker>>
{
    private readonly WebApplicationFactory<ProgramMarker> _factory;

    public TranslationWorkflowSyncTests(WebApplicationFactory<ProgramMarker> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeqSync_Retorna_JSON_Con_Pasos_Y_Result()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    var req = new HttpRequestMessage(HttpMethod.Post, "/bri-agent/translation/seq-sync?dryRun=true");
        req.Content = new StringContent("{\"prompt\":\"Hello world\"}", System.Text.Encoding.UTF8, "application/json");
        using var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("mode", out var mode) && mode.GetString() == "sequential");
        Assert.True(doc.RootElement.TryGetProperty("result", out _));
        Assert.True(doc.RootElement.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array);
        Assert.True(steps.GetArrayLength() == 4);
    }

    [Fact]
    public async Task ParSync_Retorna_JSON_Con_Pasos_Y_Result()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    var req = new HttpRequestMessage(HttpMethod.Post, "/bri-agent/translation/par-sync?dryRun=true");
        req.Content = new StringContent("{\"prompt\":\"Hello world\"}", System.Text.Encoding.UTF8, "application/json");
        using var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("mode", out var mode) && mode.GetString() == "parallel");
        Assert.True(doc.RootElement.TryGetProperty("result", out _));
        Assert.True(doc.RootElement.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array);
        Assert.True(steps.GetArrayLength() == 4);
    }
}
