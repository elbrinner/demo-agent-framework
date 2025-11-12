import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useSse, SseEvent } from '../hooks/useSse';
import { LogPanel } from '../components/LogPanel';
import { StreamPanel } from '../components/StreamPanel';
import { ResponsePanel } from '../components/ResponsePanel';
import { CodePanel } from '../components/CodePanel';
import { TracePanel } from '../components/TracePanel';
import { StatsPanel } from '../components/StatsPanel';
import { TelemetryPanel } from '../components/TelemetryPanel';

interface DemoMeta { id: string; title: string; description: string; tags: string[] }

export const DemoHost: React.FC = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const [meta, setMeta] = useState<DemoMeta | null>(null);
  const [prompt, setPrompt] = useState<string>('');
  const inputRef = useRef<HTMLInputElement>(null);
  const [logs, setLogs] = useState<string[]>([]);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [threadHistory, setThreadHistory] = useState<string[]>([]);
  const [response, setResponse] = useState<any>(null);
  const [chunks, setChunks] = useState<string[]>([]);
  const { events, connect, disconnect, clear, connected, lastEventAt, done } = useSse();
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [isConnecting, setIsConnecting] = useState<boolean>(false);
  type Toast = { id: number; kind: 'error' | 'info'; text: string };
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [lastTrace, setLastTrace] = useState<string | null>(null);
  const [lastSpan, setLastSpan] = useState<string | null>(null);
  const [tokenTimes, setTokenTimes] = useState<number[]>([]);
  const [tokenRate, setTokenRate] = useState<number>(0);
  const [telemetryLoadTrigger, setTelemetryLoadTrigger] = useState<number>(0);
  const [model, setModel] = useState<string | null>(null);
  const [accumulatedResponse, setAccumulatedResponse] = useState<string>('');
  const [selectedTools, setSelectedTools] = useState<string[]>([]);
  const [availableTools, setAvailableTools] = useState<{name:string, description:string}[]>([
    { name: 'climate', description: 'Obtiene clima actual sintético para una ciudad.' },
    { name: 'currency', description: 'Convierte montos EUR→USD con tasa fija demo.' },
    { name: 'summary', description: 'Resume contenido textual (simulado).' },
    { name: 'worldtime', description: 'Devuelve hora local aproximada de una región.' },
    { name: 'sentiment', description: 'Analiza sentimiento de un fragmento de texto.' },
  ]);
  const [workflowSteps, setWorkflowSteps] = useState<{id: string, status: 'pending' | 'running' | 'completed', output?: string, startTime?: number, endTime?: number}[]>([]);
  const [workflowStatus, setWorkflowStatus] = useState<'idle' | 'running' | 'completed'>('idle');
  const [parallelTokens, setParallelTokens] = useState<Record<string, string>>({});
  const streamText = useMemo(() => chunks.join(''), [chunks]);
  const processedIdxRef = useRef<number>(0);
  // Nuevas demos de traducción
  const isTranslationSequential = id === 'translation-sequential-workflow';
  const isTranslationParallel = id === 'translation-parallel-workflow';
  // Nueva demo Ollama con un único botón de streaming
  const isOllama = id === 'ollama';

  useEffect(() => {
    if (done) setTelemetryLoadTrigger(prev => prev + 1);
  }, [done]);

  useEffect(() => {
    const load = async () => {
      const res = await fetch('/bri-agent/demos/list');
      const all: DemoMeta[] = await res.json();
      const found = all.find(d => d.id === id);
      setMeta(found || null);
    };
    load();
  }, [id]);

  // Procesar eventos SSE ya manejados por hook useSse
  useEffect(() => {
    const startIdx = processedIdxRef.current;
    if (events.length <= startIdx) return;
    const newEvents = events.slice(startIdx);
    let newTrace: string | null = null;
    let newSpan: string | null = null;
    const now = Date.now();

    for (const ev of newEvents) {
      if (isConnecting && (ev.type === 'started' || ev.type === 'token' || ev.type === 'completed')) {
        setIsConnecting(false);
      }
      setLogs(prev => [...prev, `event: ${ev.type}`, `data: ${JSON.stringify(ev)}`]);
      if (ev.traceId) newTrace = ev.traceId;
      if (ev.spanId) newSpan = ev.spanId;

      if (ev.type === 'started') {
        const m = typeof ev.data === 'object' ? (ev.data?.model ?? null) : null;
        if (m) setModel(m);
        const tid = typeof ev.data === 'object' ? (ev.data?.threadId ?? null) : null;
        if (tid && !threadId) setThreadId(tid);
      }
      if (ev.type === 'token') {
        const stepId = ev.data?.stepId;
        const text = ev.data?.text || '';
        const parallelContext = isParallelWorkflow || isTranslationParallel;
        if (stepId && parallelContext) {
          setParallelTokens(prev => ({ ...prev, [stepId]: (prev[stepId] || '') + text }));
        } else {
          const txt = typeof ev.data === 'string' ? ev.data : (ev.data?.text ?? '');
          if (txt) {
            setChunks(prev => [...prev, txt]);
            setAccumulatedResponse(prev => prev + txt);
            setTokenTimes(prev => {
              const filtered = prev.filter(t => now - t <= 5000);
              const next = [...filtered, now];
              const seconds = Math.max(1, (next[next.length - 1] - next[0]) / 1000);
              setTokenRate(Number((next.length / seconds).toFixed(1)));
              return next;
            });
          }
        }
      }
      if (ev.type === 'error') {
        const msg = typeof ev.data === 'string' ? ev.data : (ev.data?.message ?? 'Error en stream');
        const tidErr = Date.now();
        setToasts(prev => [...prev, { id: tidErr, kind: 'error', text: msg }]);
        setTimeout(() => setToasts(prev => prev.filter(t => t.id !== tidErr)), 4000);
        setIsConnecting(false);
      }
      if (ev.type === 'workflow_started') {
        setWorkflowStatus('running');
        setWorkflowSteps([]);
        setParallelTokens({});
      }
      if (ev.type === 'step_started') {
        const stepId = typeof ev.data === 'object' ? (ev.data?.stepId || ev.data?.id) : ev.data;
        if (stepId) {
          setWorkflowSteps(prev => prev
            .map(s => s.id === stepId ? { ...s, status: 'running' as const, startTime: Date.now() } : s)
            .concat(prev.some(s => s.id === stepId) ? [] : [{ id: stepId, status: 'running' as const, startTime: Date.now() }]));
        }
      }
      if (ev.type === 'step_completed') {
        const stepId = typeof ev.data === 'object' ? (ev.data?.stepId || ev.data?.id) : ev.data;
        const output = typeof ev.data === 'object' ? (ev.data?.output || ev.data?.text) : '';
        if (stepId) {
          setWorkflowSteps(prev => prev.map(s => s.id === stepId ? { ...s, status: 'completed' as const, output: output || s.output, endTime: Date.now() } : s));
        }
      }
      if (ev.type === 'workflow_completed') {
        setWorkflowStatus('completed');
      }
    }
    if (newTrace) setLastTrace(newTrace);
    if (newSpan) setLastSpan(newSpan);
    processedIdxRef.current = events.length;
  }, [events]);

  const runBasic = async (p?: string) => {
    setLogs(prev => [...prev, 'POST /agents/basic']);
    setResponse(null);
    setAccumulatedResponse('');
    const payload = (p ?? prompt);
    try {
      setIsSubmitting(true);
      const res = await fetch('/bri-agent/agents/basic', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ prompt: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      setResponse(await res.json());
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e: any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo enviar la solicitud' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runStructured = async (p?: string) => {
    setLogs(prev => [...prev, 'POST /demos/structured-agent/run']);
    setResponse(null);
    setAccumulatedResponse('');
    const payload = (p ?? prompt);
    try {
      setIsSubmitting(true);
      const res = await fetch('/bri-agent/demos/structured-agent/run', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ prompt: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar structured-agent' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runTools = async (p?: string) => {
    setLogs(prev => [...prev, 'POST /agents/tools']);
    setResponse(null);
    setAccumulatedResponse('');
    const payload = (p ?? prompt);
    try {
      setIsSubmitting(true);
      const res = await fetch('/bri-agent/agents/tools', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ question: payload, tools: selectedTools }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      if (Array.isArray(json.availableTools)) {
        setAvailableTools(json.availableTools);
      }
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar tools-agent' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runMultiToolPrompt = async (p?: string) => {
    setLogs(prev => [...prev, 'POST /demos/multi-tool-prompt/run']);
    setResponse(null);
    setAccumulatedResponse('');
    const payload = (p ?? prompt);
    try {
      setIsSubmitting(true);
      const res = await fetch('/bri-agent/demos/multi-tool-prompt/run', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ question: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar multi-tool-prompt' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runMultiToolOrchestrated = async (p?: string) => {
    setLogs(prev => [...prev, 'POST /demos/multi-tool-orchestrated/run']);
    setResponse(null);
    setAccumulatedResponse('');
    const payload = (p ?? prompt);
    try {
      setIsSubmitting(true);
      const res = await fetch('/bri-agent/demos/multi-tool-orchestrated/run', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ input: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar multi-tool-orchestrated' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runParallelWorkflow = (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/demos/parallel-workflow/run?input=${encodeURIComponent(payload)}`;
    setLogs(prev => [...prev, `SSE GET ${url}`]);
    setIsConnecting(true);
    connect(url);
  };

  const runTranslationSequentialWorkflow = async (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/translation/seq-sync`;
    setLogs(prev => [...prev, `POST ${url}`]);
    try {
      setIsSubmitting(true);
      const res = await fetch(url, { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ prompt: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      // si vienen steps, los reflejamos en la UI de workflow
      if (Array.isArray(json.steps)) {
        setWorkflowSteps(json.steps.map((s:any) => ({ id: s.id, status: 'completed', output: s.output, startTime: Date.now() - s.durationMs, endTime: Date.now() })));
        setWorkflowStatus('completed');
      }
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar traducción secuencial' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const runTranslationParallelWorkflow = async (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/translation/par-sync`;
    setLogs(prev => [...prev, `POST ${url}`]);
    try {
      setIsSubmitting(true);
      const res = await fetch(url, { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ prompt: payload }) });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Fallo ${res.status}`);
      }
      const json = await res.json();
      setResponse(json);
      if (Array.isArray(json.steps)) {
        setWorkflowSteps(json.steps.map((s:any) => ({ id: s.id, status: 'completed', output: s.output, startTime: Date.now() - s.durationMs, endTime: Date.now() })));
        setWorkflowStatus('completed');
      }
      setModel(null);
      setTelemetryLoadTrigger(prev => prev + 1);
    } catch (e:any) {
      const id = Date.now();
      setToasts(prev => [...prev, { id, kind: 'error', text: e?.message || 'No se pudo ejecutar traducción paralela' }]);
      setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 4000);
    } finally {
      setIsSubmitting(false);
    }
  };

  const startStream = (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/agents/stream?prompt=${encodeURIComponent(payload)}`;
    setLogs(prev => [...prev, `SSE GET ${url}`]);
    setIsConnecting(true);
    connect(url);
  };
  const startOllamaStream = (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/ollama/stream?prompt=${encodeURIComponent(payload)}`;
    setLogs(prev => [...prev, `SSE GET ${url}`]);
    setIsConnecting(true);
    connect(url);
  };
  const startAiFoundryStream = (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/aifoundry/stream?prompt=${encodeURIComponent(payload)}`;
    setLogs(prev => [...prev, `SSE GET ${url}`]);
    setIsConnecting(true);
    connect(url);
  };
  const startThreadStream = (p?: string) => {
    clear(); setChunks([]);
    setAccumulatedResponse('');
    setModel(null);
    processedIdxRef.current = 0;
    const payload = (p ?? prompt);
    const url = `/bri-agent/agents/thread/stream?message=${encodeURIComponent(payload)}${threadId ? `&threadId=${encodeURIComponent(threadId)}` : ''}`;
    setLogs(prev => [...prev, `SSE GET ${url}`]);
    setIsConnecting(true);
    connect(url);
  };
  const stopStream = () => { setLogs(prev => [...prev, 'Cerrar SSE']); disconnect(); };
  const clearUi = () => { clear(); setLogs([]); setChunks([]); setResponse(null); setLastTrace(null); setLastSpan(null); setAccumulatedResponse(''); processedIdxRef.current = 0; setThreadId(null); setThreadHistory([]); };

  const isBasic = id === 'bri-basic-agent';
  const isThread = id === 'thread-agent' || id?.includes('thread');
  const isStructured = id === 'structured-agent' || id?.includes('struct');
  const isTools = id === 'tools-agent' || id?.includes('tools');
  const isStreamingCapable = id?.includes('stream') || id?.includes('workflow') || id === 'streaming-agent' || id === 'thread-agent' || isOllama || id === 'ai-foundry';
  const isMultiToolPrompt = id === 'multi-tool-prompt' || id?.includes('multi-tool-prompt');
  const isMultiToolOrchestrated = id === 'multi-tool-orchestrated' || id?.includes('multi-tool-orchestrated');
  const isWorkflow = id?.includes('workflow');
  const isParallelWorkflow = id === 'parallel-workflow' || isTranslationParallel;
  const isSequentialWorkflow = id === 'sequential-workflow' || isTranslationSequential;
  const isAiFoundry = id === 'ai-foundry';

  const header = useMemo(() => (
    <div className={`flex items-center gap-4 mb-6 p-4 rounded-lg ${isAiFoundry ? 'bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200' : 'bg-gray-50'}`}>
      <button 
        onClick={() => navigate(-1)} 
        className="px-4 py-2 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-md transition-colors flex items-center gap-2"
      >
        ← Volver
      </button>
      <div className="flex-1">
        <div className="flex items-center gap-3 mb-1">
          <h2 className="text-2xl font-bold text-gray-900 m-0">{meta?.title || id}</h2>
          {isAiFoundry && (
            <span className="px-3 py-1 bg-blue-600 text-white text-sm font-medium rounded-full flex items-center gap-2">
              <span className="w-2 h-2 bg-white rounded-full"></span>
              Azure AI Foundry
            </span>
          )}
        </div>
        <p className="text-gray-600 m-0">{meta?.description}</p>
      </div>
    </div>
  ), [meta, id, isAiFoundry]);

  if (!id) return <div style={{padding:16}}>ID de demo no proporcionado.</div>;
  if (meta === null) return <div style={{padding:16}}>Cargando…</div>;

  return (
    <div className="p-6 max-w-full">
      <div className="col-span-full">{header}</div>

      <div className="mb-6 p-4 bg-white border border-gray-200 rounded-lg shadow-sm">
        <div className="flex flex-wrap gap-3 items-center mb-4">
          {/* Sugerencias para structured (si hay meta.hints en la última respuesta) */}
          {isStructured && Array.isArray((response?.meta?.hints)) && response.meta.hints.length > 0 && (
            <div className="w-full mb-2 flex flex-wrap gap-2">
              {response.meta.hints.map((h:string, i:number) => (
                <button key={i} onClick={() => { setPrompt(h); setTimeout(() => inputRef.current?.focus(), 0); }} className="text-xs px-2 py-1 bg-purple-100 hover:bg-purple-200 text-purple-800 rounded border border-purple-200">
                  {h}
                </button>
              ))}
            </div>
          )}

          {/* Catálogo de tools seleccionables */}
          {isTools && (
            <div className="w-full mb-2 flex flex-wrap gap-2">
              {availableTools.map(t => {
                const active = selectedTools.includes(t.name);
                return (
                  <button
                    key={t.name}
                    title={t.description}
                    onClick={() => setSelectedTools(prev => prev.includes(t.name) ? prev.filter(x => x !== t.name) : [...prev, t.name])}
                    className={`text-xs px-2 py-1 rounded border ${active ? 'bg-emerald-100 border-emerald-300 text-emerald-800' : 'bg-gray-100 border-gray-300 text-gray-800'}`}
                    disabled={isSubmitting || isConnecting}
                  >
                    {active ? '✓ ' : ''}{t.name}
                  </button>
                );
              })}
            </div>
          )}

          <input 
            ref={inputRef}
            autoFocus
            value={prompt} 
            onChange={e => setPrompt(e.target.value)} 
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                const value = prompt.trim();
                if (!value) return;
                // Seleccionar acción según demo
                if (isThread) {
                  startThreadStream(value);
                } else if (isOllama) {
                  startOllamaStream(value);
                } else if (isAiFoundry) {
                  startAiFoundryStream(value);
                } else if (isBasic) {
                  runBasic(value);
                } else if (isStructured) {
                  runStructured(value);
                } else if (isTools) {
                  runTools(value);
                } else if (isMultiToolPrompt) {
                  runMultiToolPrompt(value);
                } else if (isMultiToolOrchestrated) {
                  runMultiToolOrchestrated(value);
                } else if (isTranslationSequential) {
                  runTranslationSequentialWorkflow(value);
                } else if (isTranslationParallel) {
                  runTranslationParallelWorkflow(value);
                } else if (isParallelWorkflow) {
                  runParallelWorkflow(value);
                } else if (isStreamingCapable) {
                  startStream(value);
                } else {
                  runBasic(value);
                }
                setPrompt('');
                // Devolver el foco para siguiente entrada
                setTimeout(() => inputRef.current?.focus(), 0);
              }
            }}
            className="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            placeholder={isStructured ? "Describe a la persona a convertir en ficha (nombre, edad, ocupación, habilidades)" : isTools ? "Pregunta que pueda invocar tools (clima, convertir, resume, hora, sentimiento)" : isMultiToolPrompt ? "Pregunta que requiera múltiples herramientas (ej: clima y comida en una ciudad)" : isMultiToolOrchestrated ? "Pregunta que requiera múltiples herramientas (ej: clima y comida en una ciudad)" : isParallelWorkflow ? "Ingresa cualquier texto para demostrar procesamiento paralelo" : "su consulta aqui"}
            disabled={isSubmitting || isConnecting}
          />
          {isBasic && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runBasic(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Ejecutar
            </button>
          )}
          {isStructured && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runStructured(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Consultar
            </button>
          )}
          {isTools && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runTools(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Ejecutar Tools
            </button>
          )}
          {isMultiToolPrompt && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runMultiToolPrompt(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Ejecutar Multi-Tool Prompt
            </button>
          )}
          {isMultiToolOrchestrated && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runMultiToolOrchestrated(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Ejecutar Multi-Tool Orchestrated
            </button>
          )}
          {id === 'parallel-workflow' && (
            <button 
              onClick={() => { const v = prompt.trim(); if (v) { runParallelWorkflow(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting}
            >
              Ejecutar Parallel Workflow
            </button>
          )}
          {isTranslationSequential && (
            <button
              onClick={() => { const v = prompt.trim(); if (v) { runTranslationSequentialWorkflow(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting || isConnecting}
            >
              Traducción Secuencial
            </button>
          )}
          {isTranslationParallel && (
            <button
              onClick={() => { const v = prompt.trim(); if (v) { runTranslationParallelWorkflow(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
              className="px-4 py-2 bg-pink-600 hover:bg-pink-700 text-white rounded-md transition-colors font-medium"
              disabled={isSubmitting || isConnecting}
            >
              Traducción Paralela
            </button>
          )}
          {isOllama && (
            <>
              <button
                onClick={() => { const v = prompt.trim(); if (v) { startOllamaStream(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
                disabled={connected || isConnecting}
                className="px-4 py-2 bg-green-600 hover:bg-green-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Stream Ollama
              </button>
              <button
                onClick={stopStream}
                disabled={!connected}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Detener
              </button>
            </>
          )}
          {isAiFoundry && (
            <>
              <button
                onClick={() => { const v = prompt.trim(); if (v) { startAiFoundryStream(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }}
                disabled={connected || isConnecting}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Stream AI Foundry
              </button>
              <button
                onClick={stopStream}
                disabled={!connected}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Detener
              </button>
            </>
          )}
          {/* Ocultamos los botones genéricos de Stream SSE para las demos de traducción que ya tienen su propio botón dedicado */}
          {!isBasic && isStreamingCapable && !isThread && !isTranslationSequential && !isTranslationParallel && !isOllama && !isAiFoundry && (
            <>
              <button 
                onClick={() => { const v = prompt.trim(); if (v) { startStream(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }} 
                disabled={connected || isConnecting}
                className="px-4 py-2 bg-green-600 hover:bg-green-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Stream SSE
              </button>
              <button 
                onClick={stopStream} 
                disabled={!connected}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Detener
              </button>
            </>
          )}
          {isThread && (
            <>
              <button 
                onClick={() => { const v = prompt.trim(); if (v) { startThreadStream(v); setPrompt(''); setTimeout(() => inputRef.current?.focus(), 0);} }} 
                disabled={connected || isConnecting}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Stream Thread
              </button>
              <button 
                onClick={stopStream} 
                disabled={!connected}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-gray-400 text-white rounded-md transition-colors font-medium"
              >
                Detener
              </button>
            </>
          )}
          <button 
            onClick={clearUi}
            className="px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-md transition-colors font-medium"
          >
            Limpiar
          </button>
        </div>
        
        <div className="flex flex-wrap gap-2 items-center">
          {lastTrace && (
            <div className="flex items-center gap-2">
              <span className="text-xs bg-blue-100 text-blue-800 px-2 py-1 rounded-md">
                trace {lastTrace.substring(0,12)}…
              </span>
              <button 
                onClick={() => navigator.clipboard.writeText(lastTrace!)} 
                className="text-xs px-2 py-1 bg-blue-500 hover:bg-blue-600 text-white rounded-md transition-colors"
              >
                Copiar traceId
              </button>
            </div>
          )}
          {lastSpan && (
            <div className="flex items-center gap-2">
              <span className="text-xs bg-green-100 text-green-800 px-2 py-1 rounded-md">
                span {lastSpan}
              </span>
              <button 
                onClick={() => navigator.clipboard.writeText(lastSpan!)} 
                className="text-xs px-2 py-1 bg-green-500 hover:bg-green-600 text-white rounded-md transition-colors"
              >
                Copiar spanId
              </button>
            </div>
          )}
          {isAiFoundry && (
            <span className="text-xs bg-blue-600 text-white px-2 py-1 rounded-md font-medium">
              Azure AI Foundry
            </span>
          )}
          {connected && isStreamingCapable && !done && (
            <span className="text-xs bg-green-100 text-green-800 px-2 py-1 rounded-md">
              vivo {lastEventAt ? Math.round((Date.now()-lastEventAt)/1000) + 's' : "0s"}
            </span>
          )}
          {done && (
            <span className="text-xs bg-gray-200 text-gray-700 px-2 py-1 rounded-md">
              completado
            </span>
          )}
          {connected && isStreamingCapable && (
            <span className="text-xs bg-blue-100 text-blue-800 px-2 py-1 rounded-md">
              rate {tokenRate} tok/s
            </span>
          )}
        </div>
      </div>
      {(isSubmitting || isConnecting) && (
        <div className="flex items-center gap-2 text-xs text-gray-600 mb-2">
          <span className="inline-block w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
          {isSubmitting ? 'enviando…' : 'conectando…'}
        </div>
      )}

      {/* Métricas específicas para traducción de workflows (paralelo o secuencial) */}
      {(isTranslationParallel || isTranslationSequential) && response && (
        <div className="mb-4 p-4 bg-gradient-to-r from-pink-50 to-rose-50 border border-rose-200 rounded-lg shadow-sm">
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-lg">📊</span>
            <div className="text-sm text-gray-900 font-semibold">Métricas de ejecución</div>
            {typeof response.wallDurationMs === 'number' && (
              <span className="text-xs bg-blue-100 text-blue-800 px-2 py-1 rounded">wall {Math.round(response.wallDurationMs)} ms</span>
            )}
            {typeof response.aggregateStepDurationMs === 'number' && (
              <span className="text-xs bg-purple-100 text-purple-800 px-2 py-1 rounded">suma pasos {Math.round(response.aggregateStepDurationMs)} ms</span>
            )}
            {typeof response.tokensTotalApprox === 'number' && (
              <span className="text-xs bg-emerald-100 text-emerald-800 px-2 py-1 rounded">tokens ~{response.tokensTotalApprox}</span>
            )}
          </div>
          {Array.isArray(response.steps) && response.steps.length > 0 && (
            <div className="mt-3 grid grid-cols-2 md:grid-cols-4 gap-2">
              {response.steps.map((s:any) => {
                const labelMap: Record<string,string> = { fr: 'Francés', pt: 'Portugués', de: 'Alemán', final: 'Final' };
                const label = labelMap[s.id] || s.id;
                return (
                <div key={s.id} className="text-xs bg-white border border-gray-200 rounded p-2 flex items-center justify-between">
                  <span className="font-medium text-gray-700">{label}</span>
                  <div className="flex items-center gap-2 text-gray-600">
                    {typeof s.durationMs === 'number' && <span>{Math.round(s.durationMs)} ms</span>}
                    {typeof s.tokensApprox === 'number' && <span>~{s.tokensApprox} tok</span>}
                  </div>
                </div>
              )})}
            </div>
          )}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
  <ResponsePanel response={response} streamText={streamText} />
        {isStreamingCapable && <StreamPanel chunks={chunks} />}
        <LogPanel logs={logs} />
        {isThread && (
          <div className="p-4 bg-white border border-gray-200 rounded-lg shadow-sm">
            <div className="flex items-center gap-2 mb-3">
              <span className="text-sm text-gray-700">Thread:</span>
              <code className="px-2 py-1 bg-gray-100 text-gray-800 rounded">{threadId ?? 'nuevo'}</code>
              <span className="text-xs bg-purple-100 text-purple-800 px-2 py-1 rounded-md ml-2">
                turns {Math.ceil(threadHistory.length / 2)}
              </span>
              {threadId && (
                <button 
                  onClick={() => { setThreadId(null); setThreadHistory([]); }}
                  className="ml-auto px-3 py-1 text-xs bg-gray-600 hover:bg-gray-700 text-white rounded"
                >
                  Resetear thread
                </button>
              )}
            </div>
            <div className="text-sm text-gray-800 space-y-2 max-h-64 overflow-auto">
              {threadHistory.length === 0 && <div className="text-gray-500">Sin historial todavía.</div>}
              {threadHistory.map((line, i) => {
                const isUser = i % 2 === 0;
                return (
                  <div key={i} className={`flex ${isUser ? 'justify-start' : 'justify-end'}`}>
                    <div className={`max-w-[80%] px-3 py-2 rounded-lg border shadow-sm whitespace-pre-wrap ${isUser ? 'bg-blue-50 border-blue-200 text-blue-900' : 'bg-emerald-50 border-emerald-200 text-emerald-900'}`}>
                      <div className="text-[10px] uppercase tracking-wide font-semibold mb-1 opacity-70">{isUser ? 'Usuario' : 'Agente'}</div>
                      {line}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}
        {isWorkflow && workflowSteps.length > 0 && (
          <div className="p-4 bg-white border border-gray-200 rounded-lg shadow-sm">
            <div className="flex items-center gap-2 mb-3">
              <span className="text-sm text-gray-700">Workflow:</span>
              <span className={`text-xs px-2 py-1 rounded-md ${workflowStatus === 'running' ? 'bg-blue-100 text-blue-800' : workflowStatus === 'completed' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}`}>
                {workflowStatus}
              </span>
              <span className="text-xs bg-purple-100 text-purple-800 px-2 py-1 rounded-md ml-2">
                steps {workflowSteps.length}
              </span>
            </div>
            <div className="text-sm text-gray-800 space-y-2 max-h-64 overflow-auto">
              {workflowSteps.length === 0 && <div className="text-gray-500">Sin pasos todavía.</div>}
              {workflowSteps.map((step, i) => {
                const labelMap: Record<string,string> = { fr: 'Francés', pt: 'Portugués', de: 'Alemán', final: 'Final' };
                const label = labelMap[step.id] || step.id;
                const duration = (typeof step.endTime === 'number' && typeof step.startTime === 'number')
                  ? step.endTime - step.startTime
                  : undefined;
                const tokens = step.output ? step.output.trim().split(/\s+/).filter(Boolean).length : 0;
                return (
                  <div key={step.id} className="flex items-center gap-2 p-2 border rounded">
                    <span className={`w-3 h-3 rounded-full ${step.status === 'completed' ? 'bg-green-500' : step.status === 'running' ? 'bg-blue-500 animate-pulse' : 'bg-gray-300'}`} />
                    <span className="font-medium">{label}</span>
                    <span className={`text-xs px-2 py-1 rounded ${step.status === 'completed' ? 'bg-green-100 text-green-800' : step.status === 'running' ? 'bg-blue-100 text-blue-800' : 'bg-gray-100 text-gray-800'}`}>
                      {step.status}
                    </span>
                    {typeof duration === 'number' && (
                      <span className="text-[11px] text-gray-600">{Math.round(duration)} ms</span>
                    )}
                    {step.output && (
                      <span className="text-[11px] text-gray-600">· ~{tokens} tok</span>
                    )}
                    {step.output && (
                      <span className="text-xs text-gray-600 truncate flex-1">{step.output}</span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
        {isParallelWorkflow && workflowSteps.length > 0 && (
          <div className="p-6 bg-white border border-gray-200 rounded-lg shadow-sm">
            <div className="flex items-center gap-3 mb-4">
              <div className="flex items-center gap-2">
                <span className="text-2xl">⚡</span>
                <span className="text-lg font-bold text-gray-900">Procesamiento Paralelo</span>
              </div>
              <span className={`text-sm px-3 py-1 rounded-full font-medium ${workflowStatus === 'running' ? 'bg-blue-100 text-blue-800' : workflowStatus === 'completed' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}`}>
                {workflowStatus === 'running' ? 'Ejecutando en paralelo' : workflowStatus === 'completed' ? 'Completado' : 'Esperando'}
              </span>
              <span className="text-sm bg-purple-100 text-purple-800 px-3 py-1 rounded-full ml-auto">
                {workflowSteps.length} tareas simultáneas
              </span>
            </div>
            <div className="text-sm text-gray-600 mb-4">
              Las tareas se ejecutan concurrentemente, mostrando el progreso en tiempo real de cada una.
            </div>
            <div className="text-sm text-gray-800 space-y-3 max-h-96 overflow-auto">
              {workflowSteps.length === 0 && <div className="text-gray-500">Sin pasos paralelos todavía.</div>}
              {workflowSteps.map((step, i) => {
                const labelMap: Record<string,string> = { fr: 'Francés', pt: 'Portugués', de: 'Alemán', final: 'Final' };
                const label = labelMap[step.id] || step.id;
                const duration = step.endTime && step.startTime ? step.endTime - step.startTime : 0;
                const liveText = parallelTokens[step.id] || '';
                const liveTokenCount = liveText ? liveText.trim().split(/\s+/).filter(Boolean).length : 0;
                const stepDescriptions: Record<string, string> = {
                  'step1': 'Explica qué es concurrencia en 2 frases',
                  'step2': 'Da un ejemplo de procesamiento paralelo'
                };
                const stepDescription = stepDescriptions[step.id] || label;
                // Calibración: ~40 tokens ≈ 100% (cap a 95% mientras está "running")
                const progressPercent = step.status === 'completed' ? 100 : step.status === 'running' ? Math.min(95, (liveTokenCount / 40) * 100) : 0;
                
                return (
                  <div key={step.id} className="border rounded-lg p-4 bg-gradient-to-r from-blue-50 to-purple-50 shadow-sm">
                    <div className="flex items-center justify-between mb-3">
                      <div className="flex items-center gap-3">
                        <span className={`w-4 h-4 rounded-full ${step.status === 'completed' ? 'bg-green-500' : step.status === 'running' ? 'bg-blue-500 animate-pulse' : 'bg-gray-300'}`} />
                        <div>
                          <span className="font-semibold text-gray-900 text-lg">{label}</span>
                          <div className="text-sm text-gray-600">{stepDescription}</div>
                        </div>
                        <span className={`text-xs px-3 py-1 rounded-full font-medium ${step.status === 'completed' ? 'bg-green-100 text-green-800' : step.status === 'running' ? 'bg-blue-100 text-blue-800' : 'bg-gray-100 text-gray-800'}`}>
                          {step.status}
                        </span>
                      </div>
                      <div className="text-right">
                        <div className="text-sm font-medium text-gray-700">
                          {duration > 0 ? `${(duration / 1000).toFixed(1)}s` : step.status === 'running' ? 'Procesando...' : ''}
                        </div>
                        <div className="text-xs text-gray-500">
                            ~{liveTokenCount} tok
                        </div>
                      </div>
                    </div>
                    
                    {/* Barra de progreso mejorada */}
                    <div className="w-full bg-gray-200 rounded-full h-3 mb-3 overflow-hidden">
                      <div 
                        className={`h-3 rounded-full transition-all duration-500 ease-out ${step.status === 'completed' ? 'bg-gradient-to-r from-green-400 to-green-600' : step.status === 'running' ? 'bg-gradient-to-r from-blue-400 to-blue-600 animate-pulse' : 'bg-gray-300'}`}
                        style={{ width: `${progressPercent}%` }}
                      />
                    </div>
                    
                    {/* Tokens en tiempo real con mejor diseño */}
                    {liveText && (
                      <div className="bg-white p-3 rounded-lg border border-gray-200 shadow-inner">
                        <div className="text-xs font-medium text-gray-700 mb-2 flex items-center gap-2">
                          <span className="w-2 h-2 bg-blue-500 rounded-full animate-pulse"></span>
                          Respuesta en tiempo real:
                        </div>
                        <div className="text-sm text-gray-800 font-mono leading-relaxed max-h-24 overflow-auto whitespace-pre-wrap">
                          {liveText}
                        </div>
                      </div>
                    )}
                    
                    {/* Output final */}
                    {step.output && step.status === 'completed' && (
                      <div className="mt-3 bg-green-50 p-3 rounded-lg border border-green-200">
                        <div className="text-xs font-medium text-green-700 mb-1">Resultado final:</div>
                        <div className="text-sm text-gray-800">{step.output}</div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )} 
        <TracePanel
          events={events} 
          recent={isBasic && response?.traceId ? { 
            traceId: response?.traceId, 
            spanId: response?.spanId,
            promptChars: response?.usage?.promptChars,
            responseChars: response?.usage?.responseChars,
            durationMs: response?.usage?.durationMs,
            model: response?.model
          } : undefined}
        />
        <StatsPanel events={events} model={model || response?.model || response?.usage?.model || null} />
        <TelemetryPanel loadTrigger={telemetryLoadTrigger} />
        <div className="lg:col-span-2">
          <CodePanel demoId={id} />
        </div>
      </div>
      {/* Toasts dentro del contenedor principal */}
      {toasts.length > 0 && (
        <div className="fixed bottom-4 right-4 space-y-2 z-50">
          {toasts.map(t => (
            <div key={t.id} className={`px-4 py-3 rounded shadow-lg text-sm ${t.kind === 'error' ? 'bg-red-600 text-white' : 'bg-gray-800 text-white'}`}>
              {t.text}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
