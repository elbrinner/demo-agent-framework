import { useEffect, useRef, useState } from 'react';

export interface SseEvent<T = any> {
  type: string;
  stepId?: string | null;
  data: T;
  traceId?: string | null;
  spanId?: string | null;
  ts?: number | null; // timestamp milisegundos (si backend lo envía)
}

export function useSse() {
  const esRef = useRef<EventSource | null>(null);
  const [events, setEvents] = useState<SseEvent[]>([]);
  const [connected, setConnected] = useState(false);
  const [lastEventAt, setLastEventAt] = useState<number | null>(null);
  const [done, setDone] = useState(false);

  const connect = (url: string) => {
    disconnect();
    const es = new EventSource(url);
    esRef.current = es;
    // connected se establecerá en onopen

    const onAny = (e: MessageEvent) => {
      // Varios endpoints envían data como string o JSON
      let payload: any = e.data;
      try { payload = JSON.parse(e.data); } catch {}
      // Si viene nuestra envoltura (type,data,traceId,spanId)
      const ev: SseEvent = typeof payload === 'object' && payload?.type && payload?.data
        ? {
            type: payload.type,
            data: payload.data,
            stepId: payload.stepId ?? null,
            traceId: payload.traceId ?? null,
            spanId: payload.spanId ?? null,
            ts: typeof payload.ts === 'number' ? payload.ts : null
          }
        : { type: (e as any).type || 'message', data: payload, ts: Date.now() };
      setEvents(prev => [...prev, ev]);
      setLastEventAt(Date.now());

      if (ev.type === 'completed' || ev.type === 'complete') {
        // Cierre limpio del stream
        try { es.close(); } catch {}
        if (esRef.current === es) esRef.current = null;
        setConnected(false);
        setDone(true);
      }
    };

    // Suscribirse a eventos comunes del dominio (excluye 'error' del EventSource, que no es un mensaje de app)
    es.addEventListener('message', onAny as any);
    ['token','started','complete','completed','step_started','step_completed','workflow_started','workflow_completed']
      .forEach(t => es.addEventListener(t, onAny as any));

    es.onopen = () => {
      setConnected(true);
      setLastEventAt(Date.now());
      setDone(false);
    };
    es.onerror = () => {
      // Error de conexión o keep-alive; no lo contamos como evento de app
      setConnected(false);
      // Opcional: podríamos considerar reenviar un estado interno si se requiere en UI
    };
  };

  const disconnect = () => {
    if (esRef.current) {
      esRef.current.close();
      esRef.current = null;
      setConnected(false);
    }
  };

  useEffect(() => () => disconnect(), []);

  return { events, connected, lastEventAt, done, connect, disconnect, clear: () => { setEvents([]); setDone(false); } };
}
