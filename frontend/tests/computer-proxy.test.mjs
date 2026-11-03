import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createServer as createHttpServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import { createServer } from 'vite';

test('Vite forwards auth/admin to the configured backend and keeps OP.GG middleware local', async () => {
  const backendRequests = [];
  const backend = createHttpServer((request, response) => {
    backendRequests.push({ path: request.url, method: request.method, authorization: request.headers.authorization });
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ proxied: true }));
  });
  await new Promise((resolve) => backend.listen(0, '127.0.0.1', resolve));
  const previousTarget = process.env.API_PROXY_TARGET;
  let vite;
  try {
    process.env.API_PROXY_TARGET = `http://127.0.0.1:${backend.address().port}`;
    vite = await createServer({
      root: fileURLToPath(new URL('..', import.meta.url)),
      server: { host: '127.0.0.1', port: 0 },
      optimizeDeps: { noDiscovery: true, include: [] },
    });
    await vite.listen();
    const origin = vite.resolvedUrls.local[0].replace(/\/$/, '');
    for (const [path, method] of [['/api/auth/me', 'GET'], ['/api/admin/computers', 'GET'], ['/api/admin/computers/101/status', 'PATCH']]) {
      const response = await fetch(`${origin}${path}`, { method, headers: { Authorization: 'Bearer proxy-test-token' } });
      assert.equal(response.status, 200);
      assert.deepEqual(await response.json(), { proxied: true });
      assert.deepEqual(backendRequests.at(-1), { path, method, authorization: 'Bearer proxy-test-token' });
    }
    const opgg = await fetch(`${origin}/api/valorant/opgg/stats`, { method: 'POST' });
    assert.equal(opgg.status, 405);
    assert.equal(opgg.headers.get('Allow'), 'GET');
    assert.equal(backendRequests.length, 3, 'OP.GG must not be forwarded to .NET');
  } finally {
    if (previousTarget === undefined) delete process.env.API_PROXY_TARGET;
    else process.env.API_PROXY_TARGET = previousTarget;
    await vite?.close();
    await new Promise((resolve, reject) => backend.close((error) => error ? reject(error) : resolve()));
  }
});
