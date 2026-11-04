import assert from 'node:assert/strict';
import { test } from 'node:test';
import { authFailureStatus, createRealtimeClient } from '../src/shared/realtime/realtimeClient.js';

class FakeConnection {
  handlers = new Map();
  starts = 0;
  stops = 0;
  failures = [];
  on(name, handler) { this.handlers.set(name, handler); }
  off(name, handler) { if (this.handlers.get(name) === handler) this.handlers.delete(name); }
  onreconnecting(handler) { this.reconnecting = handler; }
  onreconnected(handler) { this.reconnected = handler; }
  onclose(handler) { this.closed = handler; }
  async start() { this.starts++; if (this.failures.length) throw this.failures.shift(); }
  async stop() { this.stops++; }
  emit(name, message) { this.handlers.get(name)?.(message); }
}
const flush = () => new Promise((resolve) => setImmediate(resolve));

test('subscribers share one connection and only receive their registered event names', async () => {
  const connection = new FakeConnection();
  const client = createRealtimeClient({ connection });
  const machines = [], wallets = [], status = [];
  const first = client.subscribe({ events: ['ComputerChanged'], onEvent: (...event) => machines.push(event), onStatus: (value) => status.push(value) });
  const second = client.subscribe({ events: ['WalletChanged'], onEvent: (...event) => wallets.push(event) });
  await flush();
  connection.emit('ComputerChanged', { entityId: 7 });
  connection.emit('WalletChanged', { entityId: 8 });
  assert.equal(connection.starts, 1);
  assert.deepEqual(machines, [['ComputerChanged', { entityId: 7 }]]);
  assert.deepEqual(wallets, [['WalletChanged', { entityId: 8 }]]);
  assert.equal(status.at(-1), 'connected');
  first();
  assert.equal(connection.stops, 0);
  assert.equal(connection.handlers.has('ComputerChanged'), false);
  second();
  assert.equal(connection.stops, 1);
  assert.equal(connection.handlers.size, 0);
});

test('server readiness and a recovered connection both request authoritative refetch', async () => {
  const connection = new FakeConnection();
  const client = createRealtimeClient({ connection });
  let reads = 0;
  const states = [];
  const release = client.subscribe({ onReady: () => reads++, onStatus: (value) => states.push(value) });
  await flush();
  connection.emit('RealtimeReady');
  connection.reconnecting(new Error('Network unavailable'));
  connection.reconnected('new-connection');
  assert.equal(reads, 2);
  assert.deepEqual(states.slice(-3), ['connected', 'reconnecting', 'connected']);
  release();
});

test('initial start failures retry with backoff; cleanup cancels a pending retry', async () => {
  const connection = new FakeConnection();
  connection.failures.push(new Error('Offline'), new Error('Still offline'));
  const timers = new Map();
  const delays = [];
  let sequence = 0;
  const client = createRealtimeClient({ connection,
    schedule: (callback, delay) => { delays.push(delay); timers.set(++sequence, callback); return sequence; },
    cancel: (id) => timers.delete(id),
  });
  const release = client.subscribe({});
  await flush();
  assert.deepEqual(delays, [2000]);
  const callback = timers.get(1); timers.delete(1); callback();
  await flush();
  assert.deepEqual(delays, [2000, 5000]);
  assert.equal(connection.starts, 2);
  release();
  assert.equal(timers.size, 0);
  assert.equal(connection.stops, 1);
});

test('SDK exhausted reconnect schedules another attempt while mounted', async () => {
  const connection = new FakeConnection();
  let retry;
  const states = [];
  const client = createRealtimeClient({ connection, schedule: (callback) => { retry = callback; return 1; }, cancel: () => {} });
  const release = client.subscribe({ onStatus: (value) => states.push(value) });
  await flush();
  connection.closed(new Error('Reconnect exhausted'));
  assert.equal(states.at(-1), 'disconnected');
  retry(); await flush();
  assert.equal(connection.starts, 2);
  release();
});

test('401 and 403 stop retries and notify consumers without leaking exception details', async () => {
  for (const statusCode of [401, 403]) {
    const connection = new FakeConnection();
    connection.failures.push({ statusCode, message: 'secret query token' });
    let retries = 0;
    const errors = [], states = [];
    const client = createRealtimeClient({ connection, schedule: () => { retries++; return 1; } });
    const release = client.subscribe({ onAuthFailure: (value) => errors.push(value), onStatus: (value) => states.push(value) });
    await flush();
    connection.emit('RealtimeReady');
    assert.deepEqual(errors, [statusCode]);
    assert.equal(states.at(-1), statusCode === 401 ? 'unauthorized' : 'forbidden');
    assert.equal(retries, 0);
    release();
  }
  assert.equal(authFailureStatus(new Error('Network error')), null);
});

test('disposed or unauthorized clients ignore late events and reconnect callbacks', async () => {
  const connection = new FakeConnection();
  const client = createRealtimeClient({ connection });
  let events = 0;
  const release = client.subscribe({ events: ['ComputerChanged'], onEvent: () => events++ });
  const oldHandler = connection.handlers.get('ComputerChanged');
  await flush();
  client.authenticationFailed({ statusCode: 401 });
  oldHandler({ entityId: 1 });
  release();
  connection.reconnected(); connection.closed(); oldHandler({ entityId: 2 });
  assert.equal(events, 0);
  assert.equal(connection.stops, 1);
});
