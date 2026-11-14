import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const server = await createServer({ root, optimizeDeps: { noDiscovery: true }, server: { middlewareMode: true }, appType: 'custom', ssr: { noExternal: ['react-router-dom', 'react-router'], resolve: { conditions: ['module', 'module-sync', 'node', 'development'] } } });
try {
  const { CounterTopUpAmounts } = await server.ssrLoadModule('/src/shared/CounterTopUpAmounts.jsx');
  const { ApproveDialog } = await server.ssrLoadModule('/src/admin/topups/TopUpsPage.jsx');
  const request = { requestId: 7, customerId: 9, customerName: 'Khách <SQL>', customerPhone: '0900000009', amount: 500000, bonusAmount: 100000, totalAmount: 600000, status: 'Pending' };
  const amounts = renderToStaticMarkup(React.createElement(CounterTopUpAmounts, { request }));
  for (const label of ['Tiền mặt cần thu', 'Tiền tặng', 'Tổng cộng vào ví']) assert.ok(amounts.includes(label));
  for (const value of ['500.000', '100.000', '600.000']) assert.ok(amounts.includes(value));
  assert.equal((amounts.match(/<dt>/g) || []).length, 3);
  const dialog = renderToStaticMarkup(React.createElement(ApproveDialog, { request, onClose() {}, onApproved() {} }));
  assert.ok(dialog.includes('Khách &lt;SQL&gt;'));
  assert.match(dialog, /type="checkbox"/);
  assert.match(dialog, /type="submit" disabled=""/);
  assert.ok(dialog.includes('đã nhận đủ'));
  assert.ok(!dialog.includes('checked=""'));
  assert.ok(dialog.includes('không được tính là doanh thu'));
  assert.ok(!dialog.includes('NaN'));
  console.log('Counter topup render: three authoritative amounts, escaped identity and cash confirmation required before approval.');
} finally { await server.close(); }
