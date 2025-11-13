import React, { useEffect, useRef, useState } from 'react';
// Utilidad para normalizar el texto de un chiste eliminando metadatos variables (tema / versión)
function normalizeJokeText(raw: string): string {
  if (!raw) return '';
  // Quitar bloques JSON accidentales si vienen serializados (cuando text es un objeto stringificado)
  // y eliminar la parte '(tema: ... ) [v123]' para detectar verdaderos duplicados conceptuales.
  try {
    // Si el backend envía algo tipo '{\n  "text": "..."\n}' tratamos de extraer el campo text
    if (raw.trim().startsWith('{')) {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed.text === 'string') raw = parsed.text;
    }
  } catch { /* ignore parse errors */ }
  return raw
    .replace(/\(tema:[^)]+\)/gi, '')         // elimina '(tema: tema-xyz...)'
    .replace(/\[[vV]\d+\]/g, '')            // elimina '[v123]'
    .replace(/\s+/g, ' ')                     // normaliza espacios
    .trim()
    .toLowerCase();
}
// Extraer texto puro del chiste para mostrar (sin bajar a minúsculas)
function extractJokeDisplay(raw: string): string {
  if (!raw) return '';
  let text = raw.trim();
  if (text.startsWith('{') && text.endsWith('}')) {
    try {
      const obj = JSON.parse(text);
      if (typeof obj?.text === 'string') return obj.text.trim();
    } catch { /* ignore */ }
  }
  // Patrón "text": "..."
  const m = text.match(/"text"\s*:\s*"([^"]+)"/i);
  if (m) text = m[1].trim();
  // Limpiar decoraciones de tema/versión para el título visible
  return text
    .replace(/\(tema:[^)]+\)/gi, '')
    .replace(/\[[vV]\d+\]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}
// Pequeño componente interno para evitar repetir estilos de badges en la leyenda
const Badge: React.FC<{color:string; label:string}> = ({ color, label }) => (
  <span style={{
    display:'inline-block',
    padding:'2px 6px',
    borderRadius:12,
    fontSize:11,
    fontWeight:600,
    background:'#f1f5f9',
    color,
    letterSpacing:.5,
    textTransform:'none'
  }}>{label}</span>
);
import { useSse, SseEvent } from '../hooks/useSse';

interface JokeItemView {
  id: string;
  text: string;
  score?: number | null;
  uri?: string | null;
  approvalId?: string | null;
  reviewerNotes?: string | null;
  bossNotes?: string | null;
  state: 'generated' | 'scored' | 'waiting' | 'stored' | 'rejected';
  actions?: { agent:string; action:string; message?:string; reason?:string }[];
  reason?: string | null;
}

interface WorkflowStatusDto {
  workflowId: string;
  targetTotal: number;
  generated: number;
  saved: number;
  deleted: number;
  pendingApprovals: number;
  items: { id: string; text: string; score?: number; uri?: string; approvalId?: string }[];
}

interface WorkflowMetricsDto {
  workflowId: string;
  targetTotal: number;
  generated: number;
  saved: number;
  deleted: number;
  pendingApprovals: number;
}

