import '@testing-library/jest-dom';

// Mock básico de EventSource para pruebas del hook useSse
class MockEventSource {
  url: string;
  readyState = 0; // 0 connecting, 1 open, 2 closed
  withCredentials = false;
  onopen: ((ev: Event) => any) | null = null;
  onmessage: ((ev: MessageEvent) => any) | null = null;
  onerror: ((ev: Event) => any) | null = null;
  private listeners: Record<string, Function[]> = {};

  constructor(url: string) {
    this.url = url;
    // Registrar instancia global para facilitar dispatch en tests
    ;(globalThis as any).EventSource.instances = (globalThis as any).EventSource.instances || [];
    (globalThis as any).EventSource.instances.push(this);
    setTimeout(() => {
      this.readyState = 1;
      this.onopen && this.onopen(new Event('open'));
    }, 10);
  }

  addEventListener(type: string, cb: any) {
    if (!this.listeners[type]) this.listeners[type] = [];
    this.listeners[type].push(cb);
  }
  removeEventListener(type: string, cb: any) {
    this.listeners[type] = (this.listeners[type] || []).filter(f => f !== cb);
  }
  dispatch(type: string, data: any) {
    const ev = new MessageEvent(type, { data: typeof data === 'string' ? data : JSON.stringify(data) });
    if (type === 'message' && this.onmessage) this.onmessage(ev);
    (this.listeners[type] || []).forEach(f => f(ev));
  }
  close() { this.readyState = 2; }
}

// @ts-ignore
globalThis.EventSource = MockEventSource;
// Disabled jest-dom for node environment tests to avoid jsdom parse5 ESM issues.
// import '@testing-library/jest-dom';