import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { createSignalRClient } from '../src/shared/realtime/sharedRealtime.js';

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
