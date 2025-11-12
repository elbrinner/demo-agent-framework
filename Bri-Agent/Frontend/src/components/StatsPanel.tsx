import React, { useMemo } from 'react';
import type { SseEvent } from '../hooks/useSse';

interface Props { events: SseEvent[]; model?: string | null; }

// Panel compacto para mostrar métricas de ejecución
export const StatsPanel: React.FC<Props> = ({ events, model }) => {
  const stats = useMemo(() => {
    let tokenEvents = 0;
    let tokenChars = 0;
    let firstAt: number | null = null;
    let lastAt: number | null = null;
    for (const e of events) {
      if (e.type === 'token') {
        tokenEvents++;
        const txt = typeof e.data === 'string' ? e.data : (e.data?.text ?? '');
        tokenChars += txt.length;
        const t = typeof e.ts === 'number' ? e.ts : Date.now();
        if (firstAt === null || t < firstAt) firstAt = t;
        if (lastAt === null || t > lastAt) lastAt = t;
      }
    }
    const durationMs = firstAt !== null && lastAt !== null ? (lastAt - firstAt) : 0;
    const seconds = durationMs > 0 ? durationMs / 1000 : 1;
    const rate = tokenEvents / seconds;
    return {
      tokenEvents,
      tokenChars,
      durationMs,
      rate: Number(rate.toFixed(1))
    };
  }, [events]);

  return (
    <div className="border border-gray-300 rounded-lg p-4 bg-white">
      <div className="font-semibold mb-3 text-gray-900">Métricas</div>
      <ul className="text-sm text-gray-700 space-y-1">
        <li><strong>Modelo:</strong> {model || 'N/D'}</li>
        <li><strong>Tokens (eventos):</strong> {stats.tokenEvents}</li>
        <li><strong>Chars acumulados:</strong> {stats.tokenChars}</li>
        <li><strong>Duración tokens:</strong> {stats.durationMs} ms</li>
        <li><strong>Tasa aprox:</strong> {stats.rate} tok/s</li>
      </ul>
    </div>
  );
};