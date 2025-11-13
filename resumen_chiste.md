# Resumen Fábrica de Chistes

## Estado Actual
- **Workflow de generación**: genera chistes, los puntúa, aplica revisión del jefe (HITL si score >=9) y almacena vía MCP (filesystem restringido a `~/Documents/jokes`).
- **Dedupe**: Normalización de texto en frontend para ocultar duplicados con variantes `(tema: ...) [v###]`.
- **Cancelación**: Endpoint `POST /api/jokes/stop/{workflowId}` detiene el workflow activo por `CancellationTokenSource`.
- **Eventos SSE**: `joke_generated`, `joke_scored`, `approval_required`, `joke_stored`, `joke_rejected`, `agent_action`, `workflow_completed`.
- **HITL**: Aprobación/Rechazo humano a través de `/api/jokes/approve` y `/api/jokes/reject`; rechazo humano NO persiste archivo.
- **Persistencia**: Archivos guardados solo por flujo validado (sin doble escritura). Nombre patrón `joke-{id}-{timestamp}.txt`.
- **Panel de consulta**: Buscar, contar, resumen clásico y resumen IA (con Azure OpenAI) sobre contenidos MCP.
- **Nuevos endpoints MCP**:
  - `GET /api/jokes/search?query=&limit=`: Búsqueda substring case-insensitive.
  - `GET /api/jokes/count`: Conteo de archivos.
  - `POST /api/jokes/ai-summary`: Resumen IA en español con fragmentos controlados (Limit + MaxChars).
  - `GET /api/jokes/best`: Extrae mejor chiste por score (parse en primera línea).
  - `GET /api/jokes/contents?limit=&maxCharsPerFile=`: Devuelve snippets legibles para UI.

## UI Actual (JokesFactory)
- Botones: Iniciar Workflow, Detener Workflow, Pausar/Reanudar Stream, Reset.
- Panel de consulta: Buscar, Contar, Resumen, Resumen IA, Leer todo, Mejor chiste.
- Render de chistes: tabla con estados y acciones; ocultación de duplicados normalizados; botón Remover para estados no almacenados.
- Panel de logs: muestra acciones recientes (search, count, ai-summary, read-all, best).
- Panel de archivos: snippets plegables `<details>` con link a archivo completo.

## Logs y Observabilidad
- Frontend: console + panel interno (timestamps ISO + acción).
- Backend: OpenTelemetry console exporter para SSE y HTTP.

## Lo Pendiente / Próximos Pasos
1. **Mejorar dedupe backend** (opcional): evitar generar chistes conceptualmente iguales antes de llegar al frontend.
2. **Filtros avanzados en búsqueda**: por score mínimo, rango de fechas, sólo HITL aprobados.
3. **Resumen IA contextual**: permitir prompt adicional del usuario para foco temático.
4. **Paginación / streaming de contents**: si hay muchos archivos, cargar por lotes.
5. **Exportación CSV/JSON** de chistes guardados y métricas.
6. **Tests adicionales**: cobertura para `best`, `contents`, `ai-summary` (mock de Azure OpenAI si se requiere).
7. **Seguridad**: Sanitizar aún más el contenido antes del prompt IA (eliminar tokens potencialmente sensibles).
8. **Ranking avanzado**: Modelo que considere variabilidad del texto, no sólo score numérico inicial.
9. **Métricas agregadas**: Endpoint para distribuciones (histograma de scores, % HITL, etc.).
10. **UI estado completado**: Badge o banner específico cuando workflow termina (ya se pausa stream, pero mejorar UX).

## Uso de Resumen IA
- Enviar POST a `/api/jokes/ai-summary` con body: `{ "Query": "byte", "Limit": 10, "MaxChars": 4000 }`.
- El sistema compone prompt consolidando snippets y devuelve `summary`.

## Mejor Chiste
- Endpoint `GET /api/jokes/best` recorre archivos, parsea `score=` en línea inicial y retorna el mayor.

## Snippets de Contenido
- `GET /api/jokes/contents?limit=40&maxCharsPerFile=800` alimenta panel plegable para inspección rápida.

## Variables de Entorno Requeridas
- `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_KEY`, `AZURE_OPENAI_MODEL` para resumen IA.
- `MCP_FS_ALLOWED_PATH` se fija automáticamente a `~/Documents/jokes` si no existe.

## Consideraciones de Escalabilidad
- Lectura secuencial de archivos: Para muchos (>500) se necesita paginación + pipeline async.
- Límites prompt IA: Se corta cada archivo a 1200 chars y total acumulado por `MaxChars`.

## Calidad y Pruebas
- Suite xUnit existente para flujo principal e HITL; faltan pruebas para nuevos endpoints.
- Recomendación: Mock `AzureOpenAIClient` y MCP en tests de `ai-summary`.

## Conclusión
La fábrica de chistes permite generación controlada, revisión humana, persistencia limitada y ahora exploración y resumen inteligente de los archivos almacenados con herramientas MCP + Azure OpenAI. Próximo foco: profundizar en análisis, ranking avanzado y testear nuevos endpoints.
