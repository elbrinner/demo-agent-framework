# Agente con Contexto (Thread)

**¿Qué hace?**  
Mantiene conversación continua con memoria de interacciones anteriores.

**Cómo funciona:**  
1. **Thread ID**: Cada conversación tiene identificador único  
2. **Historial persistente**: Almacena todas las interacciones  
3. **Contexto acumulado**: Cada mensaje incluye historial completo  
4. **Continuidad**: Respuestas consideran conversación previa

**Ejemplo de uso:**  
```json
// Primera interacción
POST /bri-agent/agents/thread/stream
{
  "message": "Hola, soy Juan",
  "threadId": null  // Nuevo thread
}

// Respuesta incluye threadId generado
{
  "threadId": "abc123",
  "response": "¡Hola Juan! ¿En qué puedo ayudarte?"
}

// Siguiente interacción
POST /bri-agent/agents/thread/stream
{
  "message": "¿Cuál es mi nombre?",
  "threadId": "abc123"  // Reutiliza thread
}
```

**Características:**  
- ✅ Memoria conversacional  
- ✅ Contexto persistente  
- ✅ Experiencia natural de chat