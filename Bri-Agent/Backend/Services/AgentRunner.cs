using System.Diagnostics;
using BriAgent.Backend;
using Microsoft.Agents.AI;

namespace BriAgent.Backend.Services
{
    /// <summary>
    /// Envoltorio para ejecutar agentes con telemetría unificada (Activity/OTel + TelemetryStore),
    /// soporte de cancelación y posibles extensiones futuras (reintentos, políticas, etc.).
    /// </summary>
    public class AgentRunner
    {
        public async Task<AgentRunResponse> RunAsync(
            AIAgent agent,
            string prompt,
            AgentThread? thread = null,
            CancellationToken cancellationToken = default,
            string? model = null,
            string agentType = "agent")
        {
            var started = DateTimeOffset.UtcNow;
            using var activity = Telemetry.ActivitySource.StartActivity("Agent.Run", ActivityKind.Client);
            activity?.SetTag("agent.type", agentType);
            if (!string.IsNullOrWhiteSpace(model)) activity?.SetTag("ai.model", model);
            activity?.SetTag("prompt.length", prompt?.Length ?? 0);

            AgentRunResponse response;
            try
            {
                response = await agent.RunAsync(prompt, thread: thread, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("exception.type", ex.GetType().FullName);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }
            finally
            {
                activity?.Stop();
            }

            var ended = DateTimeOffset.UtcNow;
                // Intentar extraer uso de tokens del objeto de respuesta (best-effort, vía reflexión para no acoplar fuerte)
                int? promptTokens = null, completionTokens = null, totalTokens = null;
                try
                {
                    var respType = response.GetType();
                    var usageProp = respType.GetProperty("Usage");
                    var usageVal = usageProp?.GetValue(response);
                    if (usageVal is not null)
                    {
                        // Intentar propiedades típicas
                        promptTokens = TryGetIntProp(usageVal, "PromptTokens") ?? TryGetIntProp(usageVal, "InputTokens") ?? TryGetIntProp(usageVal, "InputTokenCount");
                        completionTokens = TryGetIntProp(usageVal, "CompletionTokens") ?? TryGetIntProp(usageVal, "OutputTokens") ?? TryGetIntProp(usageVal, "OutputTokenCount");
                        totalTokens = TryGetIntProp(usageVal, "TotalTokens")
                                      ?? ((promptTokens.HasValue && completionTokens.HasValue) ? promptTokens + completionTokens : null);
                    }
                    // Si no hay usage, estimación muy aproximada basada en caracteres (4 chars ≈ 1 token)
                    if (!promptTokens.HasValue)
                        promptTokens = (prompt?.Length ?? 0) / 4;
                    var respText = response.Text ?? string.Empty;
                    if (!completionTokens.HasValue)
                        completionTokens = respText.Length / 4;
                    if (!totalTokens.HasValue)
                        totalTokens = promptTokens + completionTokens;
                }
                catch { /* noop */ }

                // Añadir tags al Activity si está presente
                activity?.SetTag("ai.usage.prompt_tokens", promptTokens);
                activity?.SetTag("ai.usage.completion_tokens", completionTokens);
                activity?.SetTag("ai.usage.total_tokens", totalTokens);

                // Calcular coste estimado si hay información de precios disponible
                double? costUsd = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        var price = ModelPricing.GetPriceForModel(model!);
                        if (price is not null)
                        {
                            var pt = promptTokens ?? 0;
                            var ct = completionTokens ?? 0;
                            costUsd = ((pt * price.PromptPer1K) + (ct * price.CompletionPer1K)) / 1000.0;
                        }
                    }
                }
                catch { /* noop */ }

                if (costUsd.HasValue)
                {
                    activity?.SetTag("ai.usage.cost_usd", costUsd.Value);
                }

                try
                {
                    TelemetryStore.Add(new AgentInvocationTelemetry(
                        time: ended,
                        agentType: agentType,
                        model: model ?? string.Empty,
                        promptChars: prompt?.Length ?? 0,
                        responseChars: (response.Text ?? string.Empty).Length,
                        durationMs: (long)(ended - started).TotalMilliseconds,
                        traceId: activity?.TraceId.ToString(),
                        spanId: activity?.SpanId.ToString())
                    {
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        TotalTokens = totalTokens,
                        CostUsd = costUsd
                    });
                }
                catch { /* noop */ }

            return response;
        }

            private static int? TryGetIntProp(object obj, string name)
            {
                try
                {
                    var p = obj.GetType().GetProperty(name);
                    var v = p?.GetValue(obj);
                    if (v is int i) return i;
                    if (v is long l) return (int)l;
                    if (v is double d) return (int)d;
                    if (v is float f) return (int)f;
                    if (v is string s && int.TryParse(s, out var si)) return si;
                }
                catch { }
                return null;
            }
    }
}
