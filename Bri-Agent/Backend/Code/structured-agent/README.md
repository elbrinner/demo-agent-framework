# Agente con Salida Estructurada

**¿Qué hace?**  
Genera respuestas en formato JSON estructurado usando esquemas definidos.

**Cómo funciona:**  
1. **Esquema definido**: Usa `PersonInfo` class con propiedades específicas  
2. **Instrucciones claras**: Prompt guía al modelo para devolver JSON válido  
3. **Validación automática**: Framework valida formato de respuesta  
4. **Objeto tipado**: Convierte JSON a objeto C# fuertemente tipado

**Ejemplo de uso:**  
```json
POST /bri-agent/demos/structured-agent/run
{
  "prompt": "Juan Pérez, 30 años, programador, sabe C# y Python"
}
```

**Respuesta esperada:**  
```json
{
  "name": "Juan Pérez",
  "age": 30,
  "occupation": "programador",
  "skills": ["C#", "Python"]
}
```

**Características:**  
- ✅ Salida predecible y parseable  
- ✅ Validación automática  
- ✅ Integración fácil con código C#