# Informe de Integración Agent Framework – Workflow de Chistes ("Fábrica de Chistes")

Fecha: 2025-11-12
Estado general: Evolucionado pero con oportunidades de estandarización y profundización en patrones avanzados (factory completa, extensiones A2A, métricas enriquecidas, robustez HITL, pruebas de continuidad).

---
## 1. Resumen Ejecutivo
El workflow de chistes ya incorpora:
- Orquestación A2A básica (agente generador → revisor → jefe) usando `AgentRunner` para instrumentar cada paso.
- Herramientas MCP filesystem (persistencia de chistes, listado, resumen) encapsuladas como `AIFunction` vía `JokesTools`.
- Checkpointing con pausa para intervención humana (HITL) y reanudación tras aprobación.
- Uso de la abstracción de agentes del Agent Framework para llamadas a Azure OpenAI (no invocaciones REST directas).

Quedan por mejorar:
- Centralización completa de creación y configuración de agentes (factory genérica con políticas: retries, timeouts, temperatura estratégica por tipo de agente, separación de tool sets).
- Extensión de telemetría (tokens, costo estimado, distribución de latencias, correlación con eventos HITL y MCP).
- Profundizar en A2A: mensajes estructurados, roles y transferencia explícita de "context envelope" (memoria compartida limitada).
- Pruebas adicionales: continuidad HITL, reejecuciones idempotentes, estrés sobre directorio MCP compartido y validación de no duplicidad de chistes.
- Observabilidad: panel enriquecido de spans por fase (generator/reviewer/boss) + KPIs (ratio auto vs hitl, score medio, tiempo hasta aprobación humana).

---
## 2. Componentes Clave Actuales
| Componente | Rol | Observaciones |
|------------|-----|---------------|
| `JokesWorkflowService` | Orquestación end-to-end | Usa `AgentRunner` en cada fase; lógica de deduplicación; checkpoint HITL. |
| `JokesController` | API/SSE | Inicia workflow, expone estado, aprobar/rechazar, búsquedas y resúmenes. |
| `JokesTools` | Conjunto de herramientas MCP | `store_joke`, `list_jokes`, `summarize_jokes`; faltan tools de validación semántica, limpieza, búsqueda avanzada. |
| `JokesEventBus` | Canal interno de eventos | Emite progreso y transiciones; se puede enriquecer con payload telemétrico. |
| `AgentRunner` | Instrumentación ejecución | Captura duración y tamaños; falta tokens, costo, status avanzado. |
| `AgentFactory` (parcial) | Creación agentes | Debe consolidar políticas y perfiles (generator/reviewer/boss). |
| MCP FileSystem Server | Persistencia | Restricción a `~/Documents/jokes`; podría añadir cuotas / locking. |

---
## 3. Flujo A2A Actual
1. Generador produce chiste (prompt creativo).  
2. Revisor evalúa el texto y produce score (tool `validate_joke` si existe / ampliable).  
3. Jefe decide ruta: auto-aprobación o requerir HITL.  
4. En caso HITL → checkpoint (estado pausado) y espera aprobación/rechazo vía API.  
5. Tras aprobación, persistencia en MCP y emisión de evento final.

Mejoras propuestas:
- Estandarizar intercambio con estructura: `{id, text, score?, decision?, metadata{...}}`.
- Añadir fase opcional de mitigación (si score bajo, generador recibe feedback del revisor y reintenta).
- Incorporar "policy agent" para decidir número de reintentos según métricas históricas.

---
## 4. Checkpointing & HITL
Actual: Pausa cuando jefe marca "hitl"; ApprovalId gestionado y se limpia tras aprobar.
Pendiente / Mejoras:
- Persistir snapshot serializado (json) para resiliencia ante reinicios.
- Añadir expiración configurable de aprobaciones (auto-cancel si excede umbral de tiempo). 
- Registro de motivo de rechazo y reintento controlado (limit rejections → auto archivado). 
- Métrica: tiempo medio en estado HITL, ratio de rechazos vs aprobaciones.

---
## 5. Integración MCP Filesystem
Ya se usan herramientas para lectura/escritura bajo directorio acotado.
Brechas:
- Falta control de colisiones de nombre más sofisticado (hash de contenido + índice). 
- No hay validación de tamaño ni sanitización adicional. 
- Falta tool de búsqueda semántica (embedding + índice local). 
- No hay metadata store (solo archivos plano). Considerar un JSON index (id, score, fecha, decisión).

---
## 6. Uso de Azure OpenAI vía Agent Framework
Estado: Se llama mediante agentes encapsulados (no REST manual). 
Mejoras:
- Ajustar parámetros por tipo: generator (temperatura alta), reviewer (baja, top_p reducido), boss (muy baja, max tokens menor). 
- Introducir límites de tokens por fase y fallback de modelo (p.ej. modelo rápido para pre-filtrados). 
- Añadir guardrails: clasificación de contenido antes de persistir (tool `moderate_content`).

