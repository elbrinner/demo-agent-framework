import React, { useEffect, useMemo, useState } from 'react';

interface TelemetryItem {
  time: string; // ISO from backend
  agentType: string;
  model: string;
  promptChars: number;
  responseChars: number;
  durationMs: number;
  traceId?: string | null;
  spanId?: string | null;
}

// Panel para mostrar invocaciones recientes desde /bri-agent/telemetry/recent
export const TelemetryPanel: React.FC<{ loadTrigger?: number }> = ({ loadTrigger = 0 }) => {
  const [items, setItems] = useState<TelemetryItem[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await fetch('/bri-agent/telemetry/recent');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setItems(data);
    } catch (e: any) {
      setError(e?.message || 'Error cargando telemetría');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (loadTrigger > 0) load();
  }, [loadTrigger]);

  const rows = useMemo(() => items.map((i) => {
    const when = new Date(i.time);
    const agoMs = Date.now() - when.getTime();
    const ago = agoMs < 1000 ? 'ahora' : `${Math.round(agoMs / 1000)}s`;
    const kb = (i.responseChars / 1024).toFixed(1);
    return { ...i, when, ago, kb };
  }), [items]);

  return (
    <div className="border border-gray-300 rounded-lg p-4 bg-white">
      <div className="flex items-center justify-between mb-3">
        <div className="font-semibold text-gray-900">Telemetría reciente</div>
        <button onClick={load} className="text-xs px-2 py-1 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-md">Refrescar</button>
      </div>
      {loading && <div className="text-sm text-gray-500">Cargando…</div>}
      {error && <div className="text-sm text-red-600">{error}</div>}
      {!rows.length && !loading && !error && (
        <div className="text-sm text-gray-500">No hay invocaciones aún.</div>
      )}
      {!!rows.length && (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="text-left text-gray-600 border-b">
                <th className="py-2 pr-4">hace</th>
                <th className="py-2 pr-4">agente</th>
                <th className="py-2 pr-4">modelo</th>
                <th className="py-2 pr-4">prompt</th>
                <th className="py-2 pr-4">respuesta</th>
                <th className="py-2 pr-4">duración</th>
                <th className="py-2 pr-4">traza</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, idx) => (
                <tr key={idx} className="border-b last:border-b-0">
                  <td className="py-2 pr-4 whitespace-nowrap">{r.ago}</td>
                  <td className="py-2 pr-4 whitespace-nowrap">{r.agentType}</td>
                  <td className="py-2 pr-4 whitespace-nowrap">{r.model}</td>
                  <td className="py-2 pr-4 whitespace-nowrap">{r.promptChars} ch</td>
                  <td className="py-2 pr-4 whitespace-nowrap">{r.responseChars} ch ({r.kb} KB)</td>
                  <td className="py-2 pr-4 whitespace-nowrap">{r.durationMs} ms</td>
                  <td className="py-2 pr-4 whitespace-nowrap">
                    {r.traceId ? (
                      <button
                        onClick={() => navigator.clipboard.writeText(r.traceId!)}
                        className="text-xs px-2 py-1 bg-blue-500 hover:bg-blue-600 text-white rounded-md transition-colors"
                      >{r.traceId.substring(0, 12)}… copiar</button>
                    ) : (
                      <span className="text-gray-400">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
