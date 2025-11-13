using System.Text.Json;

namespace BriAgent.Backend.Services
{
    public record JokeCheckpointSnapshot(
        string ApprovalId,
        string WorkflowId,
        string JokeId,
        int Score,
        string Text,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public class JokesCheckpointService
    {
        private readonly McpFileSystemService _mcp;
        private const string Folder = "jokes/checkpoints";

        public JokesCheckpointService(McpFileSystemService mcp)
        {
            _mcp = mcp;
        }

        private static string FileFor(string approvalId) => $"{Folder}/{approvalId}.json";

        public async Task SaveAsync(JokeCheckpointSnapshot snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await _mcp.WriteAsync(FileFor(snapshot.ApprovalId), json);
        }

        public async Task UpdateStatusAsync(string approvalId, string status)
        {
            var existing = await GetAsync(approvalId);
            if (existing is null) return;
            var updated = existing with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
            await SaveAsync(updated);
        }

        public async Task<JokeCheckpointSnapshot?> GetAsync(string approvalId)
        {
            try
            {
                var text = await _mcp.ReadTextAsync(FileFor(approvalId));
                return JsonSerializer.Deserialize<JokeCheckpointSnapshot>(text);
            }
            catch { return null; }
        }

        public async Task<List<JokeCheckpointSnapshot>> ListAsync(int? limit = 100)
        {
            var list = await _mcp.ListAsync();
            var files = list.Where(r => r.Uri.Contains("/jokes/checkpoints/") || r.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(r => r.Name)
                            .ToList();
            var res = new List<JokeCheckpointSnapshot>();
            foreach (var f in files)
            {
                try
                {
                    var text = await _mcp.ReadTextAsync(f.Uri);
                    var snap = JsonSerializer.Deserialize<JokeCheckpointSnapshot>(text);
                    if (snap is not null) res.Add(snap);
                }
                catch { }
            }
            if (limit.HasValue) res = res.Take(Math.Clamp(limit.Value, 1, 1000)).ToList();
            return res;
        }
    }
}
