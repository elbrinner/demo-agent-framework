using System.Collections.Concurrent;

namespace BriAgent.Backend.Services
{
    public static class ThreadStore
    {
        private static readonly ConcurrentDictionary<string, List<string>> _threads = new();
        // Contextos reales de Agent Framework (thread objects). Se almacenan como dynamic para evitar dependencia de tipo explícito.
        private static readonly ConcurrentDictionary<string, object> _agentContexts = new();

        public static object GetOrCreateAgentContext(string threadId, Microsoft.Agents.AI.AIAgent agent)
            => _agentContexts.GetOrAdd(threadId, _ => agent.GetNewThread());

        public static IReadOnlyList<string> AddMessage(string threadId, string message)
        {
            var list = _threads.GetOrAdd(threadId, _ => new List<string>());
            lock (list)
            {
                list.Add(message);
                return list.ToList();
            }
        }

        public static IReadOnlyList<string> GetHistory(string threadId)
        {
            if (_threads.TryGetValue(threadId, out var list))
            {
                lock (list)
                {
                    return list.ToList();
                }
            }
            return Array.Empty<string>();
        }
    }
}
