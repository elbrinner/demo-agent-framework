using System.Collections.Concurrent;
using System.Threading.Channels;

namespace BriAgent.Backend.Services;

public record JokeWorkflowEvent(string WorkflowId, string Type, string? JokeId, object Payload);

/// <summary>
/// Bus de eventos in-memory por workflow usando Channel para SSE.
/// </summary>
public class JokesEventBus
{
    private readonly ConcurrentDictionary<string, Channel<JokeWorkflowEvent>> _channels = new();

    public ChannelReader<JokeWorkflowEvent> Subscribe(string workflowId)
    {
        var ch = _channels.GetOrAdd(workflowId, _ => CreateChannel());
        return ch.Reader;
    }

    public void Publish(string workflowId, JokeWorkflowEvent evt)
    {
        if (!_channels.TryGetValue(workflowId, out var ch))
        {
            ch = CreateChannel();
            _channels[workflowId] = ch;
        }
        // Intentar escribir sin bloquear; si está lleno ignoramos (backpressure simple)
        ch.Writer.TryWrite(evt);
    }

    public void Complete(string workflowId)
    {
        if (_channels.TryGetValue(workflowId, out var ch))
        {
            ch.Writer.TryComplete();
        }
    }

    private Channel<JokeWorkflowEvent> CreateChannel()
    {
        // Canal sin límite grande para evitar bloqueos en flujo corto (10 chistes)
        var ch = Channel.CreateUnbounded<JokeWorkflowEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        return ch;
    }
}
