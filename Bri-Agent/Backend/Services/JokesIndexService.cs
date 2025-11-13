using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BriAgent.Backend.Services
{
    public record JokeIndexEntry(
        string Id,
        string Uri,
        int Score,
        string Timestamp,
        string Normalized,
        string Hash
    );

    public class JokesIndexService
    {
        private readonly McpFileSystemService _mcp;
        private const string IndexPath = "jokes/index.json";

        public JokesIndexService(McpFileSystemService mcp)
        {
            _mcp = mcp;
        }

        public async Task<List<JokeIndexEntry>> ReadIndexAsync(CancellationToken ct = default)
        {
            try
            {
                var text = await _mcp.ReadTextAsync(IndexPath, ct);
                var items = JsonSerializer.Deserialize<List<JokeIndexEntry>>(text) ?? new();
                return items;
            }
            catch
            {
                return new List<JokeIndexEntry>();
            }
        }

        public async Task WriteIndexAsync(List<JokeIndexEntry> items, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            await _mcp.WriteAsync(IndexPath, json, ct);
        }

        public async Task AddOrUpdateAsync(JokeIndexEntry entry, CancellationToken ct = default)
        {
            var items = await ReadIndexAsync(ct);
            // deduplicar por Hash; si existe, mantener el más reciente por Timestamp
            var idx = items.FindIndex(e => e.Hash == entry.Hash);
            if (idx >= 0) items[idx] = entry; else items.Add(entry);
            // Ordenar descendente por Timestamp
            items.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
            await WriteIndexAsync(items, ct);
        }

        public async Task<int> RebuildAsync(CancellationToken ct = default)
        {
            var list = await _mcp.ListAsync(ct);
            var jokes = list.Where(r => r.Uri.Contains("/jokes/") || r.Name.StartsWith("joke-", StringComparison.OrdinalIgnoreCase)).ToList();
            var items = new List<JokeIndexEntry>();
            foreach (var j in jokes)
            {
                try
                {
                    var text = await _mcp.ReadTextAsync(j.Uri, ct);
                    var lines = text.Split('\n');
                    var meta = lines.FirstOrDefault()?.Trim() ?? string.Empty;
                    var body = (lines.Length > 1 ? lines[1] : lines[0]).Trim();
                    var normalized = Normalize(body);
                    var hash = Hash(normalized);
                    int score = 0;
                    string timestamp = DateTime.UtcNow.ToString("O");
                    // intentar leer score y timestamp desde meta: timestamp=...|score=...
                    if (!string.IsNullOrWhiteSpace(meta))
                    {
                        var parts = meta.Split('|');
                        foreach (var p in parts)
                        {
                            if (p.StartsWith("timestamp=")) timestamp = p.Substring("timestamp=".Length);
                            else if (p.StartsWith("score=") && int.TryParse(p.Substring("score=".Length), out var sc)) score = sc;
                        }
                    }
                    var id = System.IO.Path.GetFileNameWithoutExtension(j.Name);
                    var entry = new JokeIndexEntry(id, j.Uri, score, timestamp, normalized, hash);
                    var existing = items.FindIndex(e => e.Hash == entry.Hash);
                    if (existing >= 0) items[existing] = entry; else items.Add(entry);
                }
                catch { }
            }
            items.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
            await WriteIndexAsync(items, ct);
            return items.Count;
        }

        public async Task<List<JokeIndexEntry>> SearchAsync(string query, int limit = 20, CancellationToken ct = default)
        {
            var items = await ReadIndexAsync(ct);
            query = Normalize(query ?? string.Empty);
            var res = items.Where(i => i.Normalized.Contains(query))
                           .Take(Math.Clamp(limit, 1, 200))
                           .ToList();
            return res;
        }

        public static string Normalize(string text)
        {
            return (text ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static string Hash(string text)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
