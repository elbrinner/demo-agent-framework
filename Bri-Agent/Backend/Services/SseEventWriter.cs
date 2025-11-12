using System.Text;
using System.Text.Json;
using BriAgent.Backend.Models;

namespace BriAgent.Backend.Services;

public static class SseEventWriter
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerOptions.Web);

    public static async Task WriteEventAsync<T>(HttpResponse response, string type, T payload, string? stepId = null, CancellationToken ct = default)
    {
        // Cada evento genera un span hijo para tener granularidad (especialmente tokens)
        var parent = System.Diagnostics.Activity.Current;
        // Crear span hijo; no forzamos SetParentId tras iniciar para evitar InvalidOperationException.
        using var span = BriAgent.Backend.Telemetry.ActivitySource.StartActivity($"sse.{type}", System.Diagnostics.ActivityKind.Internal);
        span?.SetTag("sse.type", type);
        if (stepId != null) span?.SetTag("sse.step_id", stepId);
        if (payload is string s) span?.SetTag("sse.payload.length", s.Length);
        else if (payload is { })
        {
            try
            {
                var serialized = JsonSerializer.Serialize(payload, _jsonOpts);
                span?.SetTag("sse.payload.length", serialized.Length);
            }
            catch { /* ignore */ }
        }

    string? traceId = span?.TraceId.ToString();
    string? spanId = span?.SpanId.ToString();
    var envelope = new SseEnvelope<T>(type, stepId, payload, traceId, spanId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var json = JsonSerializer.Serialize(envelope, _jsonOpts);
        var sb = new StringBuilder();
        sb.Append("event: ").Append(type).Append('\n');
        sb.Append("data: ").Append(json).Append('\n').Append('\n');
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await response.Body.WriteAsync(bytes, 0, bytes.Length, ct);
        await response.Body.FlushAsync(ct);
    }

    public static async Task WriteHeartbeatAsync(HttpResponse response, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(":heartbeat\n\n");
        await response.Body.WriteAsync(bytes, 0, bytes.Length, ct);
        await response.Body.FlushAsync(ct);
    }
}
