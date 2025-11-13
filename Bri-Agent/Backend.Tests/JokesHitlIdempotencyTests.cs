using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class JokesHitlIdempotencyTests : IClassFixture<WebApplicationFactory<BriAgent.Backend.ProgramMarker>>
{
    private readonly WebApplicationFactory<BriAgent.Backend.ProgramMarker> _factory;
    public JokesHitlIdempotencyTests(WebApplicationFactory<BriAgent.Backend.ProgramMarker> factory)
    {
        _factory = factory;
    }

    private record StartDto(string WorkflowId, int TargetTotal);
    private record StatusDto(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals, ItemDto[] Items);
    private record ItemDto(string Id, string Text, int? Score, string? Uri, string? ApprovalId);

    [Fact]
    public async Task Approve_Is_Idempotent_And_Clears_ApprovalId()
    {
        var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 2, EnsureHitl = true });
        start.EnsureSuccessStatusCode();
        var dto = await start.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(dto);
        var workflowId = dto!.WorkflowId;

        string? approvalId = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline && approvalId is null)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            approvalId = status!.Items.FirstOrDefault(i => i.ApprovalId != null)?.ApprovalId;
            if (approvalId is null) await Task.Delay(200);
        }
        Assert.NotNull(approvalId);

        // Primer approve debe devolver OK; segundo debe devolver NotFound (ya no Pending)
        var first = await client.PostAsJsonAsync("/api/jokes/approve", approvalId);
        first.EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/jokes/approve", approvalId);
        Assert.False(second.IsSuccessStatusCode);

        // Esperar a que termine y verificar que no queda el ApprovalId en items
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            if (status!.PendingApprovals == 0 && status.Generated == status.TargetTotal) break;
            await Task.Delay(200);
        }
        var finalStatus = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
        Assert.DoesNotContain(finalStatus!.Items, it => it.ApprovalId == approvalId);
    }

    [Fact]
    public async Task Reject_Is_Idempotent_And_Does_Not_Create_File()
    {
        var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 2, EnsureHitl = true });
        start.EnsureSuccessStatusCode();
        var dto = await start.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(dto);
        var workflowId = dto!.WorkflowId;

        string? approvalId = null; string? jokeId = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline && approvalId is null)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            var ap = status!.Items.FirstOrDefault(i => i.ApprovalId != null);
            if (ap is not null) { approvalId = ap.ApprovalId; jokeId = ap.Id; }
            if (approvalId is null) await Task.Delay(200);
        }
        Assert.NotNull(approvalId);

        var first = await client.PostAsJsonAsync("/api/jokes/reject", approvalId);
        first.EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/jokes/reject", approvalId);
        Assert.False(second.IsSuccessStatusCode);

        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            if (status!.PendingApprovals == 0 && status.Generated == status.TargetTotal) break;
            await Task.Delay(200);
        }

        // Validar que el chiste rechazado no aparece en archivos
        var list = await client.GetFromJsonAsync<ListDto>("/api/jokes/list");
        Assert.NotNull(list);
        if (jokeId is not null)
        {
            Assert.DoesNotContain(list!.Resources, r => r.Name.Contains(jokeId));
        }
    }

    private record ListDto(int Count, ResourceDto[] Resources);
    private record ResourceDto(string Uri, string Name, string? MimeType);
}
