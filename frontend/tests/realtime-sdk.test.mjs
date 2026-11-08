import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { createSignalRClient } from '../src/shared/realtime/sharedRealtime.js';

const waitFor = async (predicate) => {
  for (let attempt = 0; attempt < 150; attempt++) {
    if (predicate()) return;
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  throw Error('Timed out waiting for the official SDK.');
};

for (const statusCode of [401, 403]) {
  test(`official SignalR SDK reports HTTP ${statusCode} before negotiate errors lose their status`, { timeout: 5000 }, async () => {
    const requests = [];
    const server = createServer((request, response) => {
      requests.push({ path: request.url, token: request.headers.authorization });
      response.writeHead(statusCode, { 'Content-Type': 'application/json' });
      response.end('null');
    });
    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    const client = createSignalRClient('sdk-test-token', `http://127.0.0.1:${server.address().port}/hubs/realtime`);
    let release;
    try {
      const states = [];
      const reported = await new Promise((resolve) => {
        release = client.subscribe({ onStatus: (status) => states.push(status), onAuthFailure: resolve });
      });
      await new Promise((resolve) => setImmediate(resolve));
      assert.equal(reported, statusCode);
      assert.equal(states.at(-1), statusCode === 401 ? 'unauthorized' : 'forbidden');
      assert.equal(requests.length, 1);
      assert.ok(requests[0].path.startsWith('/hubs/realtime/negotiate?'));
      assert.equal(requests[0].token, 'Bearer sdk-test-token');
    } finally {
      release?.();
      client.dispose();
      server.closeAllConnections();
      await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
    }
  });
}

for (const statusCode of [401, 403]) {
  test(`official SDK stops reconnect retries after HTTP ${statusCode} until a new session`, { timeout: 10000 }, async () => {
    const negotiates = [];
    const polls = new Set();
    const connections = new Map();
    let rejectOldSession = false;
    const server = createServer((request, response) => {
      const url = new URL(request.url, 'http://localhost');
      if (url.pathname.endsWith('/negotiate')) {
        const token = request.headers.authorization;
        negotiates.push(token);
        if (rejectOldSession && token === 'Bearer old-session') {
          response.writeHead(statusCode); response.end(); return;
        }
        const id = String(connections.size + 1);
        connections.set(id, { firstPoll: true, handshake: false, poll: null });
        response.setHeader('Content-Type', 'application/json');
        response.end(JSON.stringify({ negotiateVersion: 1, connectionId: id, connectionToken: id,
          availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text'] }] }));
        return;
      }
      const state = connections.get(url.searchParams.get('id'));
      if (request.method === 'GET' && state) {
        if (state.firstPoll) { state.firstPoll = false; response.end(); return; }
        if (state.handshake) { state.handshake = false; response.end('{}\u001e'); return; }
        state.poll = response;
        polls.add(response);
        response.once('close', () => { polls.delete(response); if (state.poll === response) state.poll = null; });
      } else if (request.method === 'POST' && state) {
        let body = '';
        request.on('data', chunk => { body += chunk; });
        request.on('end', () => {
          if (body.includes('"protocol"')) {
            if (state.poll) state.poll.end('{}\u001e');
            else state.handshake = true;
          }
          response.end();
        });
      } else { response.writeHead(202); response.end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const url = `http://127.0.0.1:${server.address().port}/hubs/realtime`;
    const client = createSignalRClient('old-session', url);
    const states = [], failures = [];
    let release, fresh, releaseFresh;
    try {
      release = client.subscribe({ onStatus: value => states.push(value), onAuthFailure: code => failures.push(code) });
      await waitFor(() => states.at(-1) === 'connected' && polls.size > 0);
      rejectOldSession = true;
      for (const poll of polls) { poll.writeHead(500); poll.end(); }
      await waitFor(() => failures.length > 0);
      // The SDK's second retry is due after 2s, even though its error has lost statusCode.
      await new Promise(resolve => setTimeout(resolve, 2300));
      assert.deepEqual(failures, [statusCode]);
      assert.equal(states.at(-1), statusCode === 401 ? 'unauthorized' : 'forbidden');
      assert.deepEqual(negotiates, ['Bearer old-session', 'Bearer old-session']);
      release();
      fresh = createSignalRClient('new-session', url);
      let connected = false;
      releaseFresh = fresh.subscribe({ onStatus: value => { connected = value === 'connected'; } });
      await waitFor(() => connected);
      assert.equal(negotiates.at(-1), 'Bearer new-session');
    } finally {
      release?.(); releaseFresh?.(); client.dispose(); fresh?.dispose();
      for (const poll of polls) poll.end();
      server.closeAllConnections();
      await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
    }
  });
}
