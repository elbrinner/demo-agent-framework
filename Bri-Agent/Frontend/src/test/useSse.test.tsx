import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSse } from '../hooks/useSse';

// Acceso al mock EventSource definido en setup.ts
interface MockEventSourceType {
  dispatch(type: string, data: any): void;
  close(): void;
}

describe('useSse hook', () => {
  it('conecta y recibe eventos básicos', async () => {
    const { result } = renderHook(() => useSse());
    await act(async () => {
      result.current.connect('/api/jokes/stream/fake');
      // Esperar a que abra conexión (onopen en mock)
      await new Promise(r => setTimeout(r, 30));
    });
    expect(result.current.connected).toBe(true);

    // Simular eventos a través del canal 'message' con el sobre JSON que el hook sabe parsear
    await act(async () => {
      (globalThis as any).EventSource.instances?.[0]?.dispatch('message', { type:'workflow_started', data:{ foo:'bar' } });
      (globalThis as any).EventSource.instances?.[0]?.dispatch('message', { type:'joke_generated', data:{ text:'chiste' }, stepId:'wf-1' });
      await new Promise(r => setTimeout(r, 10));
    });
    expect(result.current.events.length).toBeGreaterThanOrEqual(2);
    const gen = result.current.events.find(e => e.type === 'joke_generated');
    expect(gen).toBeTruthy();
    expect(gen?.data.text).toBe('chiste');
  });
});
