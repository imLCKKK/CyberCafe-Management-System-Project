import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createServer as createHttpServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import { createServer, preview } from 'vite';

for (const mode of ['legacy-dev', 'auth-dev', 'preview']) {
test(`Vite ${mode} forwards auth/admin/menu/customers and keeps OP.GG middleware local`, async () => {
  const backendRequests = [];
  const backend = createHttpServer((request, response) => {
    backendRequests.push({ path: request.url, method: request.method, authorization: request.headers.authorization });
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ proxied: true }));
  });
  await new Promise((resolve) => backend.listen(0, '127.0.0.1', resolve));
  const previousTarget = process.env.API_PROXY_TARGET;
  const previousAuthTarget = process.env.VITE_API_PROXY_TARGET;
  let vite;
  try {
    process.env.API_PROXY_TARGET = `http://127.0.0.1:${backend.address().port}`;
    delete process.env.VITE_API_PROXY_TARGET;
    if (mode !== 'legacy-dev') {
      process.env.VITE_API_PROXY_TARGET = process.env.API_PROXY_TARGET;
      process.env.API_PROXY_TARGET = 'http://127.0.0.1:1'; // S1-D-2 setting takes precedence.
    }
    const config = {
      root: fileURLToPath(new URL('..', import.meta.url)),
      server: { host: '127.0.0.1', port: 0 },
      preview: { host: '127.0.0.1', port: 0 },
      optimizeDeps: { noDiscovery: true, include: [] },
    };
    vite = mode === 'preview' ? await preview(config) : await createServer(config);
    if (mode !== 'preview') await vite.listen();
    const origin = vite.resolvedUrls.local[0].replace(/\/$/, '');
    const routes = [
      ['/api/auth/me', 'GET'],
      ['/api/admin/computers', 'GET'],
      ['/api/admin/computers/101/status', 'PATCH'],
      ['/api/admin/products', 'GET'],
      ['/api/admin/inventory', 'GET'],
      ['/api/menu', 'GET'],
      ['/api/customers/me', 'GET'],
      ['/api/customers/me/wallet', 'GET'],
    ];
    for (const [path, method] of routes) {
      const response = await fetch(`${origin}${path}`, { method, headers: { Authorization: 'Bearer proxy-test-token' } });
      assert.equal(response.status, 200);
      assert.match(response.headers.get('Content-Type') || '', /application\/json/);
      assert.deepEqual(await response.json(), { proxied: true });
      assert.deepEqual(backendRequests.at(-1), { path, method, authorization: 'Bearer proxy-test-token' });
    }
    const opgg = await fetch(`${origin}/api/valorant/opgg/stats`, { method: 'POST' });
    assert.equal(opgg.status, 405);
    assert.equal(opgg.headers.get('Allow'), 'GET');
    assert.equal(backendRequests.length, routes.length, 'OP.GG must not be forwarded to .NET');
  } finally {
    if (previousTarget === undefined) delete process.env.API_PROXY_TARGET;
    else process.env.API_PROXY_TARGET = previousTarget;
    if (previousAuthTarget === undefined) delete process.env.VITE_API_PROXY_TARGET;
    else process.env.VITE_API_PROXY_TARGET = previousAuthTarget;
    if (vite && mode === 'preview') await new Promise(resolve => vite.httpServer.close(resolve));
    else await vite?.close();
    await new Promise((resolve, reject) => backend.close((error) => error ? reject(error) : resolve()));
  }
});
}
