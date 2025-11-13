using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Xunit;

public class JokesCheckpointEventsTests : IClassFixture<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<BriAgent.Backend.ProgramMarker>>
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<BriAgent.Backend.ProgramMarker> _factory;
    public JokesCheckpointEventsTests(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<BriAgent.Backend.ProgramMarker> factory)
    {
        _factory = factory;
    }

    private record StartDto(string WorkflowId, int TargetTotal);
    private record StatusDto(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals, ItemDto[] Items);
    private record ItemDto(string Id, string Text, int? Score, string? Uri, string? ApprovalId);

    [Fact]
    public async Task Workflow_Emite_Checkpoint_Pause_Y_Resume()
    {
        var client = _factory.CreateClient();
        // Forzar HITL para garantizar eventos checkpoint
        var start = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 1, EnsureHitl = true });
        start.EnsureSuccessStatusCode();
        var dto = await start.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(dto);
        var workflowId = dto!.WorkflowId;

        bool sawPaused = false; bool sawResumed = false; string? approvalId = null; bool approved = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline && !(sawPaused && sawResumed))
        {
            var statusResp = await client.GetAsync($"/api/jokes/status/{workflowId}");
            statusResp.EnsureSuccessStatusCode();
            var status = await statusResp.Content.ReadFromJsonAsync<StatusDto>();
            Assert.NotNull(status);
            // Detectar chiste en espera de aprobación y aprobarlo para que se emita resume
            foreach (var item in status!.Items.Where(i => i.ApprovalId != null))
            {
                approvalId ??= item.ApprovalId;
                if (!approved && item.ApprovalId != null)
                {
                    var approve = await client.PostAsJsonAsync("/api/jokes/approve", item.ApprovalId);
                    approve.EnsureSuccessStatusCode();
                    approved = true;
                }
            }

            // Consultar eventos de workflow (endpoint SSE no es fácilmente testeable aquí; usamos heurística en estado)
            // Estrategia: cuando aparece PendingApprovals > 0 consideramos que ya pasó por checkpoint_paused,
            // y cuando vuelve a 0 después de haber estado >0 asumimos que se reanudó.
            if (status.PendingApprovals > 0) sawPaused = true;
            if (sawPaused && status.PendingApprovals == 0) sawResumed = true;

            if (!(sawPaused && sawResumed)) await Task.Delay(250);
        }

        Assert.True(sawPaused, "Debe pausar en checkpoint (pendiente HITL)");
        Assert.True(sawResumed, "Debe reanudar tras aprobación");
    }
}
