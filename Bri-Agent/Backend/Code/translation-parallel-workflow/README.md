# Workflow de Traducción Paralela

Traduce un texto en inglés simultáneamente a francés, portugués y alemán. Luego un agente final sintetiza las traducciones y devuelve una respuesta en inglés. Eventos SSE:

- workflow_started
- step_started (fr, pt, de, final)
- token (stream de cada traducción)
- step_completed (tiempo y tamaño)
- workflow_completed (duración total y resultado final)

