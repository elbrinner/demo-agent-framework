using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BriAgent.Backend.Services;

public record McpResourceDto(string Uri, string Name, string? MimeType);

/// <summary>
/// Cliente mínimo para el servidor MCP .NET (framing: una petición JSON por línea vía stdio).
/// Lanza el proceso (dotnet run --project MCP/dotnet-filesystem-server/...) y envía/recibe JSON-RPC 2.0.
/// </summary>
public class McpFileSystemService : IAsyncDisposable
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Process? _proc;
    private StreamWriter? _stdin;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode?>> _pending = new();
    private string? _allowedRoot; // cache de la raíz permitida para detectar cambios

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        var envRoot = Environment.GetEnvironmentVariable("MCP_FS_ALLOWED_PATH");
        var rootChanged = _allowedRoot != envRoot && !string.IsNullOrWhiteSpace(envRoot);
        // Reiniciar si cambia la raíz permitida para garantizar aislamiento correcto
        if (rootChanged && _proc is { HasExited: false })
        {
            try { _proc.Kill(entireProcessTree: true); } catch { }
            _proc = null; _stdin = null; _readerCts?.Cancel();
        }
        if (_proc is { HasExited: false }) return;
        await _startLock.WaitAsync(ct);
        try
        {
            if (_proc is { HasExited: false }) return;
            _allowedRoot = envRoot; // fijar cache actual

            var projectPath = ResolveMcpProjectPath();
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Propagar variables relevantes
            CopyIfSet(envRoot, "MCP_FS_ALLOWED_PATH", psi);
            CopyIfSet(Environment.GetEnvironmentVariable("MCP_REQUIRE_TOKEN"), "MCP_REQUIRE_TOKEN", psi);
            CopyIfSet(Environment.GetEnvironmentVariable("MCP_APPROVAL_TOKEN"), "MCP_APPROVAL_TOKEN", psi);

            _proc = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar el proceso MCP");
            _stdin = _proc.StandardInput;
            _stdin.AutoFlush = true;

            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReaderLoopAsync(_proc.StandardOutput, _readerCts.Token));

            // Log de errores asíncrono (no bloqueante)
            _ = Task.Run(async () =>
            {
                var err = _proc.StandardError;
                char[] buf = new char[1024];
                while (!_proc.HasExited)
                {
                    var n = await err.ReadAsync(buf, 0, buf.Length);
                    if (n <= 0) break;
                    // opcional: escribir en consola/telemetría
                    // Console.Error.Write(new string(buf, 0, n));
                }
            });
        }
        finally
        {
            _startLock.Release();
        }
    }

    private static void CopyIfSet(string? value, string key, ProcessStartInfo psi)
    {
        if (!string.IsNullOrEmpty(value)) psi.Environment[key] = value!;
    }

    private static string ResolveMcpProjectPath()
    {
        // Buscar desde base directory hacia arriba la carpeta MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "MCP", "dotnet-filesystem-server", "BriAgent.McpServer.csproj");
            if (File.Exists(candidate)) return candidate;
        }
        // Fallback: ruta relativa típica desde Backend/bin/Debug/net9.0/...
        return Path.GetFullPath(Path.Combine(baseDir, "../../../../MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj"));
    }

    private async Task ReaderLoopAsync(StreamReader stdout, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line;
            try { line = await stdout.ReadLineAsync(); }
            catch { break; }
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var hasId = root.TryGetProperty("id", out var idEl);
                var id = hasId ? idEl.GetString() : null;
                if (id is null) continue;
                if (_pending.TryRemove(id, out var tcs))
                {
                    JsonNode? result = null;
                    if (root.TryGetProperty("result", out var resultEl))
                    {
                        result = JsonNode.Parse(resultEl.GetRawText());
                        tcs.TrySetResult(result);
                    }
                    else if (root.TryGetProperty("error", out var errEl))
                    {
                        var msg = errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "MCP error";
                        tcs.TrySetException(new InvalidOperationException(msg ?? "MCP error"));
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException("Respuesta MCP inválida"));
                    }
                }
            }
            catch (Exception ex)
            {
                // No podemos mapear a una petición específica: ignorar línea o loggear
                _ = ex; // noop
            }
        }
    }

    private async Task<JsonNode?> SendAsync(string method, JsonNode? @params = null, CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var id = Guid.NewGuid().ToString("N");
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params
        };

        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var json = payload.ToJsonString(JsonOptions);
        await _stdin!.WriteLineAsync(json);
        await _stdin.FlushAsync();

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task.ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpResourceDto>> ListAsync(CancellationToken ct = default)
    {
        var node = await SendAsync("resources/list", null, ct);
        var list = new List<McpResourceDto>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is null) continue;
                var uri = item["uri"]?.GetValue<string>() ?? string.Empty;
                var name = item["name"]?.GetValue<string>() ?? string.Empty;
                var mt = item["mimeType"]?.GetValue<string>();
                list.Add(new McpResourceDto(uri, name, mt));
            }
        }
        return list;
    }

    public async Task<string> ReadTextAsync(string uri, CancellationToken ct = default)
    {
        var p = new JsonObject { ["uri"] = uri };
        var node = await SendAsync("resources/read", p, ct);
        return node?["text"]?.GetValue<string>() ?? string.Empty;
    }

    public async Task<string> WriteAsync(string relativePath, string text, CancellationToken ct = default)
    {
        var p = new JsonObject { ["relativePath"] = relativePath, ["text"] = text };
        var node = await SendAsync("resources/write", p, ct);
        return node?["uri"]?.GetValue<string>() ?? string.Empty;
    }

    public async Task AppendAsync(string uri, string text, CancellationToken ct = default)
    {
        var p = new JsonObject { ["uri"] = uri, ["text"] = text };
        _ = await SendAsync("resources/append", p, ct);
    }

    public async Task<bool> DeleteAsync(string uri, string? approvalToken = null, CancellationToken ct = default)
    {
        var p = new JsonObject { ["uri"] = uri };
        if (!string.IsNullOrWhiteSpace(approvalToken)) p["approvalToken"] = approvalToken;
        var node = await SendAsync("resources/delete", p, ct);
        return node?["ok"]?.GetValue<bool>() ?? false;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_readerCts is not null)
            {
                _readerCts.Cancel();
                _readerCts.Dispose();
            }
            if (_proc is { HasExited: false })
            {
                try { _proc.Kill(entireProcessTree: true); }
                catch { /* ignore */ }
            }
            _stdin?.Dispose();
            _proc?.Dispose();
            if (_readerTask is not null) await Task.WhenAny(_readerTask, Task.Delay(250));
        }
        catch { /* ignore */ }
    }
}
