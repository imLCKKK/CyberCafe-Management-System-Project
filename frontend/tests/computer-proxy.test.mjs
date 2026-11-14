import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createServer as createHttpServer } from 'node:http';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createServer, preview } from 'vite';

for (const mode of ['legacy-dev', 'auth-dev', 'preview']) {
test(`Vite ${mode} forwards auth/admin/menu/customers and realtime WebSockets while keeping OP.GG middleware local`, async () => {
  const backendRequests = [];
  const upgrades = [];
  const upgradedSockets = new Set();
  const backend = createHttpServer((request, response) => {
    backendRequests.push({ path: request.url, method: request.method, authorization: request.headers.authorization });
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ proxied: true }));
  });
  backend.on('upgrade', (request, socket) => {
    upgrades.push(request.url);
    upgradedSockets.add(socket);
    socket.on('close', () => upgradedSockets.delete(socket));
    const accept = createHash('sha1').update(request.headers['sec-websocket-key'] + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
    socket.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ' + accept + '\r\n\r\n');
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
    const negotiate = await fetch(`${origin}/hubs/realtime/negotiate?negotiateVersion=1`, { method: 'POST' });
    assert.equal(negotiate.status, 200);
    assert.equal(backendRequests.at(-1).path, '/hubs/realtime/negotiate?negotiateVersion=1');
    const socket = new WebSocket(origin.replace('http:', 'ws:') + '/hubs/realtime?access_token=proxy-test-token');
    try {
      await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('Realtime WebSocket proxy upgrade timed out.')), 3000);
        socket.addEventListener('open', () => { clearTimeout(timer); resolve(); }, { once: true });
        socket.addEventListener('error', (error) => { clearTimeout(timer); reject(error); }, { once: true });
      });
      assert.deepEqual(upgrades, ['/hubs/realtime?access_token=proxy-test-token']);
    } finally {
      socket.close();
      for (const upgraded of upgradedSockets) upgraded.destroy();
    }
  } finally {
    if (previousTarget === undefined) delete process.env.API_PROXY_TARGET;
    else process.env.API_PROXY_TARGET = previousTarget;
    for (const upgraded of upgradedSockets) upgraded.destroy();
    if (previousAuthTarget === undefined) delete process.env.VITE_API_PROXY_TARGET;
    else process.env.VITE_API_PROXY_TARGET = previousAuthTarget;
    if (vite && mode === 'preview') await new Promise(resolve => vite.httpServer.close(resolve));
    else await vite?.close();
    await new Promise((resolve, reject) => backend.close((error) => error ? reject(error) : resolve()));
  }
});
}
