/* @vitest-environment node */
import { describe, it, expect } from 'vitest';

// Pure mapper similar to logic inside JokesFactory useEffect for events
interface IncomingEvent { type: string; data?: any; stepId?: string }
interface JokeItemView { id: string; text: string; score?: number | null; uri?: string | null; approvalId?: string | null; state: 'generated' | 'scored' | 'waiting' | 'stored' | 'rejected'; }

export function applyEvent(items: JokeItemView[], ev: IncomingEvent): JokeItemView[] {
  switch (ev.type) {
    case 'joke_generated':
      return [...items, { id: ev.stepId || '??', text: ev.data?.text ?? '', state: 'generated' }];
    case 'joke_scored':
      return items.map(i => i.id === ev.stepId ? { ...i, score: ev.data?.score ?? null, state: 'scored' } : i);
    case 'approval_required':
      return items.map(i => i.id === ev.stepId ? { ...i, approvalId: ev.data?.approvalId, state: 'waiting' } : i);
    case 'joke_stored':
      return items.map(i => i.id === ev.stepId ? { ...i, uri: ev.data?.uri ?? i.uri, state: 'stored' } : i);
    case 'joke_rejected':
      return items.map(i => i.id === ev.stepId ? { ...i, state: 'rejected' } : i);
    default:
      return items;
  }
}

describe('applyEvent mapper', () => {
  it('adds generated joke', () => {
    const res = applyEvent([], { type: 'joke_generated', stepId: 'a1', data: { text: 'hola' } });
    expect(res).toHaveLength(1);
    expect(res[0].state).toBe('generated');
    expect(res[0].text).toBe('hola');
  });

  it('scores existing joke', () => {
    const start = [{ id: 'a1', text: 'hola', state: 'generated' as const }];
    const res = applyEvent(start, { type: 'joke_scored', stepId: 'a1', data: { score: 77 } });
    expect(res[0].state).toBe('scored');
    expect(res[0].score).toBe(77);
  });

  it('marks approval required', () => {
    const start = [{ id: 'a2', text: 'meh', state: 'scored' as const, score: 20 }];
    const res = applyEvent(start, { type: 'approval_required', stepId: 'a2', data: { approvalId: 'APP-1' } });
    expect(res[0].state).toBe('waiting');
    expect(res[0].approvalId).toBe('APP-1');
  });

  it('stores joke', () => {
    const start = [{ id: 'a3', text: 'ok', state: 'waiting' as const, approvalId: 'APP-X' }];
    const res = applyEvent(start, { type: 'joke_stored', stepId: 'a3', data: { uri: 'file://jokes/a3.txt' } });
    expect(res[0].state).toBe('stored');
    expect(res[0].uri).toContain('a3.txt');
  });

  it('rejects joke', () => {
    const start = [{ id: 'a4', text: 'bad', state: 'waiting' as const, approvalId: 'APP-Y' }];
    const res = applyEvent(start, { type: 'joke_rejected', stepId: 'a4' });
    expect(res[0].state).toBe('rejected');
  });
});
