// This module is substituted only by the browser test's network interception.
export const LogLevel = { None: 6 };
export class DefaultHttpClient {
  async send() { throw new Error('SDK fixture does not perform HTTP requests.'); }
  getCookieString() { return ''; }
}
export class HubConnectionBuilder {
  withUrl(url, options) { this.url = url; this.options = options; return this; }
  configureLogging() { return this; }
  withAutomaticReconnect(policy) { this.policy = policy; return this; }
  build() {
    const handlers = new Map();
    const connection = {
      url: this.url, options: this.options, policy: this.policy, starts: 0, stops: 0,
      on(name, handler) { handlers.set(name, handler); },
      off(name, handler) { if (handlers.get(name) === handler) handlers.delete(name); },
      onreconnecting(handler) { this.reconnecting = handler; },
      onreconnected(handler) { this.reconnected = handler; },
      onclose(handler) { this.closed = handler; },
      async start() { this.starts++; this.emit('RealtimeReady'); },
      async stop() { this.stops++; },
      emit(name, message) { handlers.get(name)?.(message); },
      handler(name) { return handlers.get(name); },
    };
    (globalThis.__realtimeConnections ||= []).push(connection);
    return connection;
  }
}
