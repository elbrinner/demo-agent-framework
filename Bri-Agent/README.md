# Bri-Agent (Monorepo)

Estructura:

```
Bri-Agent/
  Backend/    # ASP.NET Core Web API (net9.0)
  Frontend/   # React + Vite + TypeScript
```

## Backend

- URL desarrollo: http://localhost:5080
- Endpoints listos:
  - GET `/bri-agent/health`
  - GET `/bri-agent/demos/list`
  - POST `/bri-agent/agents/basic` (requiere credenciales Azure OpenAI)

Variables de entorno (o archivo `.env` en raíz del repo):

- `AZURE_OPENAI_ENDPOINT`  (ej: https://<resource>.openai.azure.com)
- `AZURE_OPENAI_KEY`
- `AZURE_OPENAI_MODEL`     (deployment name)

## Frontend

- Dev server: http://localhost:5173
- Proxy: las rutas que comienzan por `/bri-agent` se redirigen al Backend.

## Notas de desarrollo

- CORS está habilitado para `http://localhost:5173`.
- En futuras iteraciones se migrará el sistema de demos dinámicas (`IApiDemo`) al Backend para que `demos/list` no sea placeholder.
