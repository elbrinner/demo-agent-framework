using System.Collections.Concurrent;

namespace BriAgent.Backend.Services
{
    public record AgentInvocationTelemetry(
        DateTimeOffset time,
        string agentType,
        string model,
        int promptChars,
        int responseChars,
        long durationMs,
        string? traceId,
        string? spanId)
    {
        // Campos opcionales enriquecidos (si el proveedor los expone o si podemos estimarlos)
        public int? PromptTokens { get; init; }
        public int? CompletionTokens { get; init; }
        public int? TotalTokens { get; init; }
        public double? CostUsd { get; init; }
    }

    public static class TelemetryStore
    {
        private static readonly ConcurrentQueue<AgentInvocationTelemetry> _events = new();
        private const int Max = 200;

        public static void Add(AgentInvocationTelemetry t)
        {
            _events.Enqueue(t);
            while (_events.Count > Max && _events.TryDequeue(out _)) { }
        }

        public static IEnumerable<AgentInvocationTelemetry> GetAll() => _events.ToArray().OrderByDescending(e => e.time);
    }
}
