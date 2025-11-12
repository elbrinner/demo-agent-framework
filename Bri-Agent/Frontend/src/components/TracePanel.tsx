import React, { useMemo, useState } from 'react';
import type { SseEvent } from '../hooks/useSse';

export interface TracePanelProps { events: SseEvent[]; recent?: { traceId?: string|null; spanId?: string|null; promptChars?: number; responseChars?: number; durationMs?: number; model?: string|null }; }

type Row = {
  idx: number;
  type: string;
  text: string;
  len: number;
  deltaMs: number | null;
  spanId: string | null | undefined;
  stepId: string | null | undefined;
  ts: number | null | undefined;
};
type Group = {
  traceId: string;
  rows: Row[];
  summary: { tokens: number; chars: number; duration: number | null };
  isFallback?: boolean;
  fallbackSpanId?: string | null;
};

export const TracePanel: React.FC<TracePanelProps> = ({ events, recent }) => {
  const [rawView, setRawView] = useState(false);

  const groups = useMemo<Group[]>(() => {
    const byTrace = new Map<string, SseEvent[]>();
    for (const ev of events) {
      const key = ev.traceId || 'sin-traza';
      if (!byTrace.has(key)) byTrace.set(key, []);
      byTrace.get(key)!.push(ev);
    }
    const result: Group[] = Array.from(byTrace.entries()).map(([traceId, evs]) => {
      // ordenar por ts si existe
      const sorted = [...evs].sort((a,b) => (a.ts ?? 0) - (b.ts ?? 0));
      let prevTs: number | null = null;
      let cum = 0;
      const rows: Row[] = sorted.map((e, i) => {
        const txt = e.type === 'token' ? (typeof e.data === 'string' ? e.data : (e.data?.text ?? '')) : '';
        const len = txt.length;
        cum += len;
        const d = (e.ts && prevTs) ? (e.ts - prevTs) : null;
        prevTs = e.ts ?? prevTs;
        return { idx: i+1, type: e.type, text: txt, len, deltaMs: d, spanId: e.spanId, stepId: e.stepId, ts: e.ts };
      });
      const tokens = rows.filter(r => r.type === 'token').length;
      const chars = rows.reduce((n,r)=> n + r.len, 0);
      const start = sorted.find(e => e.ts)?.ts ?? null;
      const end = [...sorted].reverse().find(e => e.ts)?.ts ?? null;
      const duration = (start && end) ? (end - start) : null;
      return { traceId, rows, summary: { tokens, chars, duration }, isFallback: false };
    });
    return result.length ? result : (recent?.traceId ? [{ traceId: recent!.traceId!, rows: [], summary: { tokens: 0, chars: recent!.responseChars ?? 0, duration: recent!.durationMs ?? null }, isFallback: true, fallbackSpanId: recent!.spanId ?? null }] : []);
  }, [events, recent]);

  const hasAny = groups.length > 0;

  return (
    <div className="border border-gray-300 rounded-lg p-4 bg-white">
      <div className="flex items-center justify-between mb-3">
        <div className="font-semibold text-gray-900">Trazas</div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setRawView(v => !v)}
            className="text-xs px-2 py-1 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-md"
          >{rawView ? 'Vista tabla' : 'Vista cruda'}</button>
        </div>
      </div>

      {!hasAny && <div className="text-sm text-gray-500 italic">Sin eventos de traza todavía…</div>}

      {groups.map(g => (
        <div key={g.traceId} className="mb-4 border rounded-md border-gray-200">
          <div className="flex items-center gap-2 flex-wrap px-3 py-2 bg-gray-50 border-b">
            <span className="px-2 py-1 rounded-md bg-gray-100 text-gray-800 font-mono text-xs">
              {g.traceId === 'sin-traza' ? g.traceId : `${g.traceId.substring(0, 18)}…`}
            </span>
            {g.traceId !== 'sin-traza' && (
              <button
                onClick={() => navigator.clipboard.writeText(g.traceId!)}
                className="text-xs px-2 py-1 bg-blue-500 hover:bg-blue-600 text-white rounded-md transition-colors"
              >Copiar traceId</button>
            )}
            <span className="text-xs text-gray-600">
              tokens {g.summary.tokens} · chars {g.summary.chars}{g.summary.duration != null ? ` · duración ${g.summary.duration} ms` : ''}
              {g.isFallback && recent?.promptChars !== undefined && recent?.responseChars !== undefined && (
                <> · prompt {recent.promptChars} ch · respuesta {recent.responseChars} ch{recent.model ? ` · modelo ${recent.model}` : ''}</>
              )}
            </span>
            <button
              onClick={() => navigator.clipboard.writeText(JSON.stringify(events.filter(e=> (e.traceId||'sin-traza')===g.traceId), null, 2))}
              className="ml-auto text-xs px-2 py-1 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-md"
            >Copiar JSON</button>
          </div>

          {!rawView ? (
            <div className="overflow-x-auto">
              <table className="min-w-full text-xs">
                <thead>
                  <tr className="text-left text-gray-600 border-b">
                    <th className="py-2 px-3">#</th>
                    <th className="py-2 px-3">tipo</th>
                    <th className="py-2 px-3">token</th>
                    <th className="py-2 px-3">len</th>
                    <th className="py-2 px-3">+ms</th>
                    <th className="py-2 px-3">span</th>
                    <th className="py-2 px-3">ts</th>
                  </tr>
                </thead>
                <tbody>
                  {g.rows.length === 0 && (
                    <tr className="border-b last:border-b-0">
                      <td className="py-1 px-3 whitespace-nowrap">—</td>
                      <td className="py-1 px-3 whitespace-nowrap">completed</td>
                      <td className="py-1 px-3 font-mono text-[11px] text-gray-600">(respuesta básica sin SSE)</td>
                      <td className="py-1 px-3 whitespace-nowrap">—</td>
                      <td className="py-1 px-3 whitespace-nowrap">—</td>
                      <td className="py-1 px-3 whitespace-nowrap">{g.fallbackSpanId ? g.fallbackSpanId.substring(0,8) + '…' : '—'}</td>
                      <td className="py-1 px-3 whitespace-nowrap">—</td>
                    </tr>
                  )}
                  {g.rows.map(r => (
                    <tr key={r.idx} className="border-b last:border-b-0">
                      <td className="py-1 px-3 whitespace-nowrap">{r.idx}</td>
                      <td className="py-1 px-3 whitespace-nowrap">{r.type}</td>
                      <td className="py-1 px-3 font-mono text-[11px] break-all">{r.text || '—'}</td>
                      <td className="py-1 px-3 whitespace-nowrap">{r.len || '—'}</td>
                      <td className="py-1 px-3 whitespace-nowrap">{r.deltaMs != null ? r.deltaMs : '—'}</td>
                      <td className="py-1 px-3 whitespace-nowrap">{r.spanId ? r.spanId.substring(0, 8) + '…' : '—'}</td>
                      <td className="py-1 px-3 whitespace-nowrap">{r.ts ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="px-3 py-2 text-xs text-gray-700">
              <span className="text-xs text-gray-600">{g.rows.length ? g.rows.map(r => r.type).join(' → ') : '(respuesta básica sin SSE)'}</span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};
