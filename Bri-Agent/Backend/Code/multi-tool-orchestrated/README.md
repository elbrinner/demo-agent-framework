# Multi-Tool con Orquestación Explícita

**¿Qué hace?**  
Detecta automáticamente herramientas necesarias, las ejecuta en paralelo y genera una respuesta integrada.

**Cómo funciona:**  
1. **Detección heurística**: Analiza el prompt con reglas fijas (keywords) para identificar tools  
2. **Ejecución paralela**: Corre todas las tools detectadas simultáneamente con `Task.WhenAll`  
3. **Contexto unificado**: Combina resultados de todas las tools en un solo contexto  
4. **Integración final**: Envía contexto al modelo UNA vez para generar respuesta natural

**Herramientas disponibles:**  
- `climate`: Clima de una ciudad  
- `currency`: Conversión EUR→USD  
- `summary`: Resumen de texto  
- `worldtime`: Hora local  
- `sentiment`: Análisis de sentimiento  
- `dish`: Plato típico por ciudad

**Ejemplo de uso:**  
```json
POST /bri-agent/demos/multi-tool-orchestrated/run
{
  "question": "qué tiempo hace en Madrid y qué hay para comer?"
}
```

**Ventajas:**  
- ✅ Determinista (no depende del LLM para detectar tools)  
- ✅ Paralelo (más rápido)  
- ✅ Control total del proceso  
- ✅ Una sola llamada al modelo final