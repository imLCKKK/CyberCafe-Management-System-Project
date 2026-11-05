// Kiểm tra giao diện Ví/Tài khoản của khách và trang Khách hàng của admin sau khi nối API (S1-M-1).
// API được GIẢ LẬP trong trình duyệt (ghi đè window.fetch) để kiểm soát loading/lỗi/401 và ghi lại request.
// Đăng nhập dùng PHIÊN CHUNG của shared/auth (localStorage 'cybermanagement.auth.v1'), không có kho token riêng.
// Đây KHÔNG phải bằng chứng về SQL Server: phần đó do Backend/tests/CyberManagement.Wallet.Checks kiểm tra.
import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, press, screenshot } from './cdp.mjs';

const mockApi = () => {
  const realFetch = window.fetch.bind(window);
  const KEY = '__mockApi';
  const read = () => JSON.parse(sessionStorage.getItem(KEY) || 'null');
  const write = (state) => sessionStorage.setItem(KEY, JSON.stringify(state));
  if (!read()) {
    write({
      mode: 'ok', calls: [], nextCustomer: 3, nextTx: 3,
      customers: [
        { customerId: 1, fullName: 'Nguyễn Văn A', phone: '0900000001', email: null, walletBalance: 125000, status: 'Active', createdAt: '2026-10-01T03:00:00Z', hasActiveSession: false },
        { customerId: 2, fullName: 'Trần Thị B', phone: '0900000002', email: 'b@example.test', walletBalance: 0, status: 'Locked', createdAt: '2026-10-02T03:00:00Z', hasActiveSession: false },
      ],
      transactions: {
        1: [
          { transactionId: 2, customerId: 1, amount: -25000, balanceAfter: 125000, type: 'OrderPayment', description: 'Đơn #5', createdAt: '2026-10-03T04:00:00Z' },
          { transactionId: 1, customerId: 1, amount: 150000, balanceAfter: 150000, type: 'TopUp', description: 'Nạp tại quầy (Cash)', createdAt: '2026-10-03T03:00:00Z' },
        ],
        2: [],
      },
    });
  }
  const reply = (status, body) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
  window.fetch = async (url, options = {}) => {
    const path = String(url);
    if (!/^\/api\/(client|admin|auth)\//.test(path)) return realFetch(url, options);
    const state = read();
    const method = (options.method || 'GET').toUpperCase();
    const body = options.body ? JSON.parse(options.body) : null;
    const headers = new Headers(options.headers || {});
    const auth = headers.get('Authorization');
    const idem = headers.get('Idempotency-Key');
    state.calls.push({ method, path, auth, idem, body });
    const done = (status, payload) => { write(state); return reply(status, payload); };

    // Endpoint của phiên chung (xác minh đăng nhập/đăng xuất) luôn hoạt động, kể cả khi dữ liệu nghiệp vụ giả lập lỗi.
    if (path === '/api/auth/customer/me') {
      const owner = { 'Bearer test-client-token': 1, 'Bearer test-client-token-2': 2 }[auth];
      const customer = state.customers.find((row) => row.customerId === owner);
      return customer ? done(200, { customerId: customer.customerId, phone: customer.phone, fullName: customer.fullName }) : done(401, { message: 'Chưa đăng nhập' });
    }
    if (path === '/api/auth/me') {
      return auth === 'Bearer test-staff-token'
        ? done(200, { employeeId: 1, username: 'admin', role: 'Admin', permissions: ['customer.view', 'customer.create', 'customer.update', 'customer.status.change', 'wallet.view', 'wallet.topup'] })
        : done(401, { message: 'Chưa đăng nhập' });
    }
    if (path === '/api/auth/logout' || path === '/api/auth/customer/logout') return done(204, null);
    if (state.mode === 'error') return done(500, { title: 'Lỗi máy chủ giả lập' });
    if (path.startsWith('/api/client/')) {
      const owner = { 'Bearer test-client-token': 1, 'Bearer test-client-token-2': 2 }[auth];
      if (!owner) return done(401, { message: 'Chưa đăng nhập' });
      const customer = state.customers.find((row) => row.customerId === owner);
      if (path === '/api/client/profile') return done(200, customer);
      if (path === '/api/client/wallet') return done(200, { walletBalance: customer.walletBalance, transactions: state.transactions[owner] || [] });
      return done(404, { title: 'Không tìm thấy' });
    }
    if (auth !== 'Bearer test-staff-token') return done(401, { message: 'Chưa đăng nhập' });
    const detail = path.match(/^\/api\/admin\/customers\/(\d+)(\/.*)?$/);
    const found = detail && state.customers.find((row) => row.customerId === Number(detail[1]));
    if (path === '/api/admin/customers' && method === 'GET') return done(200, [...state.customers].sort((a, b) => b.customerId - a.customerId));
    if (path === '/api/admin/customers' && method === 'POST') {
      if (state.customers.some((row) => row.phone === body.phone)) return done(409, { title: 'Xung đột dữ liệu', detail: 'Số điện thoại đã tồn tại.' });
      const created = { customerId: state.nextCustomer++, fullName: body.fullName, phone: body.phone, email: body.email, walletBalance: 0, status: 'Active', createdAt: new Date().toISOString(), hasActiveSession: false };
      state.customers.push(created); state.transactions[created.customerId] = [];
      return done(201, created);
    }
    if (!found) return done(404, { title: 'Không tìm thấy khách hàng.' });
    if (!detail[2] && method === 'PUT') { Object.assign(found, { fullName: body.fullName, phone: body.phone, email: body.email }); return done(200, found); }
    if (detail[2] === '/status' && method === 'PUT') { found.status = body.status; return done(200, found); }
    if (detail[2] === '/wallet-transactions') return done(200, state.transactions[found.customerId] || []);
    if (detail[2] === '/wallet/top-ups' && method === 'POST') {
      // Giống server thật: bắt buộc Idempotency-Key; cùng khóa + cùng số tiền thì trả lại giao dịch cũ (200), khác số tiền thì 409.
      if (!idem) return done(400, { title: 'Dữ liệu không hợp lệ', detail: 'Thiếu khóa chống lặp Idempotency-Key.' });
      const existing = state.transactions[found.customerId].find((row) => row.idempotencyKey === idem);
      if (existing) {
        if (existing.amount !== body.amount) return done(409, { title: 'Không thể thực hiện thao tác', detail: 'Khóa chống lặp này đã dùng cho một yêu cầu khác.' });
        return done(200, existing);
      }
      if (state.topUpFail === 'before') return done(500, { title: 'Lỗi máy chủ giả lập' });
      found.walletBalance += body.amount;
      const row = { transactionId: state.nextTx++, customerId: found.customerId, amount: body.amount, balanceAfter: found.walletBalance, type: 'TopUp', description: `Nạp tại quầy (${body.collectionMethod}) - nhân viên #1`, createdAt: new Date().toISOString(), idempotencyKey: idem };
      state.transactions[found.customerId].unshift(row);
      // 'after': server đã cộng ví nhưng phản hồi bị mất (mạng) -> người dùng thấy lỗi và bấm gửi lại.
      if (state.topUpFail === 'after') return done(500, { title: 'Mất kết nối giả lập' });
      return done(201, row);
    }
    return done(404, { title: 'Không tìm thấy' });
  };
};

const mock = (patch) => evaluate(`(() => { const s = JSON.parse(sessionStorage.getItem('__mockApi')); Object.assign(s, ${JSON.stringify(patch)}); sessionStorage.setItem('__mockApi', JSON.stringify(s)); })()`);
const calls = () => evaluate("JSON.parse(sessionStorage.getItem('__mockApi')).calls");
// Phiên chung: { accessToken, accountType: 'customer' | 'employee' } trong localStorage; AuthProvider xác minh bằng /api/auth/(customer/)me.
const token = (kind, value) => evaluate(value
  ? `localStorage.setItem('cybermanagement.auth.v1', JSON.stringify({ accessToken: ${JSON.stringify(value)}, accountType: ${JSON.stringify(kind === 'client' ? 'customer' : 'employee')} }))`
  : `localStorage.removeItem('cybermanagement.auth.v1')`);
const path = () => evaluate('location.pathname');
const text = () => evaluate('document.body.innerText');
const submit = () => evaluate("document.querySelector('dialog[open] form').requestSubmit()");
const closeDialog = async () => { await press('Escape'); await until("!document.querySelector('dialog[open]')"); };
const rowAction = (rowText, action) => evaluate(`(() => { const row = [...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes(${JSON.stringify(rowText)})); if (!row) throw Error('Missing row: ' + ${JSON.stringify(rowText)}); const b = [...row.querySelectorAll('button')].find((x) => x.textContent.trim() === ${JSON.stringify(action)}); if (!b) throw Error('Missing action: ' + ${JSON.stringify(action)}); b.click(); })()`);
const rows = () => evaluate("document.querySelectorAll('tbody tr').length");

try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(${mockApi.toString()})()` });
  await go('/client/auth', 'document.querySelector(".auth-form")'); await evaluate('localStorage.clear(); sessionStorage.clear()');

  // ---- Ví của khách: số dư và lịch sử đến từ API, đúng chủ tài khoản, reload vẫn đúng.
  await token('client', 'test-client-token');
  await go('/client/wallet'); await until("Boolean(document.querySelector('.home-balance'))");
  assert.ok(await evaluate("document.querySelector('.home-balance').textContent.includes('125.000')"), 'Số dư từ API');
  assert.equal(await rows(), 2, 'Hai giao dịch từ API');
  assert.ok((await text()).includes('Nguyễn Văn A'), 'Tên khách từ API');
  assert.ok((await text()).includes('Thanh toán đơn món') && (await text()).includes('Nạp tiền vào ví'));
  assert.ok((await calls()).some((c) => c.path === '/api/client/wallet' && c.auth === 'Bearer test-client-token'), 'Gửi Bearer token');
  await input('[aria-label="Lọc giao dịch"]', 'in');
  assert.ok(await evaluate("[...document.querySelectorAll('tbody .home-amount')].every((el) => el.textContent.startsWith('+'))"));
  await input('[aria-label="Lọc giao dịch"]', 'out');
  assert.ok(await evaluate("[...document.querySelectorAll('tbody .home-amount')].every((el) => el.textContent.startsWith('−'))"));
  await cdp('Page.reload'); await until("Boolean(document.querySelector('.home-balance'))");
  assert.ok(await evaluate("document.querySelector('.home-balance').textContent.includes('125.000')"), 'Reload vẫn đúng số dư');
  assert.equal(await rows(), 2);

  // ---- API lỗi / chưa đăng nhập: hiện lỗi, KHÔNG hiện số dư mẫu; thử lại được.
  await mock({ mode: 'error' }); await cdp('Page.reload'); await until("Boolean(document.querySelector('[role=alert]'))");
  assert.ok((await text()).includes('Lỗi máy chủ giả lập'));
  assert.equal(await evaluate("Boolean(document.querySelector('.home-balance'))"), false, 'Không có số dư khi API lỗi');
  assert.equal((await text()).includes('125.000'), false, 'Không rò số dư demo/cũ khi API lỗi');
  await mock({ mode: 'ok' }); await click('Thử lại'); await until("Boolean(document.querySelector('.home-balance'))");

  // ---- Chưa đăng nhập (không có phiên chung): về trang đăng nhập, không hiện số dư, không gọi API ví.
  // Token riêng kiểu cũ trong sessionStorage KHÔNG còn được đọc (trước đây module đọc kho riêng nên login chung bị token rỗng).
  await evaluate("sessionStorage.setItem('cybercafe.client.accessToken', 'legacy-private-token')");
  const beforeLoggedOut = (await calls()).length;
  await token('client', ''); await cdp('Page.reload'); await until("location.pathname === '/client/auth'");
  assert.equal(await evaluate("Boolean(document.querySelector('.home-balance'))"), false, 'Không có số dư khi chưa đăng nhập');
  assert.equal((await calls()).slice(beforeLoggedOut).filter((c) => c.path.startsWith('/api/client/')).length, 0, 'Chưa đăng nhập thì không gọi API khách');
  assert.equal((await calls()).some((c) => c.auth === 'Bearer legacy-private-token'), false, 'Không đọc token riêng cũ');
  await go('/client/wallet', 'document.querySelector(".auth-form")');
  assert.equal(await path(), '/client/auth', 'Vào thẳng trang ví khi chưa đăng nhập bị đưa về đăng nhập');
  await evaluate("sessionStorage.removeItem('cybercafe.client.accessToken')");

  // ---- Tài khoản: đọc hồ sơ đúng người đăng nhập; không dùng dữ liệu demo.
  await token('client', 'test-client-token'); await go('/client/account');
  await until("document.body.innerText.includes('0900000001')");
  assert.ok((await text()).includes('Nguyễn Văn A'));
  // Đổi tài khoản (đăng xuất rồi đăng nhập khách khác): mọi request sau đó chỉ mang token mới, không dùng lại token cũ.
  const beforeSwitch = (await calls()).length;
  await token('client', ''); await token('client', 'test-client-token-2'); await cdp('Page.reload');
  await until("document.body.innerText.includes('0900000002')");
  assert.equal((await text()).includes('Nguyễn Văn A'), false, 'Khách khác không thấy hồ sơ của khách A');
  const afterSwitch = (await calls()).slice(beforeSwitch).filter((c) => c.auth);
  assert.ok(afterSwitch.length > 0 && afterSwitch.every((c) => c.auth === 'Bearer test-client-token-2'), 'Sau khi đổi tài khoản không dùng token cũ');
  await mock({ mode: 'error' }); await cdp('Page.reload'); await until("Boolean(document.querySelector('[role=alert]'))");
  // Tên ở thanh bên đến từ phiên đăng nhập chung đã xác minh; thẻ hồ sơ của trang Tài khoản (từ API ví/hồ sơ) không được hiện.
  assert.equal(await evaluate("Boolean(document.querySelector('.account-profile'))"), false, 'Không hiện hồ sơ khi API lỗi');
  await mock({ mode: 'ok' });

  // ---- Admin: danh sách/tạo/sửa/khóa/nạp ví qua API.
  await token('staff', 'test-staff-token'); await go('/admin/customers'); await until("document.querySelectorAll('tbody tr').length === 2");
  assert.ok(await evaluate("document.querySelector('tbody tr').textContent.includes('Trần Thị B')"), 'Sắp xếp mới nhất trước');
  await click('Thêm khách hàng'); await until("Boolean(document.querySelector('dialog[open]'))");
  await input('dialog[open] [name=fullName]', 'Khách mới'); await input('dialog[open] [name=phone]', '0911111111');
  await input('dialog[open] [name=initialPassword]', 'MatKhau-12345'); await submit();
  await until("!document.querySelector('dialog[open]') && document.querySelectorAll('tbody tr').length === 3");
  const created = (await calls()).find((c) => c.method === 'POST' && c.path === '/api/admin/customers');
  assert.deepEqual(created.body, { fullName: 'Khách mới', phone: '0911111111', email: null, initialPassword: 'MatKhau-12345' });
  assert.ok(await evaluate("document.querySelector('tbody tr').textContent.includes('Khách mới')"), 'Khách mới đọc lại từ API');
  await click('Thêm khách hàng'); await until("Boolean(document.querySelector('dialog[open]'))");
  await input('dialog[open] [name=fullName]', 'Trùng số'); await input('dialog[open] [name=phone]', '0911111111');
  await input('dialog[open] [name=initialPassword]', 'MatKhau-12345'); await submit();
  await until("document.querySelector('dialog[open] [role=alert]')?.textContent.includes('đã tồn tại')");
  await closeDialog();
  assert.equal(await rows(), 3, 'Trùng số điện thoại không tạo thêm khách');

  await rowAction('Khách mới', 'Khóa'); await until("Boolean(document.querySelector('dialog[open]'))");
  assert.equal(await evaluate("document.querySelector('dialog[open] form').checkValidity()"), false, 'Khóa phải có lý do');
  await input('dialog[open] [name=reason]', 'Vi phạm nội quy'); await submit(); await until("!document.querySelector('dialog[open]')");
  const lock = (await calls()).find((c) => c.method === 'PUT' && c.path.endsWith('/status'));
  assert.deepEqual(lock.body, { status: 'Locked', reason: 'Vi phạm nội quy' });
  await until("[...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes('Khách mới')).textContent.includes('Đã khóa')");

  await rowAction('Nguyễn Văn A', 'Nạp ví'); await until("Boolean(document.querySelector('dialog[open]'))");
  await input('dialog[open] [name=amount]', 5000);
  assert.equal(await evaluate("document.querySelector('dialog[open] form').checkValidity()"), false, 'Số tiền nạp nhỏ hơn mức tối thiểu');
  await input('dialog[open] [name=amount]', 50000); await input('dialog[open] [name=method]', 'BankTransfer'); await submit();
  await until("!document.querySelector('dialog[open]')");
  const topUps = (await calls()).filter((c) => c.method === 'POST' && c.path.endsWith('/wallet/top-ups'));
  assert.equal(topUps.length, 1, 'Chỉ một request nạp (không gửi khi form không hợp lệ)');
  assert.deepEqual(topUps[0].body, { amount: 50000, collectionMethod: 'BankTransfer' });
  assert.ok(topUps[0].idem && topUps[0].idem.length >= 8, 'Request nạp có Idempotency-Key');
  await until("[...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes('Nguyễn Văn A')).textContent.includes('175.000')");
  await evaluate("[...document.querySelectorAll('tbody .admin-link')].find((b) => b.textContent === 'Nguyễn Văn A').click()");
  await until("(() => { const t = document.querySelector('dialog[open]')?.textContent ?? ''; return t.includes('Nạp tiền vào ví') && t.includes('50.000') && t.includes('175.000'); })()");
  await closeDialog();

  // ---- Chống nạp lặp: phản hồi bị mất (server ĐÃ cộng ví nhưng trình duyệt báo lỗi), nhân viên bấm gửi lại cùng form.
  const topUpCalls = async () => (await calls()).filter((c) => c.method === 'POST' && c.path.endsWith('/wallet/top-ups'));
  const topUpsBefore = (await topUpCalls()).length;
  await rowAction('Nguyễn Văn A', 'Nạp ví'); await until("Boolean(document.querySelector('dialog[open]'))");
  await input('dialog[open] [name=amount]', 20000);
  await mock({ topUpFail: 'after' }); await submit();
  await until("document.querySelector('dialog[open] [role=alert]')?.textContent.includes('Mất kết nối giả lập')");
  await mock({ topUpFail: null }); await submit(); await until("!document.querySelector('dialog[open]')");
  const retried = (await topUpCalls()).slice(topUpsBefore);
  assert.equal(retried.length, 2, 'Một lần lỗi và một lần gửi lại');
  assert.equal(retried[0].idem, retried[1].idem, 'Gửi lại dùng đúng khóa chống lặp cũ');
  await until("[...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes('Nguyễn Văn A')).textContent.includes('195.000')");
  assert.equal(await evaluate("[...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes('Nguyễn Văn A')).textContent.includes('215.000')"), false, 'Gửi lại không cộng ví lần hai');

  // Đổi số tiền sau một lần lỗi là một yêu cầu mới: phải dùng khóa MỚI (khóa cũ gắn với số tiền cũ sẽ bị server từ chối 409).
  await rowAction('Nguyễn Văn A', 'Nạp ví'); await until("Boolean(document.querySelector('dialog[open]'))");
  await input('dialog[open] [name=amount]', 10000);
  await mock({ topUpFail: 'before' }); await submit();
  await until("Boolean(document.querySelector('dialog[open] [role=alert]'))");
  await mock({ topUpFail: null }); await input('dialog[open] [name=amount]', 12000); await submit(); await until("!document.querySelector('dialog[open]')");
  const changed = (await topUpCalls()).slice(-2);
  assert.notEqual(changed[0].idem, changed[1].idem, 'Đổi số tiền thì dùng khóa mới');
  await until("[...document.querySelectorAll('tbody tr')].find((r) => r.textContent.includes('Nguyễn Văn A')).textContent.includes('207.000')");

  await mock({ mode: 'error' }); await cdp('Page.reload'); await until("Boolean(document.querySelector('.admin-message.error'))");
  assert.equal(await rows(), 0, 'Không có bảng khi API lỗi');
  assert.equal((await text()).includes('Nguyễn Văn A'), false, 'Không hiện dữ liệu cũ/mẫu khi API lỗi');
  await mock({ mode: 'ok' }); await click('Thử lại'); await until("document.querySelectorAll('tbody tr').length === 3");

  await screenshot('wallet-customers-api.png');
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: [
    'wallet balance/history from API with bearer token of the shared session and correct owner', 'wallet filters and reload', 'API error shows error without balance, retry works',
    'no shared session redirects to login, no client API call, legacy private token ignored', 'switching account never reuses the old token',
    'account profile per logged-in customer, no demo fallback', 'admin list newest first', 'admin create posts exact payload and reloads from API', 'duplicate phone error keeps list unchanged',
    'lock requires reason', 'top-up validation, single request with Idempotency-Key, balance reload', 'lost-response retry reuses the key and credits once', 'changed amount uses a new key', 'wallet history dialog', 'admin API error hides table'] }));
} finally { socket.close(); }
