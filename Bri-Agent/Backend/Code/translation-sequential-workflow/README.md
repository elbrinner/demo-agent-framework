# Workflow de Traducción Secuencial

Traduce un texto en inglés a francés → portugués → alemán y luego un agente final devuelve una respuesta en inglés que incluye las traducciones intermedias. Emite eventos SSE para:

- workflow_started: inicio y entrada original
- step_started: arranque de cada agente (fr, pt, de, final)
- token: tokens generados por cada agente (streaming)
- step_completed: duración y salida por agente
- workflow_completed: tiempo total y resumen

