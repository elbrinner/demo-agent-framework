# Multi-Tool con Prompt Engineering

**¿Qué hace?**  
Usa instrucciones detalladas para que el LLM detecte y use múltiples herramientas automáticamente.

**Cómo funciona:**  
1. **Instrucciones reforzadas**: Prompt incluye "UTILIZA TODAS las herramientas relevantes"  
2. **Detección por LLM**: El modelo decide qué tools usar basado en el contexto  
3. **Ejecución automática**: Framework ejecuta las tools que el LLM solicita  
4. **Respuesta integrada**: LLM combina resultados en respuesta natural

**Herramientas disponibles:**  
- `climate`: Clima de una ciudad  
- `currency`: Conversión EUR→USD  
- `summary`: Resumen de texto  
- `worldtime`: Hora local  
- `sentiment`: Análisis de sentimiento  
- `dish`: Plato típico por ciudad

**Ejemplo de uso:**  
```json
POST /bri-agent/demos/multi-tool-prompt/run
{
  "question": "qué tiempo hace en Madrid y qué hay para comer?"
}
```

**Ventajas:**  
- ✅ Inteligente (LLM decide qué tools usar)  
- ✅ Flexible (adapta a consultas complejas)  
- ✅ Natural (respuesta integrada automáticamente)  
- ✅ Sin heurísticas manuales

**Desventajas:**  
- ❌ No determinista (puede olvidar tools)  
- ❌ Más lento (múltiples llamadas al modelo)