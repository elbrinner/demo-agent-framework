import React, { useEffect, useState } from 'react';

interface DemoMeta {
  id: string;
  title: string;
  description: string;
  tags: string[];
}

export const App: React.FC = () => {
  const [demos, setDemos] = useState<DemoMeta[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch('/bri-agent/demos/list');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        setDemos(data);
      } catch (e: any) {
        setError(e.message);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  return (
    <div style={{ fontFamily: 'system-ui', padding: '1rem' }}>
      <h1>Bri-Agent Frontend</h1>
      <p>Listado dinámico de demos (placeholder).</p>
      {loading && <p>Cargando...</p>}
      {error && <p style={{color:'red'}}>Error: {error}</p>}
      <ul>
        {demos.map(d => (
          <li key={d.id}>
            <strong>{d.title}</strong> — {d.description} <em>[{d.tags.join(', ')}]</em>
          </li>
        ))}
      </ul>
    </div>
  );
};
