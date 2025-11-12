import React, { useState } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';

interface CodeFile { path: string; language?: string; content?: string; found?: boolean; error?: string; }
interface CodeResponse { demo: string; files: CodeFile[]; persisted?: boolean; origin?: 'code' | 'source'; }

export const CodePanel: React.FC<{ demoId: string }> = ({ demoId }) => {
  const [files, setFiles] = useState<CodeFile[] | null>(null);
  const [persisted, setPersisted] = useState<boolean | null>(null);
  const [origin, setOrigin] = useState<'code' | 'source' | null>(null);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true); setError(null);
    try {
  // Backend expone GET /bri-agent/demos/{demoId}/code
  const res = await fetch(`/bri-agent/demos/${demoId}/code`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: CodeResponse = await res.json();
      setFiles(json.files || []);
      setPersisted(!!json.persisted);
      setOrigin((json.origin as any) || null);
    } catch (e:any) { setError(e.message); }
    finally { setLoading(false); }
  };

  const toggle = () => {
    const next = !open;
    setOpen(next);
    if (next && files == null && !loading) load();
  };

  return (
    <div className="border border-gray-300 rounded-lg p-4 bg-gray-900 text-gray-100">
      <button 
        onClick={toggle} 
        className="mb-3 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors font-medium"
      >
        {open ? 'Ocultar Código' : 'Mostrar Código'}
      </button>
      {(origin || persisted !== null) && (
        <div className="mb-3 flex items-center gap-2 text-xs">
          {origin === 'code' && (
            <span className="px-2 py-1 rounded bg-purple-600 text-white">educativo</span>
          )}
          {persisted === true && (
            <span className="px-2 py-1 rounded bg-emerald-600 text-white">persistido</span>
          )}
        </div>
      )}
      {error && <div className="text-red-400 mb-3">Error: {error}</div>}
      {open && loading && <div className="text-gray-400">Cargando código…</div>}
      {open && files && (
        <div className="space-y-6">
          {files.map(f => (
            <div key={f.path}>
              <div className="font-semibold mb-2 flex items-center gap-2">
                {f.path}
                {f.found === false && <span className="text-red-400">(no encontrado)</span>}
              </div>
              {f.content && (
                <SyntaxHighlighter
                  language={(f.language as any) || 'csharp'}
                  style={vscDarkPlus}
                  customStyle={{ margin: 0, padding: 12, fontSize: 13, borderRadius: 6 }}
                >
                  {f.content}
                </SyntaxHighlighter>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
