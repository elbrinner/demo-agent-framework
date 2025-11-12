# Agente Básico

**¿Qué hace?**  
Recibe un prompt de texto y devuelve una respuesta generada por Azure OpenAI.

**Cómo funciona:**  
1. Configura cliente Azure OpenAI con credenciales  
2. Crea un agente básico con el modelo especificado  
3. Envía el prompt al agente y obtiene respuesta  
4. Retorna respuesta en formato JSON

**Ejemplo de uso:**  
```json
POST /bri-agent/agents/basic
{
  "prompt": "¿Qué es el 'Hola mundo' en programación?"
}
```

**Características:**  
- ✅ Respuesta directa del modelo  
- ✅ Sin herramientas adicionales  
- ✅ Formato API simple