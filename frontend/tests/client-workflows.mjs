import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, press, screenshot, setupWorkflowLogin } from './cdp.mjs';
const key = 'tram.client.dbml.demo.v2';
const db = () => evaluate(`JSON.parse(localStorage.getItem('${key}'))`);
const close = async () => { await press('Escape'); await until("!document.querySelector('dialog[open]')"); };
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await setupWorkflowLogin('customer', { resetDemo: true });
  // Verify the Home -> cart handoff as well as ordinary wallet checkout.
  await evaluate("document.querySelector('[aria-label=\"Thêm Mì xào bò vào giỏ\"]').click()");
  await until("document.querySelector('.order-cart .client-count')?.textContent === '1'");
  await evaluate("document.querySelector('.order-cart-body > .home-button.primary').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  await click('Xác nhận & thanh toán', 'dialog button');
  await until("Boolean(document.querySelector('.client-orders-list'))");
  assert.equal((await db()).customers[0].wallet_balance, 125000);
  assert.equal((await db()).orders.length, 4);
  await evaluate("document.querySelector('.client-orders-list .home-text-button').click()");
  await click('Yêu cầu hủy', 'dialog button');
  await until("document.querySelector('dialog').textContent.includes('Đang chờ duyệt hủy')");
  assert.equal((await db()).customers[0].wallet_balance, 125000);
  assert.equal((await db()).order_item_cancel_requests.at(-1).status, 'Pending');
  await close();
  await go('/client/home'); assert.ok(await evaluate("document.querySelector('.home-balance').textContent.includes('125.000')"));
  // Trang Ví và Tài khoản đã đọc dữ liệu từ API/SQL nên không còn kiểm tra bằng localStorage demo ở đây:
  // xem wallet-customers-api.mjs (API giả có kiểm soát) và Backend/tests/CyberManagement.Wallet.Checks (SQL Server thật).
  // Exercise real tournament UI while substituting a deterministic unavailable
  // external provider. No third-party profile is contacted by this suite.
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `const realFetch = window.fetch; window.fetch = (url, options) => String(url).includes('/api/valorant/opgg/stats') ? Promise.resolve(new Response(JSON.stringify({ message: 'Dịch vụ thống kê tạm ngừng' }), { status: 503, headers: { 'Content-Type': 'application/json' } })) : realFetch(url, options);` });
  await go('/client/tournaments');
  await click('Đăng ký tham gia');
  await input('dialog input', 'InvalidRiot'); await input('dialog select', 'Duelist');
  assert.equal(await evaluate("document.querySelector('dialog form').checkValidity()"), false, 'Tag is required');
  await input('dialog input', 'FrontendTester#VN1'); await click('Xác nhận đăng ký', 'dialog button');
  await until("!document.querySelector('dialog[open]') && document.body.textContent.includes('FrontendTester#VN1')");
  await until("!document.querySelector('.tour-section-heading button').disabled");
  assert.equal(await evaluate("document.querySelectorAll('.tour-metric.missing').length"), 4);
  await input('.tour-search input', 'does-not-exist');
  await until("document.querySelector('.tour-empty')?.textContent.includes('Không tìm thấy')");
  await click('Xóa bộ lọc');
  await until("document.querySelectorAll('.tour-table tbody tr').length === 1");
  await go('/client/home'); assert.ok(await evaluate("document.body.textContent.includes('Xem đăng ký của bạn')"));
  await screenshot('client-workflows-complete.png');
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['Home cart handoff and wallet checkout', 'cancellation without early refund', 'balance sync', 'tournament registration/validation/filter/Home sync', 'unavailable stats remain missing'] }));
} finally { socket.close(); }
