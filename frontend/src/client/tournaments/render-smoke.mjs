import { createServer } from 'vite';
import React from 'react';
import { renderToString } from 'react-dom/server';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
globalThis.localStorage = { getItem: () => null };
const server = await createServer({ root: fileURLToPath(new URL('../../../', import.meta.url)), optimizeDeps: { noDiscovery: true }, resolve: { alias: [{ find: /^(?:\.\.\/)+shared\/auth\/AuthContext(?:\.jsx)?$|^\.\/AuthContext(?:\.jsx)?$/, replacement: fileURLToPath(new URL('../../../tests/auth-render-context.jsx', import.meta.url)) }] }, server: { middlewareMode: true }, appType: 'custom', ssr: { noExternal: ['react-router-dom', 'react-router'], resolve: { conditions: ['module', 'module-sync', 'node', 'development'] } } });
try {
  const { MemoryRouter } = await server.ssrLoadModule('react-router-dom');
  const { AuthProvider } = await server.ssrLoadModule('/tests/auth-render-context.jsx');
  const { TournamentsPage } = await server.ssrLoadModule('/src/client/tournaments/TournamentsPage.jsx');
  const { default: HomePage } = await server.ssrLoadModule('/src/client/home/HomePage.jsx');
  const { ClientDemoProvider } = await server.ssrLoadModule('/src/client/shared/ClientDemoContext.jsx');
  const render = (path) => renderToString(React.createElement(MemoryRouter, { initialEntries: [path] }, React.createElement(AuthProvider, null, React.createElement(ClientDemoProvider, null, React.createElement(path === '/client/home' ? HomePage : TournamentsPage))))).replace(/<!-- -->/g, '');
  const empty = render('/client/tournaments');
  for (const text of ['Giải đấu cộng đồng', 'Chưa có giải đấu']) assert.ok(empty.includes(text), `Missing ${text}`);
  assert.ok(!empty.includes('CHƯA TRIỂN KHAI'));
  assert.ok(!empty.includes('Đấu trường cuối tuần'), 'The SQL tournament route must not render demo tournament data.');
  assert.ok(render('/client/home').includes('Hẹn bạn ở giải đấu tới'));
  console.log('Tournament route render checks passed: API-backed empty state, no demo tournament data.');
} finally { await server.close(); delete globalThis.localStorage; }
