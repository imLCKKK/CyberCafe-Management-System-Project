import { createServer } from 'vite';
import React from 'react';
import { renderToString } from 'react-dom/server';
import assert from 'node:assert/strict';

// Render through Vite so JSX and CSS use the same module pipeline as the app.
// This checks render-time exceptions, not visual layout or browser interactions.
globalThis.localStorage = { getItem: () => null };
const server = await createServer({
  server: { middlewareMode: true },
  appType: 'custom',
  ssr: {
    noExternal: ['react-router-dom', 'react-router'],
    resolve: { conditions: ['module', 'module-sync', 'node', 'development'] },
  },
});
try {
  const { MemoryRouter } = await server.ssrLoadModule('react-router-dom');
  const { default: HomePage } = await server.ssrLoadModule('/web/src/client/home/HomePage.jsx');
  const html = renderToString(React.createElement(MemoryRouter, { initialEntries: ['/client/home'] }, React.createElement(HomePage))).replace(/<!-- -->/g, '');
  for (const text of ['Chào Minh Anh', '02:15:00', 'Nạp tiền vào ví', 'Mì xào bò', 'Đấu trường cuối tuần', 'Hoạt động gần đây']) {
    assert.ok(html.includes(text), `Missing rendered content: ${text}`);
  }
  assert.ok(!html.includes('NaN'));
  console.log('HomePage render smoke test passed: all dashboard sections rendered without runtime errors.');
} finally {
  await server.close();
  delete globalThis.localStorage;
}
