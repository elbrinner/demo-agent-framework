import React, { useEffect, useState } from 'react';
import { JokesFactory } from './JokesFactory';

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
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch('/bri-agent/demos/list');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        if (!cancelled) setDemos(data);
      } catch (e:any) {
        if (!cancelled) setError(e.message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  return (
    <div style={{ fontFamily: 'system-ui', padding: '1rem' }}>
      <h1>Bri-Agent Frontend</h1>
      <p>Listado dinámico de demos y Fábrica de Chistes (HITL).</p>
      {loading && <p>Cargando...</p>}
      {error && <p style={{ color: 'red' }}>Error: {error}</p>}
      <details style={{ marginBottom: '1rem' }} open>
        <summary style={{ cursor: 'pointer', fontWeight: 600 }}>Demos registradas</summary>
        <ul>
          {demos.map(d => (
            <li key={d.id}>
              <strong>{d.title}</strong> — {d.description} <em>[{d.tags.join(', ')}]</em>
            </li>
          ))}
        </ul>
      </details>
      <JokesFactory />
    </div>
  );
};
