# Agente en Modo Streaming

**¿Qué hace?**  
Genera respuestas en tiempo real usando Server-Sent Events (SSE).

**Cómo funciona:**  
1. **Conexión SSE**: Cliente establece conexión persistente  
2. **Tokens progresivos**: Modelo envía tokens uno por uno  
3. **Eventos en tiempo real**: Cada token llega como evento separado  
4. **UI actualizable**: Frontend muestra respuesta mientras se genera

**Ejemplo de uso:**  
```javascript
// Frontend establece conexión
const eventSource = new EventSource('/bri-agent/agents/stream?prompt=Hola');

// Escucha eventos
eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  if (data.type === 'token') {
    appendToResponse(data.text);
  }
};
```

**Eventos SSE:**  
- `started`: Inicio con metadata  
- `token`: Cada fragmento de texto  
- `completed`: Fin con estadísticas  
- `error`: Errores durante generación

**Características:**  
- ✅ Respuesta en tiempo real  
- ✅ Mejor UX (no espera completa)  
- ✅ Streaming nativo de OpenAI