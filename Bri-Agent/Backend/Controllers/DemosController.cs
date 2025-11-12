using Microsoft.AspNetCore.Mvc;
using BriAgent.Backend.DemosRuntime;
using System.Text;
using BriAgent.Backend.Models;
using System.IO;

namespace BriAgent.Backend.Controllers;

[ApiController]
[Route("bri-agent/demos")] // sin barra inicial para no duplicar en swagger
public class DemosController : ControllerBase
{
    [HttpGet("list")]
    public IActionResult List()
    {
        var items = DemoRegistry.Demos.Select(d => {
            var id = d.Id;
            var isOllama = string.Equals(id, "ollama", StringComparison.OrdinalIgnoreCase);
            var isAiFoundry = string.Equals(id, "aifoundry", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "ai-foundry", StringComparison.OrdinalIgnoreCase);
            var mode = isOllama ? "streaming" : InferModeFromId(id);
            var stream = isOllama || isAiFoundry || id.Contains("stream") || id.Contains("thread") || id.Contains("workflow");
            var history = id.Contains("thread");
            var recommended = isOllama || isAiFoundry ? "StreamingViewer" : InferRecommendedView(id);
            var caps = InferCapabilities(id).ToList();
            if (isOllama && !caps.Contains("streaming")) caps.Add("streaming");
            if (isAiFoundry && !caps.Contains("streaming")) caps.Add("streaming");

            return new
            {
                id = d.Id,
                title = d.Title,
                description = d.Description,
                tags = d.Tags.ToArray(),
                meta = new UiMeta(
                    version: "v1",
                    demoId: d.Id,
                    controller: nameof(DemosController),
                    ui: new UiProfile(
                        mode: mode,
                        stream: stream,
                        history: history,
                        recommendedView: recommended,
                        capabilities: caps
                    )
                )
            } as object;
        }).ToList();

       

    return Ok(items);
    }

    [HttpGet("{id}/code")] // GET /bri-agent/demos/{id}/code
    public IActionResult Code(string id)
    {
        // 1) Intentar carpeta Bri-Agent/Backend/Code/{id}
        //    Mapeos amistosos: bri-basic-agent -> hola-mundo
        var backendRoot = ResolveBackendRoot();
        var codeDir = Path.Combine(backendRoot, "Code", id);
        if (!Directory.Exists(codeDir) && id == "bri-basic-agent")
        {
            var alias = Path.Combine(backendRoot, "Code", "hola-mundo");
            if (Directory.Exists(alias)) codeDir = alias;
        }

        if (Directory.Exists(codeDir))
        {
            // Si la carpeta existe pero está vacía, dejamos que el fallback persista los archivos
            if (Directory.GetFiles(codeDir, "*", SearchOption.AllDirectories).Length == 0)
            {
                // continuar al fallback para persistir
            }
            else
            {
                var tree = BuildCodeTree(codeDir, baseDir: codeDir);
            // Flat compatibility list
            var flatFiles = new List<object>();
            void Collect(dynamic node, string baseRel)
            {
                if (node.kind == "file")
                {
                    string relPath = node.path;
                    var effectiveId = id == "bri-basic-agent" && codeDir.EndsWith(Path.Combine("Code","hola-mundo")) ? "hola-mundo" : id;
                    flatFiles.Add(new { path = Path.Combine("Bri-Agent","Backend","Code", effectiveId, relPath).Replace('\\','/'), found = true, language = InferLanguage(relPath), content = (string?)node.content });
                }
                else if (node.kind == "dir")
                {
                    foreach (var child in node.children)
                        Collect(child, baseRel);
                }
            }
            foreach (var n in tree) Collect(n, "");
            return Ok(new { demo = id, files = flatFiles, tree, persisted = false, origin = "code" });
            }
        }

    // 2) Fallback: lista real desde el registro dinámico
        var demo = DemoRegistry.Demos.FirstOrDefault(d => d.Id == id);
        if (demo == null) return NotFound(new { error = "Demo no encontrada" });

        var files = new List<object>();
        foreach (var rel in demo.SourceFiles)
        {
            try
            {
                var full = ResolveRepoRelative(rel);
                if (!System.IO.File.Exists(full))
                {
                    files.Add(new { path = rel, found = false });
                    continue;
                }
                var content = System.IO.File.ReadAllText(full, Encoding.UTF8);
                files.Add(new { path = rel, found = true, language = InferLanguage(rel), content });
            }
            catch (Exception ex)
            {
                files.Add(new { path = rel, found = false, error = ex.Message });
            }
        }
        // 3) Persistir copia en Bri-Agent/Backend/Code/{id} si no existe para futuras respuestas y evitar depender de rutas originales
        try
        {
            var persistDir = Path.Combine(backendRoot, "Code", id);
            if (!Directory.Exists(persistDir))
            {
                Directory.CreateDirectory(persistDir);
                foreach (var f in files.Where(f => (bool?)f.GetType().GetProperty("found")?.GetValue(f) == true))
                {
                    var pathProp = f.GetType().GetProperty("path")?.GetValue(f) as string;
                    var contentProp = f.GetType().GetProperty("content")?.GetValue(f) as string;
                    if (pathProp == null || contentProp == null) continue;
                    // Mantener estructura creando subdirectorios según la ruta relativa original
                    var safeRelative = pathProp.Replace('\\','/');
                    // Evitar que rutas absolutas o que salgan de la raíz se copien
                    if (Path.IsPathRooted(safeRelative) || safeRelative.Contains("..")) continue;
                    var targetPath = Path.Combine(persistDir, safeRelative);
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    System.IO.File.WriteAllText(targetPath, contentProp, Encoding.UTF8);
                }
                // Releer desde Code/{id} para uniformidad de respuesta futura
                var tree = BuildCodeTree(persistDir, baseDir: persistDir);
                var flatFiles = new List<object>();
                void Collect2(dynamic node, string baseRel)
                {
                    if (node.kind == "file")
                    {
                        string relPath = node.path;
                        flatFiles.Add(new { path = Path.Combine("Bri-Agent","Backend","Code", id, relPath).Replace('\\','/'), found = true, language = InferLanguage(relPath), content = (string?)node.content });
                    }
                    else if (node.kind == "dir")
                    {
                        foreach (var child in node.children)
                            Collect2(child, baseRel);
                    }
                }
                foreach (var n in tree) Collect2(n, "");
                return Ok(new { demo = id, files = flatFiles, tree, persisted = true, origin = "code" });
            }
        }
        catch (Exception ex)
        {
            // Si falla la persistencia, devolvemos el listado original con el error añadido
            return Ok(new { demo = demo.Id, files, persistenceError = ex.Message, persisted = false, origin = "source" });
        }

        return Ok(new { demo = demo.Id, files, persisted = false, origin = "source" });
    }