export const JokesFactory: React.FC = () => {
  const { events, connect, disconnect, connected, clear } = useSse();
  // Dedupe robusto de logs/acciones por clave (usa ref para evitar condiciones de carrera)
  const seenLogKeysRef = useRef<Set<string>>(new Set());
  // Evitar logs duplicados de 'completed' si llegan múltiples eventos terminales
  const [completedLogged, setCompletedLogged] = useState(false);
  const [stoppedLogged, setStoppedLogged] = useState(false);
  const [workflowId, setWorkflowId] = useState<string | null>(null);
  const [items, setItems] = useState<JokeItemView[]>([]);
  const [status, setStatus] = useState<WorkflowStatusDto | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [completed, setCompleted] = useState(false);
  // Métricas adicionales (KPI)
  const [blockedModeration, setBlockedModeration] = useState(0);
  const [approvalsRequested, setApprovalsRequested] = useState(0);
  const [limitSummary, setLimitSummary] = useState(5);
  const [summary, setSummary] = useState<string>('');
  const [aiSummary, setAiSummary] = useState<string>('');
  const [rawFiles, setRawFiles] = useState<Array<{ name:string; uri:string; content:string }>>([]);
  const [bestJoke, setBestJoke] = useState<{ name:string; uri:string; score:number; firstLine:string }|null>(null);
  const [logLines, setLogLines] = useState<string[]>([]);
  const shortId = (s?:string|null) => s ? (s.split('-').pop() || s) : '?';
  const clip = (s:string, n:number) => (s && s.length > n) ? (s.slice(0, n) + '…') : (s || '');
  const agentPalette: Record<string,string> = {
    generator: '#2563eb',
    validator: '#1e3a8a',
    boss: '#b45309',
    review: '#991b1b',
    human: '#dc2626',
    workflow: '#6d28d9',
    agent: '#374151',
    log: '#334155'
  };
  const agentLabelEs: Record<string,string> = {
    generator: 'chistes',
    validator: 'revisor',
    boss: 'jefe',
    human: 'humano',
    review: 'revisión',
    agent: 'agente',
    log: 'log'
  };

  function addActionUnique(itemId: string, agent: string, action: string, message?: string, reason?: string) {
    setItems(prev => prev.map(i => {
      if (i.id !== itemId) return i;
      // No añadir acciones después de un estado terminal
      if (i.state === 'rejected' || i.state === 'stored') return i;
      const next = { agent, action, message, reason };
      const exists = (i.actions || []).some(a => (a.agent||'')===agent && (a.action||'')===action && (a.reason||'')===(reason||'') && (a.message||'')===(message||''));
      return exists ? i : { ...i, actions: [...(i.actions || []), next] };
    }));
  }
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [totalRequested, setTotalRequested] = useState(5);
  // Panel de consulta guardados (MCP)
  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Array<{ name:string; uri:string; preview:string }>>([]);
  const [countSaved, setCountSaved] = useState<number | null>(null);
  const [busyApprovals, setBusyApprovals] = useState<Set<string>>(new Set());
  // Estado: mapa de approvalId -> estado/fecha para diagnóstico HITL
  const [approvals, setApprovals] = useState<Record<string, { status: string; createdAt?: string; reason?: string }>>({});
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  type StepStatus = 'done' | 'pending' | 'skip' | 'reject';
  const StepDot: React.FC<{label:string; status:StepStatus}> = ({ label, status }) => {
    const cls = status === 'done' ? 'bg-emerald-600' : status === 'pending' ? 'bg-amber-500' : status === 'reject' ? 'bg-rose-600' : 'bg-slate-300';
    const text = status === 'done' ? 'text-emerald-700' : status === 'pending' ? 'text-amber-700' : status === 'reject' ? 'text-rose-700' : 'text-slate-500';
    return (
      <div className="flex items-center gap-2">
        <span className={`inline-block w-2.5 h-2.5 rounded-full ${cls}`}></span>
        <span className={`text-[11px] ${text}`}>{label}</span>
      </div>
    );
  };
  function Stepper(it: JokeItemView) {
    const score = it.score ?? null;
    const needsHitl = (score ?? 0) >= 9;
    const steps: { key:string; label:string; status:StepStatus }[] = [];
    // 1 Generador
    steps.push({ key:'gen', label:'generador', status:'done' });
    // 2 Revisor
    steps.push({ key:'rev', label:'revisor', status: it.score != null ? 'done' : 'pending' });
    // 3 Jefe
    if (needsHitl) {
      const st: StepStatus = it.state === 'waiting' ? 'pending' : (it.state === 'stored' || it.state === 'rejected') ? 'done' : 'pending';
      steps.push({ key:'boss', label:'jefe', status: st });
    } else {
      steps.push({ key:'boss', label:'jefe', status:'skip' });
    }
    // 4 Humano (si aplica)
    if (needsHitl) {
      const st: StepStatus = it.state === 'rejected' ? 'reject' : it.state === 'stored' ? 'done' : it.state === 'waiting' ? 'pending' : 'pending';
      steps.push({ key:'human', label:'humano', status: st });
    } else {
      steps.push({ key:'human', label:'humano', status:'skip' });
    }
    // 5 Almacén
  // Se elimina paso explícito de almacén: persistencia automática vía MCP
    return (
      <div className="flex items-center gap-4 flex-wrap mb-1">
        {steps.map(s => (
          <div key={s.key} className="flex items-center gap-2">
            <StepDot label={s.label} status={s.status} />
          </div>
        ))}
      </div>
    );
  }

  // Mapear eventos SSE del backend
  useEffect(() => {
    if (!events.length) return;
    const ev = events[events.length - 1];
    if (!workflowId && ev.type === 'workflow_started') {
      // El payload tiene { total }
      // workflowId lo sabremos por la respuesta inicial start
      pushLog(`[workflow] started total=${(ev.data as any)?.total ?? '?'}`);
    }
    if (ev.type === 'state_snapshot') {
      const d:any = ev.data;
      pushLog(`[workflow] snapshot gen=${d?.generated ?? '?'} saved=${d?.saved ?? '?'} waiting=${d?.pendingApprovals ?? '?'} target=${d?.targetTotal ?? '?'}`);
    }
    if (ev.type === 'joke_generated') {
      console.log('[SSE] joke_generated', ev.stepId, ev.data);
      pushLog(`[generator] id=${shortId(ev.stepId)} generated: ${clip((ev.data as any)?.text ?? '', 80)}`);
      // Sólo actualizamos estado; las acciones se registran vía agent_action para evitar duplicados
      setItems(prev => {
        const id = ev.stepId || '??';
        if (prev.some(i => i.id === id)) return prev; // evitar duplicados si llega repetido
        const text = (ev.data as any)?.text ?? '';
        console.log('[STATE] push generated id=', id, 'text=', text);
        // Prepend para que lo más nuevo quede arriba
        return [{ id, text: (ev.data as any)?.text ?? '', state: 'generated', actions: [], reviewerNotes: null, bossNotes: null }, ...prev];
      });
    }
    if (ev.type === 'joke_scored') {
      console.log('[SSE] joke_scored', ev.stepId, ev.data);
      const sc = (ev.data as any)?.score ?? '?';
      pushLog(`[validator] id=${shortId(ev.stepId)} scored=${sc}`);
  addActionUnique(ev.stepId || '??', 'validator', 'puntuó', String(sc));
      setItems(prev => prev.map(i => i.id === ev.stepId ? { ...i, score: (ev.data as any)?.score ?? null, state: 'scored', reviewerNotes: (ev.data as any)?.rationale ?? i.reviewerNotes } : i));
    }
    if (ev.type === 'approval_required') {
      const data: any = ev.data;
      console.log('[SSE] approval_required', ev.stepId, data);
      pushLog(`[boss] id=${shortId(ev.stepId)} approval_required approvalId=${data?.approvalId ?? '?'} `);
  addActionUnique(ev.stepId || '??', 'boss', 'pidió HITL', data?.approvalId ? `approvalId=${data.approvalId}` : undefined);
      setItems(prev => prev.map(i => i.id === ev.stepId ? { ...i, approvalId: data?.approvalId, state: 'waiting', reviewerNotes: data?.reviewerNotes ?? i.reviewerNotes, bossNotes: data?.bossNotes ?? i.bossNotes } : i));
      setApprovalsRequested(v => v + 1);
    }
    if (ev.type === 'joke_stored') {
      console.log('[SSE] joke_stored', ev.stepId);
      pushLog(`[workflow] id=${shortId(ev.stepId)} stored`);
  addActionUnique(ev.stepId || '??', 'workflow', 'guardó');
      setItems(prev => prev.map(i => i.id === ev.stepId ? { ...i, state: 'stored' } : i));
      refreshStatus();
    }
    if (ev.type === 'moderation_blocked') {
      const data:any = ev.data;
      console.log('[SSE] moderation_blocked', ev.stepId, data);
      pushLog(`[review] id=${shortId(ev.stepId)} moderation_blocked reason=${data?.reason ?? ''}`);
  addActionUnique(ev.stepId || '??', 'review', 'rechazó moderación', data?.reason, data?.reason);
      setItems(prev => prev.map(i => i.id === ev.stepId ? { ...i, state: 'rejected', reason: data?.reason || i.reason, approvalId: null } : i));
      setBlockedModeration(v => v + 1);
    }
    if (ev.type === 'joke_rejected') {
      const data:any = ev.data;
      console.log('[SSE] joke_rejected', ev.stepId, data);
      const k = `rej|${ev.stepId}|${data?.reason||''}`;
      if (!seenLogKeysRef.current.has(k)) {
        seenLogKeysRef.current.add(k);
        pushLog(`[review] id=${shortId(ev.stepId)} rejected reason=${data?.reason ?? ''}`);
      }
  // Evitar duplicidad: el rechazo humano ya queda reflejado por agent_action; no añadimos aquí
      setItems(prev => prev.map(i => i.id === ev.stepId ? { ...i, state: 'rejected', approvalId: null, reason: data?.reason || i.reason, reviewerNotes: data?.reviewerNotes ?? i.reviewerNotes, bossNotes: data?.bossNotes ?? i.bossNotes } : i));
    }
    if (ev.type === 'agent_action') {
      const payload:any = ev.data || {};
      console.log('[SSE] agent_action', ev.stepId, payload);
      const k = `${ev.stepId}|${payload.agent}|${payload.action}|${payload.reason||''}|${payload.message||''}`;
      if (!seenLogKeysRef.current.has(k)) {
        seenLogKeysRef.current.add(k);
        pushLog(`[${payload.agent || 'agent'}] ${payload.action || 'action'} id=${shortId(ev.stepId)}${payload.reason ? ` reason=${payload.reason}`:''}${payload.message ? ` - ${payload.message}`:''}`);
        if (!(payload.agent==='generator' && payload.action==='generated')){
          addActionUnique(ev.stepId || '??', String(payload.agent || 'agent'), String(payload.action || 'acción'), payload.message, payload.reason);
        }
      }
      // Actualizar sólo la razón si aplica; las acciones ya las administra addActionUnique con dedupe
      // Actualizar notas si vienen desde eventos de agente
      setItems(prev => prev.map(i => {
        if (i.id !== ev.stepId) return i;
        let reviewerNotes = i.reviewerNotes;
        let bossNotes = i.bossNotes;
        if ((payload.agent||'').toLowerCase() === 'reviewer' || (payload.agent||'').toLowerCase() === 'validator') {
          if (payload.notes) reviewerNotes = payload.notes;
        }
        if ((payload.agent||'').toLowerCase() === 'boss') {
          if (payload.notes) bossNotes = payload.notes;
        }
        const reason = (payload.action === 'reject' && payload.agent !== 'human') ? (payload.reason || i.reason) : i.reason;
        return { ...i, reviewerNotes, bossNotes, reason };
      }));
    }
    if (ev.type === 'workflow_completed') {
      console.log('[SSE] workflow_completed');
      if (!completedLogged) {
        pushLog('[workflow] completed');
        setCompletedLogged(true);
      }
      // Último evento: detenemos stream y polling
      disconnect();
      setCompleted(true);
      setAutoRefresh(false);
    }
    if (ev.type === 'workflow_stopped') {
      console.log('[SSE] workflow_stopped');
      if (!stoppedLogged) {
        pushLog('[workflow] stopped');
        setStoppedLogged(true);
      }
      disconnect();
      setCompleted(true);
      setAutoRefresh(false);
    }
  }, [events, workflowId, disconnect]);

  // Poll de estado periódico si hay workflowId
  useEffect(() => {
    if (!workflowId || !autoRefresh || completed) return;
    // Métricas ligeras cada 2s
    const h1 = setInterval(() => refreshMetrics(), 2000);
    // Estado completo (incluye items) cada 6s
    const h2 = setInterval(() => refreshStatus(), 6000);
    return () => { clearInterval(h1); clearInterval(h2); };
  }, [workflowId, autoRefresh, completed]);

  async function startWorkflow() {
    console.log('[ACTION] startWorkflow total=', totalRequested);
    setStarting(true); setError(null); setSummary(''); clear(); setItems([]);
    try {
      const res = await fetch('/api/jokes/start', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ Total: totalRequested, EnsureHitl: true }) });
      if (!res.ok) {
        const body = await res.text().catch(() => '');
        throw new Error(`HTTP ${res.status}${body ? ' - ' + body.slice(0,200) : ''}`);
      }
      const data = await res.json();
      console.log('[ACTION] startWorkflow response', data);
  setWorkflowId(data.workflowId);
  setCompletedLogged(false);
  setStoppedLogged(false);
      connect(`/api/jokes/stream/${data.workflowId}`);
    } catch (e:any) {
      setError(e?.message || 'Error al iniciar workflow');
      console.warn('[ACTION] startWorkflow error', e);
    } finally { setStarting(false); }
  }

  async function stopWorkflow() {
    console.log('[ACTION] stopWorkflow workflowId=', workflowId);
    if (!workflowId) return;
    try {
      await fetch(`/api/jokes/stop/${workflowId}`, { method: 'POST' });
      console.log('[ACTION] stopWorkflow POST enviado');
    } catch { /* noop */ }
    disconnect();
    setAutoRefresh(false);
  }

  async function refreshStatus() {
    if (!workflowId) return;
    try {
  const res = await fetch(`/api/jokes/status/${workflowId}`);
      if (!res.ok) throw new Error('Status error');
      const data: WorkflowStatusDto = await res.json();
      console.log('[POLL] status', data);
      setStatus(data);
      if (data.generated === data.targetTotal && data.pendingApprovals === 0) {
        setCompleted(true);
        setAutoRefresh(false);
      }
      // Alinear items con backend por si se recargó la página o se perdió algún SSE
      setItems(prev => {
        const map = new Map(prev.map(i => [i.id, i]));
        data.items.forEach(it => {
          if (!map.has(it.id)) {
            const inferred: JokeItemView['state'] = it.uri ? 'stored' : (it.approvalId ? 'waiting' : (it.score != null ? 'scored' : 'generated'));
            map.set(it.id, { id: it.id, text: it.text, score: it.score, uri: it.uri, approvalId: it.approvalId, state: inferred, actions: [] });
          } else {
            const old = map.get(it.id)!;
            // Preservar estados terminales (rejected, stored) y no degradar el estado por datos parciales del status
            const inferred: JokeItemView['state'] = it.uri ? 'stored' : (it.approvalId ? 'waiting' : (it.score != null ? 'scored' : old.state));
            const nextState: JokeItemView['state'] = old.state === 'rejected' ? 'rejected' : (old.state === 'stored' ? 'stored' : inferred);
            // Importante: approvalId del servidor es autoritativo; si viene null debemos limpiar el local
            map.set(it.id, { ...old, score: it.score ?? old.score, uri: it.uri ?? old.uri, approvalId: it.approvalId ?? null, state: nextState, actions: old.actions || [] });
          }
        });
        // Ordenar por número de secuencia al final del id para estabilidad visual
        const arr = Array.from(map.values());
        const num = (s:string) => {
          const parts = s.split('-');
          const n = parseInt(parts[parts.length-1], 10);
          return isNaN(n) ? Number.MAX_SAFE_INTEGER : n;
        };
        // Mostrar el más reciente primero (descendente)
        return arr.sort((a,b) => num(b.id) - num(a.id));
      });
    } catch { /* ignore poll errors */ }
  }

  async function refreshMetrics() {
    if (!workflowId) return;
    try {
      const res = await fetch(`/api/jokes/metrics/${workflowId}`);
      if (!res.ok) return; // no interrumpir la UI si falla
      const m: WorkflowMetricsDto = await res.json();
      console.log('[POLL] metrics', m);
      setStatus(prev => prev ? { ...prev, ...m } as WorkflowStatusDto : prev);
      if (m.generated === m.targetTotal && m.pendingApprovals === 0) {
        setCompleted(true);
        setAutoRefresh(false);
      }
    } catch { /* ignorar errores de métricas */ }
  }

  // Refrescar estados de aprobaciones pendientes (HITL)
  async function refreshApprovalStatuses() {
    // Tomar los approvalId actualmente en espera
    const ids = Array.from(new Set(items.filter(i => i.state === 'waiting' && i.approvalId).map(i => i.approvalId!)));
    if (ids.length === 0) return;
    try {
      // Limitar el fan-out para evitar demasiadas llamadas simultáneas
      const chunk = (arr: string[], size: number) => arr.reduce<string[][]>((acc, _, i) => (i % size ? acc : [...acc, arr.slice(i, i + size)]), []);
      for (const group of chunk(ids, 5)) {
        const results = await Promise.all(group.map(async id => {
          try {
            const res = await fetch(`/api/jokes/approval/${id}`);
            if (!res.ok) return { id, notfound: true } as const;
            const d = await res.json();
            return { id, status: (d.status ?? d.Status ?? '').toString().toLowerCase(), createdAt: d.createdAt ?? d.CreatedAt, reason: d.reason ?? d.Reason } as const;
          } catch {
            return { id, error: true } as const;
          }
        }));
        setApprovals(prev => {
          const next = { ...prev };
          for (const r of results) {
            if ('status' in r) next[r.id] = { status: r.status, createdAt: r.createdAt, reason: r.reason };
            else if ('notfound' in r) delete next[r.id]; // backend reiniciado o ya procesado
          }
          return next;
        });
      }
    } catch { /* noop */ }
  }

  // Poll periódico para métricas/estado/approvals cuando autoRefresh está activo
  useEffect(() => {
    if (!autoRefresh) return;
    const h1 = setInterval(() => refreshMetrics(), 4000);
    const h2 = setInterval(() => refreshStatus(), 6000);
    const h3 = setInterval(() => refreshApprovalStatuses(), 7000);
    return () => { clearInterval(h1); clearInterval(h2); clearInterval(h3); };
  }, [autoRefresh, workflowId, items]);

  function since(iso?: string) {
    if (!iso) return '—';
    const t = new Date(iso).getTime();
    const s = Math.max(0, Math.floor((Date.now() - t)/1000));
    if (s < 60) return `${s}s`;
    const m = Math.floor(s/60); const r = s%60;
    return `${m}m ${r}s`;
  }

  async function actApproval(approvalId: string, action: 'approve'|'reject') {
    if (!approvalId) { setError('approvalId vacío'); return; }
    pushLog(`[human] ${action} approvalId=${approvalId}`);
    setBusyApprovals(prev => new Set(prev).add(approvalId));
    try {
      const res = await fetch(`/api/jokes/${action}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(approvalId) });
      if (!res.ok) {
        const t = await res.text().catch(()=> '');
        if (res.status === 404) {
          pushLog(`[workflow] ${action} fallo 404 approvalId=${approvalId} — puede que ya haya sido procesado o el backend se reinició`);
        }
        throw new Error(`Approval error HTTP ${res.status}${t?` - ${t.slice(0,160)}`:''}`);
      }
      pushLog(`[workflow] ${action} ok approvalId=${approvalId}`);
      // dejemos que el SSE actualice el estado (joke_stored / joke_rejected)
      refreshStatus();
    } catch (e:any) {
      console.error('[APPROVAL] error', e);
      pushLog(`[workflow] ${action} error: ${e?.message||e}`);
      setError(e.message || String(e));
      // Refrescar snapshot para reconciliar UI después del error
  try { await refreshStatus(); } catch {}
    } finally {
      setBusyApprovals(prev => { const n = new Set(prev); n.delete(approvalId); return n; });
    }
  }

  async function loadSummary() {
    try {
      const res = await fetch(`/api/jokes/summary?limit=${limitSummary}`);
      if (!res.ok) throw new Error('Summary error');
      const data = await res.json();
      const lines = (data.previews || []).map((p:any) => `${p.name}: ${p.firstLine}`).join('\n');
      setSummary(lines || '(sin datos)');
    } catch (e:any) { setError(e.message); }
  }

  // Consultas a guardados (MCP)
  async function handleSearch() {
    try {
      console.log('[ACTION] search query=', query);
      pushLog(`search query='${query}'`);
      const res = await fetch(`/api/jokes/search?query=${encodeURIComponent(query)}&limit=100`);
      if (!res.ok) throw new Error('Search error');
      const data = await res.json();
      setSearchResults(((data.results || []) as any[]).map(r => ({ name: r.name ?? r.Name, uri: r.uri ?? r.Uri, preview: r.preview ?? r.Preview })));
    } catch (e:any) { setError(e.message); }
  }

  async function handleCount() {
    try {
      console.log('[ACTION] count');
      pushLog('count');
      const res = await fetch('/api/jokes/count');
      if (!res.ok) throw new Error('Count error');
      const data = await res.json();
      setCountSaved(Number(data.count ?? 0));
    } catch (e:any) { setError(e.message); }
  }

  async function handleAiSummary() {
    try {
      console.log('[ACTION] ai-summary query=', query);
      pushLog(`ai-summary query='${query}'`);
      const res = await fetch('/api/jokes/ai-summary', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ Query: query, Limit: 10, MaxChars: 4000 }) });
      if (!res.ok) throw new Error('AI Summary error');
      const data = await res.json();
      setAiSummary(String(data.summary ?? data.Summary ?? ''));
    } catch (e:any) { setError(e.message); }
  }

  async function handleReadAll() {
    try {
      console.log('[ACTION] read-all files');
      pushLog('read-all');
      const res = await fetch('/api/jokes/contents?limit=40&maxCharsPerFile=800');
      if (!res.ok) throw new Error('Contents error');
      const data = await res.json();
      setRawFiles(((data.files||[]) as any[]).map(f => ({ name:f.name??f.Name, uri:f.uri??f.Uri, content:f.content??f.Content })));
    } catch (e:any) { setError(e.message); }
  }

  async function handleBest() {
    try {
      console.log('[ACTION] best');
      pushLog('best');
      const res = await fetch('/api/jokes/best');
      if (!res.ok) throw new Error('Best error');
      const data = await res.json();
      setBestJoke({ name: data.name ?? data.Name, uri: data.uri ?? data.Uri, score: Number(data.score ?? data.Score ?? -1), firstLine: data.firstLine ?? data.FirstLine ?? '' });
    } catch (e:any) { setError(e.message); }
  }

  function pushLog(line:string) {
    setLogLines(prev => [...prev.slice(-199), `${new Date().toISOString()} ${line}`]);
  }

  return (
    <div className="px-4 py-6 font-sans space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-4">
        <h1 className="text-2xl font-bold text-slate-800">Fábrica de Chistes</h1>
  <div className="flex items-center gap-3 flex-wrap">
          <label className="text-sm font-medium text-slate-700 flex items-center gap-2">Total:
            <input type="number" min={1} max={50} value={totalRequested} onChange={e => setTotalRequested(Number(e.target.value))} className="w-20 h-8 rounded border border-slate-300 px-2 text-sm" />
          </label>
          <button onClick={startWorkflow} disabled={starting || connected} className="px-3 py-1.5 rounded bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium disabled:opacity-50">Iniciar Workflow</button>
          <button onClick={stopWorkflow} disabled={!workflowId} className="px-3 py-1.5 rounded bg-rose-600 hover:bg-rose-700 text-white text-sm font-medium disabled:opacity-50">Detener Workflow</button>
          {connected ? (
            <button onClick={() => disconnect()} disabled={!connected} className="px-3 py-1.5 rounded bg-slate-100 hover:bg-slate-200 text-slate-800 text-sm font-medium border border-slate-300">Pausar Stream</button>
          ) : (
            <button onClick={() => { if (workflowId) connect(`/api/jokes/stream/${workflowId}`); }} disabled={!workflowId} className="px-3 py-1.5 rounded bg-slate-100 hover:bg-slate-200 text-slate-800 text-sm font-medium border border-slate-300">Reanudar Stream</button>
          )}
          <label className="flex items-center gap-2 text-xs text-slate-600">
            <input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} className="rounded border-slate-300" /> Auto refresh
          </label>
          <button onClick={() => { setWorkflowId(null); setItems([]); setStatus(null); clear(); setSummary(''); }} className="px-3 py-1.5 rounded bg-slate-100 hover:bg-slate-200 text-slate-800 text-sm font-medium border border-slate-300">Reset</button>
        </div>
      </div>

      {error && <div className="rounded bg-red-50 border border-red-300 text-red-700 px-3 py-2 text-sm">Error: {error}</div>}

      {workflowId && (
        <div className="text-xs bg-indigo-50 border border-indigo-200 px-3 py-1 rounded">Workflow ID: <code className="font-mono">{workflowId}</code></div>
      )}

      {/* Leyendas */}
      <div className="flex flex-wrap gap-6 items-start">
        <div className="space-y-1">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Estados</p>
          <div className="flex gap-2 flex-wrap text-xs">
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-slate-100 text-slate-700 font-semibold">generated</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 font-semibold">scored</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 font-semibold">waiting</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 font-semibold">stored</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-red-100 text-red-700 font-semibold">rejected</span>
          </div>
        </div>
        <div className="space-y-1">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Agentes</p>
          <div className="flex gap-2 flex-wrap text-xs">
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 font-semibold">chistes</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 font-semibold">revisor</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 font-semibold">jefe</span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-rose-50 text-rose-700 font-semibold">humano</span>
          </div>
        </div>
        <div className="text-xs text-slate-600 mt-2">
          {(() => {
            const generated = items.filter(i => i.state === 'generated').length;
            const scored = items.filter(i => i.state === 'scored').length;
            const waiting = items.filter(i => i.state === 'waiting').length;
            const stored = items.filter(i => i.state === 'stored').length;
            const rejected = items.filter(i => i.state === 'rejected').length;
            return <span>Contadores — g:{generated} · s:{scored} · w:{waiting} · st:{stored} · r:{rejected}</span>;
          })()}
        </div>
      </div>

      <section className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          <h2 className="text-lg font-semibold text-slate-800">Chistes</h2>
          {items.length === 0 && <div style={{color:'#777'}}>Aún no hay chistes.</div>}
          {items.length > 0 && (
            <div className="space-y-4">
              {items.map(it => {
                const waiting = it.state === 'waiting' && it.approvalId;
                const rejected = it.state === 'rejected';
                const stored = it.state === 'stored';
                return (
                  <div key={it.id} className={`border rounded-lg px-4 py-3 shadow-sm transition ${waiting?'bg-amber-50 border-amber-200':rejected?'bg-rose-50 border-rose-200':stored?'bg-emerald-50 border-emerald-200':'bg-white border-slate-200'}`}>
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className={`leading-relaxed ${waiting?'text-xl font-semibold':'text-lg font-medium'} whitespace-pre-wrap`}>{extractJokeDisplay(it.text)}</div>
                        <div className="mt-2 flex flex-wrap items-center gap-3 text-[11px]">
                          <span className={`px-2 py-1 rounded-full font-semibold tracking-wide ${stored?'bg-emerald-100 text-emerald-700':waiting?'bg-amber-100 text-amber-700':rejected?'bg-rose-100 text-rose-700':it.score!=null?'bg-blue-100 text-blue-700':'bg-slate-100 text-slate-700'}`}>{it.state}{rejected && it.reason?` · ${it.reason}`:''}</span>
                          <span className="px-2 py-1 rounded-full bg-slate-100 text-slate-700 font-semibold">score: {it.score ?? '—'}</span>
                          {Stepper(it)}
                        </div>
                        {(it.reviewerNotes || it.bossNotes) && (
                          <div className="mt-2 space-y-1 text-[12px] text-slate-700">
                            {it.reviewerNotes && (
                              <div className="flex items-start gap-2">
                                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 font-semibold">revisor</span>
                                <span className="whitespace-pre-wrap">{it.reviewerNotes}</span>
                              </div>
                            )}
                            {it.bossNotes && (
                              <div className="flex items-start gap-2">
                                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 font-semibold">jefe</span>
                                <span className="whitespace-pre-wrap">{it.bossNotes}</span>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                      <div className="flex flex-col gap-2 items-end">
                        {waiting && it.approvalId && (
                          <>
                            <button
                              title={`approvalId=${it.approvalId}`}
                              disabled={busyApprovals.has(it.approvalId)}
                              onClick={() => actApproval(it.approvalId!, 'approve')}
                              className={`px-4 py-1.5 rounded text-white text-sm font-medium w-28 ${busyApprovals.has(it.approvalId) ? 'bg-emerald-500/50 cursor-default' : 'bg-emerald-600 hover:bg-emerald-700 cursor-pointer'}`}
                            >{busyApprovals.has(it.approvalId) ? 'Enviando…' : 'Aprobar'}</button>
                            <button
                              title={`approvalId=${it.approvalId}`}
                              disabled={busyApprovals.has(it.approvalId)}
                              onClick={() => actApproval(it.approvalId!, 'reject')}
                              className={`px-4 py-1.5 rounded text-white text-sm font-medium w-28 ${busyApprovals.has(it.approvalId) ? 'bg-rose-600/50 cursor-default' : 'bg-rose-600 hover:bg-rose-700 cursor-pointer'}`}
                            >{busyApprovals.has(it.approvalId) ? 'Enviando…' : 'Rechazar'}</button>
                          </>
                        )}
                        {stored && it.uri && (
                          <a href={it.uri} target="_blank" rel="noreferrer" className="px-4 py-1.5 rounded bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium w-28 text-center">Archivo</a>
                        )}
                        {!waiting && !stored && it.state !== 'rejected' && (
                          <button onClick={() => setItems(prev => prev.filter(x => x.id !== it.id))} className="px-4 py-1.5 rounded bg-slate-500 hover:bg-slate-600 text-white text-sm font-medium w-28">Remover</button>
                        )}
                      </div>
                    </div>
                    {(it.actions?.length ?? 0) > 0 && (
                      <div className="mt-3">
                        <ul className="space-y-1 text-xs">
                          {(expanded.has(it.id) ? it.actions! : it.actions!.slice(-4)).map((a,idx) => {
                            const ag = (a.agent||'agent').toLowerCase();
                            const color = agentPalette[ag] || '#475569';
                            const emoji: Record<string,string> = { generator:'🎭', validator:'✅', boss:'👔', human:'🙋', review:'🛑', agent:'🤖' };
                            const nice = (agent:string, action:string) => {
                              const act = action.toLowerCase();
                              if (agent==='generator' && (act.includes('contó')||act.includes('generated'))) return 'contó el chiste';
                              if (agent==='validator' && (act.includes('puntuó')||act.includes('score'))) return `puntuó ${a.message ?? ''}`.trim();
                              if (agent==='boss' && (act.includes('hitl')||act.includes('approval'))) return 'pidió revisión humana';
                              if ((agent==='human'||agent==='review') && act.includes('rechaz')) return `rechazó${a.reason?': '+a.reason:''}`;
                              return `${action}${a.message?': '+a.message:''}`;
                            };
                            return (
                              <li key={idx} className="flex gap-2 items-start">
                                <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full font-semibold text-[10px]`} style={{background:'#f1f5f9', color}}>
                                  {emoji[ag] || '🤖'} {agentLabelEs[ag] || ag}
                                </span>
                                <span className="whitespace-pre-wrap">{nice(ag, a.action)}</span>
                              </li>
                            );
                          })}
                        </ul>
                        {(it.actions!.length > 4) && (
                          <button
                            onClick={() => setExpanded(prev => { const n = new Set(prev); n.has(it.id) ? n.delete(it.id) : n.add(it.id); return n; })}
                            className="mt-1 text-[11px] text-blue-600 hover:underline"
                          >
                            {expanded.has(it.id) ? 'Ocultar historial' : `Ver historial (${it.actions!.length})`}
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
        <div className="space-y-4">
          <h2 className="text-lg font-semibold text-slate-800">Estado</h2>
          {!status && <div style={{color:'#777'}}>Sin estado aún.</div>}
          {status && (
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div className="p-2 bg-slate-50 rounded border border-slate-200">Objetivo: <span className="font-semibold">{status.targetTotal}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200">Generados: <span className="font-semibold">{status.generated}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200">Guardados: <span className="font-semibold">{status.saved}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200">Eliminados: <span className="font-semibold">{status.deleted}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200 col-span-2">Pendientes HITL: <span className="font-semibold">{status.pendingApprovals}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200 col-span-2">Aprobaciones solicitadas: <span className="font-semibold">{approvalsRequested}</span></div>
              <div className="p-2 bg-slate-50 rounded border border-slate-200 col-span-2">Bloqueos moderación: <span className="font-semibold">{blockedModeration}</span> <span className="ml-2 text-[10px] text-slate-500">({(blockedModeration === 0 ? 0 : ((blockedModeration / Math.max(1, status.generated)) * 100)).toFixed(1)}% de generados)</span></div>
            </div>
          )}
          {/* Logs de llamadas/eventos */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">Llamadas y eventos</h3>
              <div className="flex items-center gap-2">
                <button onClick={() => setLogLines([])} className="px-2 py-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs border border-slate-300">Limpiar</button>
                <button onClick={() => loadSummary()} className="px-2 py-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs border border-slate-300">Resumen guardados</button>
              </div>
            </div>
            <div className="h-48 overflow-auto rounded border border-slate-200 bg-slate-50 p-2">
              {logLines.length === 0 ? (
                <div className="text-[11px] text-slate-500">Sin logs aún. Inicia el workflow y observa aquí las llamadas de agentes, rechazos, aprobaciones y persistencias.</div>
              ) : (
                <ul className="space-y-1">
                  {logLines.slice(-200).map((l,idx) => (
                    <li key={idx} className="font-mono text-[11px] text-slate-700 whitespace-pre-wrap">{l}</li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      </section>
      {/* Panel flotante de diagnóstico HITL */}
      {/* Modal flotante de aprobaciones eliminado por requerimiento */}
    </div>
  );
};
