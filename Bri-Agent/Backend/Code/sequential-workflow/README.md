# Workflow Secuencial

**¿Qué hace?**  
Ejecuta pasos de procesamiento uno después del otro con streaming.

**Cómo funciona:**  
1. **Pasos ordenados**: Workflow con steps dependientes  
2. **Ejecución secuencial**: Cada step espera al anterior  
3. **Streaming progresivo**: Eventos SSE muestran progreso  
4. **Resultado acumulado**: Output de cada step alimenta al siguiente

**Ejemplo de flujo:**  
```
Input → Step A → Output A → Step B → Output B → Step C → Resultado Final
```

**Características:**  
- ✅ Dependencias claras  
- ✅ Procesamiento paso a paso  
- ✅ Streaming progresivo  
- ✅ Control total del orden