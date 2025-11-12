# Workflow Paralelo

**¿Qué hace?**  
Ejecuta múltiples pasos de procesamiento simultáneamente con streaming.

**Cómo funciona:**  
1. **Pasos definidos**: Workflow con steps independientes  
2. **Ejecución paralela**: Todos los steps corren al mismo tiempo  
3. **Streaming combinado**: Eventos SSE de todos los steps  
4. **Resultado final**: Combina outputs de todos los steps

**Ejemplo de flujo:**  
```
Input → [Step A] → Output A
       → [Step B] → Output B  
       → [Step C] → Output C
       
Output A + B + C → Resultado Final
```

**Características:**  
- ✅ Procesamiento paralelo  
- ✅ Más eficiente que secuencial  
- ✅ Streaming en tiempo real  
- ✅ Combinación automática de resultados