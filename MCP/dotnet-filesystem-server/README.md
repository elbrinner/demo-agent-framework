# BriAgent MCP Filesystem Server (.NET)

Servidor MCP minimalista por stdio (línea a línea JSON) para listar/leer/escribir/append/borrar archivos bajo una carpeta permitida.

IMPORTANTE: Este servidor implementa un framing simplificado (una petición JSON por línea). Para integración MCP 100% estándar, cambia a framing con cabeceras y `content-length` o a una librería MCP para .NET.

## Handlers expuestos
- resources/list
- resources/read { uri }
- resources/write { relativePath, text }
- resources/append { uri, text }
- resources/delete { uri, approvalToken? }

## Variables de entorno
- MCP_FS_ALLOWED_PATH: ruta raíz permitida (por defecto: cwd)
- MCP_REQUIRE_TOKEN: "true" para exigir token en `resources/delete`
- MCP_APPROVAL_TOKEN: token válido para `resources/delete` si `MCP_REQUIRE_TOKEN=true`

## Ejecutar

```bash
export MCP_FS_ALLOWED_PATH="/Users/<usuario>/Documents/demo"
# opcional para delete protegido
# export MCP_REQUIRE_TOKEN=true
# export MCP_APPROVAL_TOKEN=secreto-123

# build
dotnet build MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj

# run
dotnet run --project MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj
```

## Probar manualmente (opcional)
En otra terminal, enviar JSON por línea (simulando cliente JSON-RPC simplificado):

```bash
# Listar
printf '{"jsonrpc":"2.0","id":"1","method":"resources/list"}\n' | dotnet run --project MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj

# Leer un archivo (ajusta la URI)
printf '{"jsonrpc":"2.0","id":"2","method":"resources/read","params":{"uri":"file:///Users/<usuario>/Documents/demo/README.txt"}}\n' | dotnet run --project MCP/dotnet-filesystem-server/BriAgent.McpServer.csproj
```

## Notas
- Se valida que toda ruta quede dentro de `MCP_FS_ALLOWED_PATH`.
- `resources/write` evita sobrescribir ficheros ya existentes.
- Para producción, recomienda usar una librería MCP/JSON-RPC con framing estándar y tests de interoperabilidad.
