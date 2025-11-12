import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

interface DemoMeta { id: string; title: string; description: string; tags: string[] }

export const DemoList: React.FC = () => {
  const [demos, setDemos] = useState<DemoMeta[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      try {
        const res = await fetch('/bri-agent/demos/list');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        setDemos(await res.json());
      } catch (e:any) { setError(e.message); }
      finally { setLoading(false); }
    };
    load();
  }, []);

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <h1 className="text-3xl font-bold text-gray-900 mb-2">Bri-Agent Demos</h1>
      <p className="text-gray-600 mb-6">Selecciona una demo para ver el flujo interno (logs, tokens, código y más).</p>
      {loading && <p className="text-blue-600">Cargando…</p>}
      {error && <p className="text-red-600 bg-red-50 p-3 rounded-md">Error: {error}</p>}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {demos.map(d => (
          <div key={d.id} className="bg-white border border-gray-200 rounded-lg p-4 shadow-sm hover:shadow-md transition-shadow">
            <Link to={`/demos/${d.id}`} className="block">
              <h3 className="text-lg font-semibold text-blue-600 hover:text-blue-800 mb-2">{d.title}</h3>
            </Link>
            <p className="text-gray-700 mb-3">{d.description}</p>
            <div className="flex flex-wrap gap-1">
              {d.tags.map(tag => (
                <span key={tag} className="inline-block bg-blue-100 text-blue-800 text-xs px-2 py-1 rounded-full">
                  #{tag}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