    private static string InferModeFromId(string id)
        => id switch
        {
            var s when s.Contains("thread") => "thread",
            var s when s.Contains("stream") => "streaming",
            var s when s.Contains("workflow") => "workflow",
            var s when s.Contains("tool") => "tools",
            var s when s.Contains("struct") => "structured",
            _ => "single"
        };

    private static string? InferRecommendedView(string id)
        => id.Contains("workflow") ? "WorkflowRun"
         : id.Contains("thread") ? "ThreadChat"
         : id.Contains("stream") ? "StreamingViewer"
         : null;

    private static IEnumerable<string> InferCapabilities(string id)
    {
        var caps = new List<string>();
        if (id.Contains("stream")) caps.Add("streaming");
        if (id.Contains("thread")) caps.Add("memory");
        if (id.Contains("tool")) caps.Add("tools");
        if (id.Contains("struct")) caps.Add("structured");
        if (id.Contains("workflow")) caps.Add("workflow");
        if (id.Equals("ollama", StringComparison.OrdinalIgnoreCase)) caps.Add("streaming");
        if (id.Equals("aifoundry", StringComparison.OrdinalIgnoreCase) || id.Equals("ai-foundry", StringComparison.OrdinalIgnoreCase)) caps.Add("streaming");
        return caps;
    }

    private static string ResolveBackendRoot()
    {
        // Subir desde el bin hasta encontrar el csproj del backend
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "BriAgent.Backend.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // Fallback: intentar por convención desde la raíz del repo
        var repo = ResolveRepoRoot();
        var candidate = Path.Combine(repo, "Bri-Agent", "Backend");
        if (Directory.Exists(candidate)) return candidate;
        return repo;
    }

    private static string InferLanguage(string path)
        => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".tsx" => "tsx",
            ".ts" => "ts",
            ".js" => "javascript",
            _ => "text"
        };

    private static string ResolveRepoRelative(string relative)
    {
        var root = ResolveRepoRoot();
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
    }

    private static string ResolveRepoRoot()
    {
        // Detectar raíz buscando el .sln
        var dir = System.IO.Directory.GetParent(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "demo-agent-framework.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    private static List<dynamic> BuildCodeTree(string currentDir, string baseDir)
    {
        var nodes = new List<dynamic>();
        foreach (var dir in Directory.GetDirectories(currentDir))
        {
            var name = Path.GetFileName(dir);
            var child = new {
                name,
                path = Path.GetRelativePath(baseDir, dir).Replace('\\','/'),
                kind = "dir",
                children = BuildCodeTree(dir, baseDir)
            };
            nodes.Add(child);
        }
        foreach (var file in Directory.GetFiles(currentDir))
        {
            var name = Path.GetFileName(file);
            var rel = Path.GetRelativePath(baseDir, file).Replace('\\','/');
            string content = System.IO.File.ReadAllText(file, Encoding.UTF8);
            nodes.Add(new { name, path = rel, kind = "file", language = InferLanguage(rel), content });
        }
        // Ordenar: dir primero, luego archivo por nombre
        nodes.Sort((a,b) => string.Compare((string)a.kind,(string)b.kind,StringComparison.Ordinal) != 0
            ? string.Compare((string)a.kind,(string)b.kind,StringComparison.Ordinal)
            : string.Compare((string)a.name,(string)b.name,StringComparison.Ordinal));
        return nodes;
    }
}
