import assert from 'node:assert/strict';
import React from 'react';
import { renderToString } from 'react-dom/server';
import { createServer } from 'vite';

// Render all menu routes through Vite, including their real shared providers.
// Browser interactions and chart amounts are checked separately.
globalThis.localStorage = { getItem: () => null };
globalThis.window = { addEventListener() {}, removeEventListener() {} };
const originalError = console.error;
const warnings = [];
console.error = (...args) => { if (!String(args[0]).includes('useLayoutEffect does nothing on the server')) warnings.push(String(args[0])); };
const server = await createServer({ server: { middlewareMode: true }, appType: 'custom', ssr: { noExternal: ['react-router-dom', 'react-router'], resolve: { conditions: ['module', 'module-sync', 'node', 'development'] } } });
try {
  const { MemoryRouter } = await server.ssrLoadModule('react-router-dom');
  const { AppRoutes } = await server.ssrLoadModule('/web/src/app/routes.jsx');
  const pages = { dashboard: 'Xin chào, Admin', computers: 'Quản lý máy tính', customers: 'Quản lý khách hàng', orders: 'Quản lý gọi món', products: 'Quản lý sản phẩm', inventory: 'Quản lý kho', invoices: 'Hóa đơn &amp; thanh toán', tournaments: 'Quản lý giải đấu', staff: 'Quản lý nhân viên', reports: 'Báo cáo &amp; thống kê' };
  for (const [path, title] of Object.entries(pages)) {
    const html = renderToString(React.createElement(MemoryRouter, { initialEntries: [`/admin/${path}`] }, React.createElement(AppRoutes)));
    assert.ok(html.includes(title), `Missing title for ${path}`);
    assert.ok(!html.includes('CHƯA TRIỂN KHAI'), `Placeholder in ${path}`);
    assert.ok(!html.includes('NaN'), `Invalid numeric output in ${path}`);
    if (['reports', 'dashboard'].includes(path)) assert.ok(html.includes('<svg'), `Missing chart in ${path}`);
  }
  assert.deepEqual(warnings, [], 'Unexpected React render warnings');
  console.log('Admin render smoke: all 10 menu routes rendered with shared data and no placeholders.');
} finally {
  await server.close(); console.error = originalError;
  delete globalThis.localStorage; delete globalThis.window;
}
