import { createServer } from 'vite';
import React from 'react';
import { renderToString } from 'react-dom/server';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

// Render through Vite so JSX and CSS use the same module pipeline as the app.
// This checks render-time exceptions, not visual layout or browser interactions.
globalThis.localStorage = { getItem: () => null };
const server = await createServer({
  root: fileURLToPath(new URL('../../../', import.meta.url)),
  optimizeDeps: { noDiscovery: true },
  resolve: { alias: [{ find: /^(?:\.\.\/)+shared\/auth\/AuthContext(?:\.jsx)?$|^\.\/AuthContext(?:\.jsx)?$/, replacement: fileURLToPath(new URL('../../../tests/auth-render-context.jsx', import.meta.url)) }] },
  server: { middlewareMode: true },
  appType: 'custom',
  ssr: {
    noExternal: ['react-router-dom', 'react-router'],
    resolve: { conditions: ['module', 'module-sync', 'node', 'development'] },
  },
});
try {
  const { MemoryRouter } = await server.ssrLoadModule('react-router-dom');
  const { AuthProvider } = await server.ssrLoadModule('/tests/auth-render-context.jsx');
  const { default: HomePage } = await server.ssrLoadModule('/src/client/home/HomePage.jsx');
  const { ClientDemoProvider } = await server.ssrLoadModule('/src/client/shared/ClientDemoContext.jsx');
  const html = renderToString(React.createElement(MemoryRouter, { initialEntries: ['/client/home'] }, React.createElement(AuthProvider, null, React.createElement(ClientDemoProvider, null, React.createElement(HomePage))))).replace(/<!-- -->/g, '');
  for (const text of ['Chào Minh Anh', '2 giờ 15 phút', 'Nạp tiền vào ví', 'Đang tải thực đơn…', 'Hẹn bạn ở giải đấu tới', 'Hoạt động gần đây']) {
    assert.ok(html.includes(text), `Missing rendered content: ${text}`);
  }
  assert.ok(!html.includes('NaN'));
  console.log('HomePage render smoke test passed: all dashboard sections rendered without runtime errors.');
} finally {
  await server.close();
  delete globalThis.localStorage;
}
