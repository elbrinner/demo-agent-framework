import React, { useEffect, useState } from 'react';
import { useSse, SseEvent } from '../hooks/useSse';
import { LogPanel } from '../components/LogPanel';
import { TracePanel } from '../components/TracePanel';
import { StreamPanel } from '../components/StreamPanel';

export const Workflows: React.FC = () => {
  const { events, connect, disconnect, clear, connected, lastEventAt } = useSse();
  const [logs, setLogs] = useState<string[]>([]);
  const [mode, setMode] = useState<'seq'|'par'>('seq');
  const [steps, setSteps] = useState<{ id: string; name: string; status: 'pending'|'running'|'done'; }[]>([]);
  const [meta, setMeta] = useState<any>(null);
  const [chunks, setChunks] = useState<string[]>([]);
  const [lastTrace, setLastTrace] = useState<string | null>(null);
  const [results, setResults] = useState<Record<string, { text: string; durationMs?: number }>>({});
  const [tokenTimes, setTokenTimes] = useState<number[]>([]);
  const [tokenRate, setTokenRate] = useState<number>(0);

  useEffect(() => {
    if (!events.length) return;
    const last: SseEvent = events[events.length - 1];
    setLogs(prev => [...prev, `[${last.type}]` + (last.stepId?` step:${last.stepId}`:'') + (last.traceId?` trace:${last.traceId.substring(0,8)}`:'')]);
    if (last.type === 'workflow_started') {
      // Inicializar pasos / ramas desde el evento started
      const d: any = last.data;
      setMeta(d?.meta || null);
      const rawSteps: any[] = d?.steps || d?.branches || [];
      setSteps(rawSteps.map(s => ({ id: s.id, name: s.name, status: 'pending' })));
    }
    if (last.type === 'token') {
      const txt = typeof last.data === 'string' ? last.data : (last.data?.text ?? '');
      if (txt) {
        setChunks(prev => [...prev, txt]);
        // Actualizar ventana deslizante de 5s y tasa
        const now = Date.now();
        setTokenTimes(prev => {
          const filtered = prev.filter(t => now - t <= 5000);
          const next = [...filtered, now];
          const seconds = Math.max(1, (next[next.length-1] - next[0]) / 1000);
          setTokenRate(Number((next.length / seconds).toFixed(1)));
          return next;
        });
      }
    }
    if (last.traceId) setLastTrace(last.traceId);
    if (last.type === 'step_started') {
      setSteps(prev => prev.map(s => s.id === (last.stepId || (last.data as any)?.id) ? { ...s, status: 'running' } : s));
    }
    if (last.type === 'step_completed') {
      const d = last.data as any;
      const id = d?.id ?? last.stepId ?? 'unknown';
      setResults(prev => ({ ...prev, [id]: { text: d?.text ?? '', durationMs: d?.durationMs } }));
      setSteps(prev => prev.map(s => s.id === id ? { ...s, status: 'done' } : s));
    }
  }, [events]);

  const start = () => {
    clear(); setChunks([]); setResults({}); setTokenTimes([]); setTokenRate(0); setMeta(null);
    const url = `/bri-agent/workflows/${mode}`; // GET streaming endpoints seq / par
    connect(url);
  };

  return (
    <div style={{padding:'1rem', display:'grid', gap:12, gridTemplateColumns:'1fr 1fr'}}>
      <div style={{gridColumn:'1/-1'}}>
        <h1>Workflows</h1>
        <div style={{display:'flex', gap:8, alignItems:'center'}}>
          <select value={mode} onChange={e => setMode(e.target.value as any)}>
            <option value="seq">Secuencial</option>
            <option value="par">Paralelo</option>
          </select>
          <button onClick={start} disabled={connected}>Iniciar</button>
          <button onClick={disconnect} disabled={!connected}>Detener</button>
          <button onClick={() => { clear(); setLogs([]); setChunks([]); setResults({}); setLastTrace(null); setTokenTimes([]); setTokenRate(0); }}>Limpiar</button>
          {lastTrace && (
            <div style={{marginLeft:8, display:'flex', alignItems:'center', gap:6}}>
              <span style={{fontSize:12, background:'#eef', padding:'2px 6px', borderRadius:4}}>trace {lastTrace.substring(0,12)}…</span>
              <button onClick={() => navigator.clipboard.writeText(lastTrace!)} style={{fontSize:12}}>Copy traceId</button>
            </div>
          )}
          {connected && (
            <span style={{fontSize:11, color:"#0a0", background:"#e8fbe8", padding:"2px 6px", borderRadius:4}}>
              vivo {lastEventAt ? Math.round((Date.now()-lastEventAt)/1000) + 's' : "0s"}
            </span>
          )}
          {connected && (
            <span style={{fontSize:11, color:"#064", background:"#e6fff1", padding:"2px 6px", borderRadius:4}}>
              rate {tokenRate} tok/s
            </span>
          )}
        </div>
      </div>

    <StreamPanel chunks={chunks} />
      <LogPanel logs={logs} />
  <TracePanel events={events} />

      <div style={{border:'1px solid #ddd', borderRadius:8, padding:10}}>
        <div style={{display:'flex', justifyContent:'space-between', alignItems:'center', marginBottom:6}}>
          <div style={{fontWeight:600}}>Workflow</div>
          {meta?.ui?.mode === 'workflow' && (
            <span style={{fontSize:11, background:'#eef', padding:'2px 6px', borderRadius:4}}>view: {meta.ui.recommendedView}</span>
          )}
        </div>
        <div style={{marginBottom:8}}>
          {steps.length === 0 && <div style={{color:'#999'}}>Sin pasos inicializados todavía…</div>}
          {steps.length > 0 && (
            <ol style={{margin:0, paddingLeft:20}}>
              {steps.map(s => (
                <li key={s.id} style={{marginBottom:4}}>
                  <strong>{s.name}</strong> <code style={{fontSize:11}}>{s.id}</code>{' '}
                  <span style={{fontSize:11, color:s.status==='done'?'#064':s.status==='running'?'#a36':'#888'}}>
                    [{s.status}]
                  </span>
                </li>
              ))}
            </ol>
          )}
        </div>
        <div style={{fontWeight:600, marginBottom:6}}>Resultados por paso</div>
        {Object.keys(results).length === 0 ? (
          <div style={{color:'#999'}}>Aún sin resultados…</div>
        ) : (
          <ul style={{margin:0, paddingLeft:20}}>
            {Object.entries(results).map(([id, r]) => {
              const text = r.text || '';
              const tokens = text ? text.trim().split(/\s+/).filter(Boolean).length : 0;
              return (
                <li key={id} style={{marginBottom:6}}>
                  <div style={{fontFamily:'ui-monospace, SFMono-Regular'}}>
                    <strong>{id}</strong>
                    {typeof r.durationMs === 'number' && (
                      <span style={{marginLeft:8, fontSize:12, color:'#555'}}>
                        ({Math.round(r.durationMs)} ms · ~{tokens} tok)
                      </span>
                    )}
                    {typeof r.durationMs !== 'number' && (
                      <span style={{marginLeft:8, fontSize:12, color:'#555'}}>
                        (~{tokens} tok)
                      </span>
                    )}
                  </div>
                  <div style={{whiteSpace:'pre-wrap'}}>{text}</div>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
};
