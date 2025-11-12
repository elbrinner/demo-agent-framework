namespace BriAgent.Backend.Models;

public record WorkflowRequest(List<WorkflowStep> steps, string? threadId = null);
public record WorkflowStep(string id, string prompt);
public record WorkflowStepResult(string id, string? text, double? durationMs = null);

public record SseEnvelope<T>(string type, string? stepId, T data, string? traceId = null, string? spanId = null, long? ts = null);
