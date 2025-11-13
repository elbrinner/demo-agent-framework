using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Collections.Generic;
using BriAgent.Backend.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Xunit;

public class JokesWorkflowTests : IClassFixture<WebApplicationFactory<BriAgent.Backend.ProgramMarker>>
{
    private readonly WebApplicationFactory<BriAgent.Backend.ProgramMarker> _factory;

    public JokesWorkflowTests(WebApplicationFactory<BriAgent.Backend.ProgramMarker> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Podríamos inyectar versiones alternativas de herramientas si fuera necesario.
        });
    }

    [Fact]
    public async Task Workflow_Completo_AutoStore_Sin_HITL()
    {
        var client = _factory.CreateClient();
        // Iniciar workflow con pocos chistes para test rápido
        var startResp = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 3 });
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(startJson);
        var workflowId = startJson!.WorkflowId;

    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        bool completed = false;
        var processedApprovals = new HashSet<string>();
        while (DateTime.UtcNow < deadline && !completed)
        {
            var statusResp = await client.GetAsync($"/api/jokes/status/{workflowId}");
            statusResp.EnsureSuccessStatusCode();
            var status = await statusResp.Content.ReadFromJsonAsync<StatusDto>();
            Assert.NotNull(status);

            // Aprobar cualquier pendiente inmediatamente para desbloquear el flujo
            foreach (var ap in status!.Items.Where(i => i.ApprovalId != null))
            {
                if (ap.ApprovalId != null && processedApprovals.Add(ap.ApprovalId))
                {
                    var approve = await client.PostAsJsonAsync("/api/jokes/approve", ap.ApprovalId);
                    approve.EnsureSuccessStatusCode();
                }
            }

            completed = status.Generated == status.TargetTotal && status.PendingApprovals == 0;
            if (!completed)
                await Task.Delay(250); // Pequeño backoff
        }

        Assert.True(completed, "Debe completar workflow dentro del tiempo");
        var finalStatusResp = await client.GetAsync($"/api/jokes/status/{workflowId}");
        var finalStatus = await finalStatusResp.Content.ReadFromJsonAsync<StatusDto>();
        Assert.NotNull(finalStatus);
        Assert.True(finalStatus!.Saved >= 1, "Se espera al menos un chiste almacenado");
    }

    [Fact]
    public async Task Workflow_HITL_Aprobacion_Manual()
    {
        var client = _factory.CreateClient();
    var startResp = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 5, EnsureHitl = true });
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(startJson);
        var workflowId = startJson!.WorkflowId;

        string? firstApprovalId = null;
        bool sawApproval = false; bool completed = false;
        var processedApprovals = new HashSet<string>();
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < deadline && !completed)
        {
            var statusResp = await client.GetAsync($"/api/jokes/status/{workflowId}");
            statusResp.EnsureSuccessStatusCode();
            var status = await statusResp.Content.ReadFromJsonAsync<StatusDto>();
            Assert.NotNull(status);

            foreach (var ap in status!.Items.Where(i => i.ApprovalId != null))
            {
                if (ap.ApprovalId != null && processedApprovals.Add(ap.ApprovalId))
                {
                    sawApproval = true;
                    firstApprovalId ??= ap.ApprovalId;
                    var approve = await client.PostAsJsonAsync("/api/jokes/approve", ap.ApprovalId);
                    approve.EnsureSuccessStatusCode();
                }
            }

            completed = status.Generated == status.TargetTotal && status.PendingApprovals == 0;
            if (!completed)
                await Task.Delay(300);
        }
        Assert.True(completed, "Workflow debe terminar");
        Assert.True(sawApproval, "Debe existir al menos un chiste que requirió aprobación (probabilidad alta con 5 chistes)");
        var finalStatusResp = await client.GetAsync($"/api/jokes/status/{workflowId}");
        var finalStatus = await finalStatusResp.Content.ReadFromJsonAsync<StatusDto>();
        Assert.NotNull(finalStatus);
        // Nueva semántica: tras aprobación se limpia ApprovalId, verificamos que ya NO esté.
        if (firstApprovalId != null)
        {
            Assert.DoesNotContain(finalStatus!.Items, it => it.ApprovalId == firstApprovalId);
        }
    }

    [Fact]
    public async Task Workflow_HITL_Rechazo_Manual()
    {
        var client = _factory.CreateClient();
    var startResp = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 8, EnsureHitl = true });
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(startJson);
        var workflowId = startJson!.WorkflowId;

        var processedApprovals = new HashSet<string>();
        bool rejectedOne = false;
        bool sawApproval = false;
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            foreach (var ap in status!.Items.Where(i => i.ApprovalId != null))
            {
                if (ap.ApprovalId == null) continue;
                if (!processedApprovals.Add(ap.ApprovalId)) continue;
                sawApproval = true;
                if (!rejectedOne)
                {
                    var reject = await client.PostAsJsonAsync("/api/jokes/reject", ap.ApprovalId);
                    reject.EnsureSuccessStatusCode();
                    rejectedOne = true;
                }
                else
                {
                    var approve = await client.PostAsJsonAsync("/api/jokes/approve", ap.ApprovalId);
                    approve.EnsureSuccessStatusCode();
                }
            }
            if (status.Generated == status.TargetTotal && status.PendingApprovals == 0) break;
            await Task.Delay(250);
        }
        var finalStatus = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
        Assert.True(finalStatus!.Generated == finalStatus.TargetTotal && finalStatus.PendingApprovals == 0, "Workflow debe terminar");
        Assert.True(sawApproval, "Se esperaba al menos una aprobación para probar rechazo");
        Assert.True(rejectedOne, "Debió rechazarse al menos un chiste");
        Assert.True(finalStatus.Deleted > 0, "Debe existir al menos un chiste marcado como eliminado (rechazado)");
    }

    // DTOs locales para deserializar respuestas
    private record StartDto(string WorkflowId, int TargetTotal);
    private record StatusDto(string WorkflowId, int TargetTotal, int Generated, int Saved, int Deleted, int PendingApprovals, ItemDto[] Items);
    private record ItemDto(string Id, string Text, int? Score, string? Uri, string? ApprovalId);

    private record ListDto(int Count, ResourceDto[] Resources);
    private record ResourceDto(string Uri, string Name, string? MimeType);

    [Fact]
    public async Task Human_Reject_Does_Not_Create_File_And_FileCounts_Match_Saved()
    {
        var client = _factory.CreateClient();

        // Contar archivos actuales antes del workflow
        var listBefore = await client.GetFromJsonAsync<ListDto>("/api/jokes/list");
        Assert.NotNull(listBefore);
        var beforeCount = listBefore!.Count;

        // Iniciar workflow con varios chistes para forzar al menos un HITL
        var startResp = await client.PostAsJsonAsync("/api/jokes/start", new { Total = 8 });
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<StartDto>();
        Assert.NotNull(startJson);
        var workflowId = startJson!.WorkflowId;

        string? rejectedJokeId = null;
        var processedApprovals = new HashSet<string>();
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
            Assert.NotNull(status);
            foreach (var ap in status!.Items.Where(i => i.ApprovalId != null))
            {
                if (ap.ApprovalId is null) continue;
                if (!processedApprovals.Add(ap.ApprovalId)) continue;

                if (rejectedJokeId is null)
                {
                    // Rechazar el primero que pida HITL
                    rejectedJokeId = ap.Id;
                    var reject = await client.PostAsJsonAsync("/api/jokes/reject", ap.ApprovalId);
                    reject.EnsureSuccessStatusCode();
                }
                else
                {
                    var approve = await client.PostAsJsonAsync("/api/jokes/approve", ap.ApprovalId);
                    approve.EnsureSuccessStatusCode();
                }
            }

            if (status.Generated == status.TargetTotal && status.PendingApprovals == 0) break;
            await Task.Delay(250);
        }

        // Estado final
        var finalStatus = await client.GetFromJsonAsync<StatusDto>($"/api/jokes/status/{workflowId}");
        Assert.NotNull(finalStatus);
        Assert.True(finalStatus!.Generated == finalStatus.TargetTotal && finalStatus.PendingApprovals == 0, "Workflow debe terminar");

        // Archivos después del workflow
        var listAfter = await client.GetFromJsonAsync<ListDto>("/api/jokes/list");
        Assert.NotNull(listAfter);
        var afterCount = listAfter!.Count;

        // Verificar que sólo los guardados crearon archivos
    // Puede haber archivos acumulados de otros tests (directorio compartido); asegurar que al menos coincide o excede.
    Assert.True(afterCount - beforeCount >= finalStatus.Saved, $"Esperado >= Saved ({finalStatus.Saved}) pero diff fue {afterCount - beforeCount}");

        // Si hubo un rechazo humano, su id no debe aparecer en ningún nombre de archivo
        if (rejectedJokeId != null)
        {
            Assert.DoesNotContain(listAfter.Resources, r => r.Name.Contains(rejectedJokeId));
        }
    }
}
