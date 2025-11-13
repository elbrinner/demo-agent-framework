using System.Text.Json;
using System.Text.Json.Nodes;

namespace BriAgent.McpServer;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string RootDir = GetRoot();

    public static async Task Main()
    {
        // Bucle principal: JSON-RPC por línea (stdio)
        while (true)
        {
            var line = Console.ReadLine();
            if (line is null) break; // stdin cerrado
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var method = root.TryGetProperty("method", out var mEl) ? mEl.GetString() : null;
                var @params = root.TryGetProperty("params", out var pEl) ? JsonNode.Parse(pEl.GetRawText()) as JsonObject : null;
                if (string.IsNullOrEmpty(method))
                {
                    WriteError(id, -32600, "Invalid Request");
                    continue;
                }

                switch (method)
                {
                    case "resources/list":
                        await HandleListAsync(id);
                        break;
                    case "resources/read":
                        await HandleReadAsync(id, @params);
                        break;
                    case "resources/write":
                        await HandleWriteAsync(id, @params);
                        break;
                    case "resources/append":
                        await HandleAppendAsync(id, @params);
                        break;
                    case "resources/delete":
                        await HandleDeleteAsync(id, @params);
                        break;
                    default:
                        WriteError(id, -32601, $"Method not found: {method}");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError(null, -32000, ex.Message);
            }
        }
    }

    private static async Task HandleListAsync(string? id)
    {
        var list = new List<JsonObject>();
        foreach (var file in Directory.EnumerateFiles(RootDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var name = Path.GetRelativePath(RootDir, file);
                var uri = new Uri(file).AbsoluteUri;
                list.Add(new JsonObject
                {
                    ["uri"] = uri,
                    ["name"] = name.Replace("\\", "/"),
                    ["mimeType"] = GuessMime(file)
                });
            }
            catch { }
        }
        WriteResult(id, new JsonArray(list.ToArray()));
        await Task.CompletedTask;
    }

    private static async Task HandleReadAsync(string? id, JsonObject? @params)
    {
        var uri = @params?["uri"]?.GetValue<string>() ?? string.Empty;
        var path = PathFromUri(RootDir, uri);
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path) : string.Empty;
        var result = new JsonObject { ["text"] = text };
        WriteResult(id, result);
    }

    private static async Task HandleWriteAsync(string? id, JsonObject? @params)
    {
        var relative = @params?["relativePath"]?.GetValue<string>() ?? string.Empty;
        var text = @params?["text"]?.GetValue<string>() ?? string.Empty;
        var path = MapRelative(RootDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Permitir overwrite
        await File.WriteAllTextAsync(path, text);
        var result = new JsonObject { ["uri"] = new Uri(path).AbsoluteUri };
        WriteResult(id, result);
    }

    private static async Task HandleAppendAsync(string? id, JsonObject? @params)
    {
        var uri = @params?["uri"]?.GetValue<string>() ?? string.Empty;
        var text = @params?["text"]?.GetValue<string>() ?? string.Empty;
        var path = PathFromUri(RootDir, uri);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(path, text);
        WriteResult(id, new JsonObject { ["ok"] = true });
    }

    private static async Task HandleDeleteAsync(string? id, JsonObject? @params)
    {
        var require = string.Equals(Environment.GetEnvironmentVariable("MCP_REQUIRE_TOKEN"), "true", StringComparison.OrdinalIgnoreCase);
        if (require)
        {
            var token = @params?["approvalToken"]?.GetValue<string>();
            var expected = Environment.GetEnvironmentVariable("MCP_APPROVAL_TOKEN");
            if (string.IsNullOrEmpty(token) || token != expected)
            {
                WriteError(id, 401, "approval token required");
                return;
            }
        }
        var uri = @params?["uri"]?.GetValue<string>() ?? string.Empty;
        var path = PathFromUri(RootDir, uri);
        if (File.Exists(path)) File.Delete(path);
        WriteResult(id, new JsonObject { ["ok"] = true });
        await Task.CompletedTask;
    }

    private static string GetRoot()
    {
        var root = Environment.GetEnvironmentVariable("MCP_FS_ALLOWED_PATH");
        if (string.IsNullOrWhiteSpace(root)) root = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(root!);
        return Path.GetFullPath(root!);
    }

    private static string EnsureUnderRoot(string root, string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Path outside of allowed root");
        return full;
    }

    private static string MapRelative(string root, string relative)
    {
        var combined = Path.Combine(root, relative.Replace("\\", "/"));
        return EnsureUnderRoot(root, combined);
    }

    private static string PathFromUri(string root, string uriOrPath)
    {
        if (Uri.TryCreate(uriOrPath, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return EnsureUnderRoot(root, uri.LocalPath);
        }
        // Tratar como ruta relativa
        return MapRelative(root, uriOrPath);
    }

    private static string GuessMime(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }

    private static void WriteResult(string? id, JsonNode result)
    {
        var resp = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
        Console.Out.WriteLine(resp.ToJsonString(JsonOpts));
        Console.Out.Flush();
    }

    private static void WriteError(string? id, int code, string message)
    {
        var err = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        };
        var resp = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = err
        };
        Console.Out.WriteLine(resp.ToJsonString(JsonOpts));
        Console.Out.Flush();
    }
}
