import { createServer } from 'vite';
import React from 'react';
import { renderToString } from 'react-dom/server';
import assert from 'node:assert/strict';
import { createDemoData } from '../home/mockData.js';
import { registerTournament } from '../home/homeModel.js';

let db = createDemoData();
globalThis.localStorage = { getItem: () => JSON.stringify(db) };
const server = await createServer({ server: { middlewareMode: true }, appType: 'custom', ssr: { noExternal: ['react-router-dom', 'react-router'], resolve: { conditions: ['module', 'module-sync', 'node', 'development'] } } });
try {
  const { MemoryRouter } = await server.ssrLoadModule('react-router-dom');
  const { TournamentsPage } = await server.ssrLoadModule('/web/src/client/tournaments/TournamentsPage.jsx');
  const { default: HomePage } = await server.ssrLoadModule('/web/src/client/home/HomePage.jsx');
  const render = (path) => renderToString(React.createElement(MemoryRouter, { initialEntries: [path] }, React.createElement(path === '/client/home' ? HomePage : TournamentsPage))).replace(/<!-- -->/g, '');
  const empty = render('/client/tournaments?tournament=1');
  for (const text of ['Giải đấu cộng đồng', 'Tỷ lệ thắng', 'Headshot', 'Damage / round', 'KDA', 'Sẵn sàng ghi tên']) assert.ok(empty.includes(text), `Missing ${text}`);
  assert.ok(!empty.includes('CHƯA TRIỂN KHAI'));
  db = registerTournament(db, 1, { riotId: 'Player#VN1', rankId: 4, role: 'Duelist' });
  const registered = render('/client/tournaments?tournament=1');
  for (const text of ['Bạn đã đăng ký', 'Player#VN1', 'Gold', 'Chưa đồng bộ', 'Player-VN1']) assert.ok(registered.includes(text), `Missing ${text}`);
  assert.equal((registered.match(/tour-metric missing/g) || []).length, 4);
  assert.ok(!registered.includes('NaN'));
  assert.ok(render('/client/home').includes('Xem đăng ký của bạn'));
  db.Tournaments = [];
  assert.ok(render('/client/tournaments').includes('Chưa có giải đấu Valorant'));
  console.log('Tournament route render checks passed: empty, registered, shared Home state, no tournaments.');
} finally { await server.close(); delete globalThis.localStorage; }
