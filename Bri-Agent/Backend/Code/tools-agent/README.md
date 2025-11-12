# Agente con Herramientas (Function Calling)

**¿Qué hace?**  
Demuestra function calling básico: el agente puede usar herramientas individuales.

**Cómo funciona:**  
1. **Registro de tools**: Define funciones disponibles (climate, currency, etc.)  
2. **Detección automática**: LLM decide cuándo usar cada tool  
3. **Ejecución secuencial**: Framework ejecuta la tool solicitada  
4. **Respuesta integrada**: LLM incorpora resultado en respuesta final

**Herramientas disponibles:**  
- `climate`: Clima de una ciudad  
- `currency`: Conversión EUR→USD  
- `summary`: Resumen de texto  
- `worldtime`: Hora local  
- `sentiment`: Análisis de sentimiento  
- `dish`: Plato típico por ciudad

**Ejemplo de uso:**  
```json
POST /bri-agent/agents/tools
{
  "question": "qué tiempo hace en Madrid?",
  "tools": ["climate"]
}
```

**Características:**  
- ✅ Function calling nativo  
- ✅ Una tool por consulta  
- ✅ Control manual de tools disponibles