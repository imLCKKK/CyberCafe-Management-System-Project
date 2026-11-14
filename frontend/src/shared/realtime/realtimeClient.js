export const authFailureStatus = (error) => [401, 403].includes(error?.statusCode ?? error?.status)
  ? error.statusCode ?? error.status : null;

// Lifecycle is independent of React so multiple owners can share one authenticated connection.
export function createRealtimeClient({ connection, schedule = setTimeout, cancel = clearTimeout }) {
  const subscribers = new Set();
  const eventHandlers = new Map();
  let status = 'idle';
  let starting = false;
  let retryTimer = null;
  let failures = 0;
  let disposed = false;
  let unauthorized = false;

  function setStatus(value) {
    status = value;
    for (const subscriber of subscribers) subscriber.onStatus?.(value);
  }

  function authenticationFailed(error) {
    if (disposed || unauthorized) return;
    const code = authFailureStatus(error);
    if (!code) return;
    unauthorized = true;
    if (retryTimer !== null) { cancel(retryTimer); retryTimer = null; }
    setStatus(code === 401 ? 'unauthorized' : 'forbidden');
    for (const subscriber of subscribers) subscriber.onAuthFailure?.(code);
  }

  function ready() {
    if (disposed || unauthorized) return;
    failures = 0;
    setStatus('connected');
    for (const subscriber of subscribers) subscriber.onReady?.();
  }

  function retry() {
    if (disposed || unauthorized || starting || retryTimer !== null || !subscribers.size) return;
    const delays = [2000, 5000, 10000, 30000];
    retryTimer = schedule(() => { retryTimer = null; void start(); }, delays[Math.min(failures++, delays.length - 1)]);
  }

  async function start() {
    if (disposed || unauthorized || starting || !subscribers.size) return;
    starting = true;
    setStatus('connecting');
    let failed = false;
    try {
      await connection.start();
      if (!disposed && !unauthorized) { failures = 0; setStatus('connected'); }
    } catch (error) {
      failed = true;
      if (!disposed && !unauthorized) {
        if (authFailureStatus(error)) authenticationFailed(error);
        else setStatus('disconnected');
      }
    } finally {
      starting = false;
      if (failed) retry();
    }
  }

  connection.on('RealtimeReady', ready);
  connection.onreconnecting((error) => {
    if (disposed || unauthorized) return;
    if (authFailureStatus(error)) authenticationFailed(error);
    else setStatus('reconnecting');
  });
  connection.onreconnected(ready);
  connection.onclose((error) => {
    if (disposed || unauthorized) return;
    if (authFailureStatus(error)) authenticationFailed(error);
    else { setStatus('disconnected'); retry(); }
  });

  function dispose() {
    if (disposed) return;
    disposed = true;
    if (retryTimer !== null) cancel(retryTimer);
    retryTimer = null;
    connection.off('RealtimeReady', ready);
    for (const [name, handler] of eventHandlers) connection.off(name, handler);
    eventHandlers.clear();
    subscribers.clear();
    void connection.stop().catch(() => {});
  }

  return {
    authenticationFailed,
    dispose,
    subscribe(subscriber) {
      if (disposed) throw new Error('Realtime connection is already disposed.');
      subscribers.add(subscriber);
      for (const name of subscriber.events || []) {
        if (!eventHandlers.has(name)) {
          const handler = (message) => {
            if (disposed || unauthorized) return;
            for (const current of subscribers)
              if (current.events?.includes(name)) current.onEvent?.(name, message);
          };
          eventHandlers.set(name, handler);
          connection.on(name, handler);
        }
      }
      subscriber.onStatus?.(status);
      if (unauthorized) subscriber.onAuthFailure?.(status === 'unauthorized' ? 401 : 403);
      else if (subscribers.size === 1) void start();
      else if (status === 'connected') subscriber.onReady?.();
      return () => {
        subscribers.delete(subscriber);
        for (const [name, handler] of eventHandlers) {
          if (![...subscribers].some((current) => current.events?.includes(name))) {
            connection.off(name, handler);
            eventHandlers.delete(name);
          }
        }
        if (!subscribers.size) dispose();
      };
    },
  };
}
