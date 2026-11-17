import assert from 'node:assert/strict';
import { cdp, evaluate, until, screenshot, socket, setupWorkflowLogin, go, click, input } from './cdp.mjs';

// UI-state fixture only. The separate invoices-live suite uses real HTTP/JWT/SQL.
const fixture = { id: 1, customerId: 7, customerName: 'Workflow Customer', orderIds: [12], gamingSessionId: null,
  actorType: 'Staff', employeeId: 3, issuedBy: 'Cashier', issuedAt: '2026-11-15T12:00:00Z', subtotal: 100000, discount: 10000,
  grossAmount: 90000, cancelledAmount: 90000, refundAmount: 90000, paidAmount: 0, amountDue: 0, overpaidAmount: 0, status: 'Cancelled',
  lines: [{ id: 1, orderDetailId: 1, sessionChargeId: null, description: '<script>snapshot food</script>', quantity: 1, unitPrice: 100000, subtotal: 100000, discountAmount: 10000, amount: 90000, cancelledAmount: 90000 }], payments: [
    { id: 1, type: 'Payment', method: 'ZaloPay', status: 'Succeeded', amount: 90000, walletTransactionId: null,
      paidAt: '2026-11-16T16:55:00Z', paymentTime: '2026-11-16T16:55:00Z', confirmedAt: '2026-11-16T16:55:01Z',
      providerOccurredAt: '2026-11-16T16:55:00Z', paymentTimeBasis: 'Source' },
    { id: 2, type: 'Refund', method: 'ZaloPay', status: 'Succeeded', amount: 90000, walletTransactionId: null,
      paidAt: '2026-11-16T17:03:00Z', paymentTime: '2026-11-16T17:03:00Z', confirmedAt: '2026-11-16T17:03:00Z',
      providerOccurredAt: null, paymentTimeBasis: 'Confirmation' }] };
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await setupWorkflowLogin('customer');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window); window.__invoiceMode = 'data';
    window.fetch = async (request, options) => {
      const path = new URL(typeof request === 'string' ? request : request.url, location.href).pathname;
      if (path.startsWith('/api/client/invoices')) {
        if (window.__invoiceMode === 'error') return Response.json({ detail: 'Invoice connection failed' }, { status: 503 });
        return Response.json(path === '/api/client/invoices' ? (window.__invoiceMode === 'empty' ? [] : [${JSON.stringify(fixture)}]) : ${JSON.stringify(fixture)});
      }
      return originalFetch(request, options);
    };
  })()` });
  await go('/client/invoices', 'document.querySelector(".invoice-workspace")');
  await until('document.querySelectorAll(".sql-invoice-table tbody tr").length === 1');
  await input('.sql-invoice-search input', 'not-found'); await until('document.body.innerText.includes("Không có hóa đơn phù hợp")');
  await input('.sql-invoice-search input', ''); await until('document.querySelectorAll(".sql-invoice-table tbody tr").length === 1');
  await evaluate('document.querySelector(".sql-invoice-table tbody button").click()');
  await until('Boolean(document.querySelector("dialog .invoice-snapshot"))');
  assert.equal(await evaluate('document.querySelector("dialog .invoice-snapshot script")'), null);
  assert.ok(await evaluate('document.querySelector("dialog .invoice-snapshot").textContent.includes("<script>snapshot food</script>")'));
  assert.ok(await evaluate('document.querySelector("dialog .invoice-money-history").textContent.includes("Ghi nhận theo thời điểm hệ thống xác nhận")'));
  assert.ok(await evaluate('document.querySelector("dialog .invoice-money-history").textContent.includes("Chưa có thời điểm giao dịch từ nhà cung cấp")'));
  assert.ok(await evaluate('document.querySelector(".invoice-print-root").textContent.includes("00:03:00")'));
  await screenshot('invoices-fixture-detail.png');
  await evaluate('document.querySelector("dialog .home-icon-button").click()');
  await evaluate('window.__invoiceMode = "error"'); await click('Tải lại');
  await until('Boolean(document.querySelector(".invoice-workspace [role=alert]"))');
  assert.equal(await evaluate('document.querySelectorAll(".sql-invoice-table tbody tr").length'), 0);
  await evaluate('window.__invoiceMode = "empty"'); await click('Thử lại');
  await until('document.body.innerText.includes("Chưa có hóa đơn được phát hành")');
  console.log('PASS invoice UI fixture: search, escaped snapshot, detail, API error, empty/recovery; no demo fallback.');
} finally { socket.close(); }