---
## 7. Telemetría y Observabilidad
Actual: duración y tamaño de prompt/respuesta; Activity spans por fase.
Faltante:
- Tokens usados (prompt/completion). 
- Coste estimado acumulado por workflow. 
- Estados detallados (pending, paused_hitl, approved, rejected, stored). 
- Enlace a errores (excepciones por fase, reintentos). 
- Dashboard agregado: KPIs (auto vs hitl %, score promedio, latencia promedio por fase). 

---
## 8. Brechas y Recomendaciones Concretas
| Categoría | Brecha | Recomendación | Prioridad |
|-----------|--------|---------------|----------|
| Factory | Config dispersa | Consolidar perfiles en `AgentFactory` | Alta |
| Herramientas | Validación limitada | Añadir tools: `moderate_content`, `semantic_search`, `retry_generation` | Media |
| HITL | Sin expiración/apelación | Implementar TTL y razón de rechazo + flujo de reintento | Alta |
| Telemetría | Sin tokens/costo | Instrumentar recuento tokens y coste (SDK) | Alta |
| Persistencia | Solo archivos planos | Crear `index.json` y tool para mantenimiento | Media |
| Resiliencia | Falta snapshot durable | Guardar estado en JSON al pausar (checkpoint) | Alta |
| A2A | Intercambio textual | Estructurar mensajes con schema validado | Media |
| Testing | Cobertura incompleta | Añadir tests de reintento, rechazo, expiración HITL, deduplicación | Alta |
| Seguridad | Sin moderación | Tool de moderación + rechazo automático | Alta |

---
## 9. Plan de Implementación Incremental
1. (Día 1-2) Extender `AgentFactory` con perfiles: parámetros (temperature, top_p, max_tokens) y tool sets por rol.  
2. (Día 2-3) Instrumentar tokens y coste en `AgentRunner` (lectura de usage en respuesta del modelo).  
3. (Día 3) Introducir índice `jokes/index.json` + tool `list_index` / `search_index`.  
4. (Día 4) Implementar expiración HITL y snapshot JSON persistente; actualizar tests.  
5. (Día 5) Moderación y guardrails (rechazo automático si infringe políticas).  
6. (Día 6) A2A enriquecido: comunicación estructurada + reintentos condicionales (policy agent).  
7. (Día 7) Dashboard telemetría (endpoint /diagnostics/jokes extendido).  
8. (Día 8) Suite de pruebas completa (stress + edge cases).  

---
## 10. Métricas Clave Objetivo
- Latencia promedio por fase: Generador < 2.5s, Revisor < 1.5s, Jefe < 1s. 
- % Auto-aprobaciones ≥ 60% (indica buena calidad inicial). 
- Ratio de chistes duplicados < 2%. 
- Tiempo medio en HITL < 90s. 
- Coste por chiste ≤ objetivo presupuestal (definir según modelo). 

---
## 11. Riesgos y Mitigaciones
| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| Acumulación de archivos | Espacio y performance | Límite + rotación + compresión index | Implementar index y limpieza programada |
| Revisor sesgado | Score distorsionado | Ajuste dinámico usando histórico estadístico | Métrica de varianza | 
| HITL pendiente larga | Bloqueo de flujo | TTL y auto-cancel | Notificaciones | 
| Coste impredecible | Exceso presupuesto | Telemetría tokens/costo + alertas | Optimizar parámetros | 
| Contenido inadecuado | Riesgo reputacional | Moderación automática antes de persistir | Tool `moderate_content` |

---
## 12. Próximos Pasos Inmediatos
1. Implementar perfiles en `AgentFactory` y adaptar `JokesWorkflowService` para solicitar agentes vía perfil (el servicio ya parcialmente lo hace, consolidar). 
2. Añadir extracción de tokens/costo a `AgentRunner`. 
3. Crear `index.json` y migrar lógica de deduplicación a uso de hash + index. 
4. Añadir TTL al Approval y snapshot persistente. 

---
## 13. Resumen Final
La "Fábrica de Chistes" está en una fase madura inicial: ya utiliza conceptos del Agent Framework (agentes diferenciados, runner, tools MCP, checkpoint HITL). El valor siguiente está en profesionalizar la fábrica: parametrización fina de agentes, telemetría económica, robustez HITL y un índice con semántica; esto habilita escalabilidad y calidad consistente.

---
## 14. Apéndice – Ejemplo de Perfil de Agente (Propuesto)
```csharp
public record AgentProfile(
    string Name,
    string Model,
    float Temperature,
    float TopP,
    int MaxOutputTokens,
    IReadOnlyList<AIFunction> Tools);

// Ejemplo para "generator":
var generatorProfile = new AgentProfile(
    Name: "jokes.generator",
    Model: modelCfg.CreativeModel,
    Temperature: 0.9f,
    TopP: 0.95f,
    MaxOutputTokens: 512,
    Tools: new[] { JokesTools.StoreJoke, JokesTools.ListJokes }
);
```

---
## 15. Glosario Breve
- A2A: Agente-a-Agente, comunicación encadenada estructurada. 
- HITL: Human-In-The-Loop, intervención humana en decisiones críticas. 
- MCP: Model Context Protocol, capa estandarizada para herramientas externas (filesystem en este caso). 
- Checkpoint: Estado persistible del flujo para reanudación confiable.

---
Fin del informe.
