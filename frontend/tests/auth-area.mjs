import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, origin } from './cdp.mjs';

const storageKey = 'cybermanagement.auth.v1';
let secondTabSocket;
let secondTabId;

async function openSecondTab() {
  secondTabId = (await cdp('Target.createTarget', { url: 'about:blank' })).targetId;
  const targets = await fetch(`http://127.0.0.1:${process.env.FRONTEND_CDP_PORT || 9234}/json/list`).then(response => response.json());
  secondTabSocket = new WebSocket(targets.find(target => target.id === secondTabId).webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    secondTabSocket.addEventListener('open', resolve, { once: true });
    secondTabSocket.addEventListener('error', reject, { once: true });
  });
  let commandId = 0;
  const pendingCommands = new Map();
  secondTabSocket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (!message.id) return;
    const command = pendingCommands.get(message.id);
    pendingCommands.delete(message.id);
    if (message.error) command.reject(new Error(JSON.stringify(message.error)));
    else if (message.result?.exceptionDetails) command.reject(new Error(message.result.exceptionDetails.text));
    else command.resolve(message.result?.result?.value ?? message.result);
  });
  return (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++commandId;
    pendingCommands.set(id, { resolve, reject });
    secondTabSocket.send(JSON.stringify({ id, method, params }));
  });
}

try {
  await cdp('Page.enable');
  await cdp('Runtime.enable');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (url, options = {}) => {
      const path = new URL(url, location.href).pathname;
      const calls = JSON.parse(localStorage.getItem('__authTestCalls') || '[]');
      calls.push({ path, method: options.method || 'GET', authorization: options.headers?.get('Authorization') || null });
      localStorage.setItem('__authTestCalls', JSON.stringify(calls));
      const scenario = localStorage.getItem('__authTestScenario');
      if (path === '/api/auth/me' && scenario === 'me-error') {
        return Response.json({ message: 'Unavailable.' }, { status: 500 });
      }
      if (path === '/api/auth/me' && scenario === 'authenticated') {
        return Response.json({ employeeId: 4, username: 'waiter01', role: 'Waiter', permissions: ['employee.view'] });
      }
      if (path === '/api/auth/customer/me' && scenario === 'customer-pending') {
        await new Promise(resolve => setTimeout(resolve, 600));
        return Response.json({ customerId: 7, phone: '0900000007', fullName: 'Test customer' });
      }
      if (path === '/api/auth/logout') return new Response(null, { status: 204 });
      return originalFetch(url, options);
    };
  })()` });

  await evaluate(`localStorage.clear(); localStorage.setItem('__authTestScenario', 'no-session')`);
  await go('/admin/staff', 'document.querySelector(".auth-form")');
  assert.equal(await evaluate('location.pathname'), '/admin/auth', 'Direct unauthenticated access redirects to employee login');
  assert.equal(await evaluate(`(JSON.parse(localStorage.getItem('__authTestCalls') || '[]')).some(call => call.path === '/api/auth/me')`), false, 'No session does not trigger a principal request');
  assert.equal(await evaluate('Boolean(document.querySelector(".admin-shell"))'), false, 'Admin UI is not rendered');

  await evaluate(`localStorage.setItem('__authTestScenario', 'authenticated'); localStorage.setItem('${storageKey}', JSON.stringify({ accessToken: 'test-token', accountType: 'employee' }))`);
  await go('/admin/staff', 'document.querySelector(".admin-shell")');
  await evaluate('document.querySelector("[aria-label=\\"Đăng xuất\\"]").click()');
  await until('location.pathname === "/admin/auth" && Boolean(document.querySelector(".auth-form"))');
  assert.equal(await evaluate(`localStorage.getItem('${storageKey}')`), null, 'Logout clears the stored session');

  await evaluate(`localStorage.setItem('__authTestScenario', 'me-error'); localStorage.setItem('${storageKey}', JSON.stringify({ accessToken: 'invalid-token', accountType: 'employee' }))`);
  await go('/admin/staff', 'document.querySelector(".auth-form")');
  assert.equal(await evaluate('location.pathname'), '/admin/auth', 'A failed /me request redirects to employee login');
  assert.equal(await evaluate(`localStorage.getItem('${storageKey}')`), null, 'A failed /me request clears the invalid session');
  assert.equal(await evaluate('Boolean(document.querySelector(".admin-shell"))'), false, 'Admin UI remains hidden after /me fails');

  await evaluate(`(() => {
    localStorage.setItem('__authTestScenario', 'authenticated');
    const newValue = JSON.stringify({ accessToken: 'waiter-token', accountType: 'employee' });
    localStorage.setItem('${storageKey}', newValue);
    dispatchEvent(new StorageEvent('storage', { key: '${storageKey}', oldValue: null, newValue, storageArea: localStorage }));
  })()`);
  await until('location.pathname === "/admin/staff" && Boolean(document.querySelector(".admin-shell"))');
  assert.equal(await evaluate('document.querySelector(".user b")?.textContent'), 'waiter01', 'The first tab displays the verified employee identity');
  const secondTabCommand = await openSecondTab();
  await secondTabCommand('Page.enable');
  await secondTabCommand('Runtime.enable');
  await secondTabCommand('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    window.fetch = async url => {
      const path = new URL(url, location.href).pathname;
      if (path === '/api/auth/me') return Response.json({ employeeId: 4, username: 'waiter01', role: 'Waiter', permissions: ['employee.view'] });
      if (path === '/api/auth/customer/me') return Response.json({ customerId: 7, phone: '0900000007', fullName: 'Test customer' });
      return Response.json({ message: 'Unexpected request.' }, { status: 500 });
    };
  })()` });
  await secondTabCommand('Page.navigate', { url: `${origin}/admin/auth` });
  let secondTabReady = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    const ready = await secondTabCommand('Runtime.evaluate', { expression: 'Boolean(document.querySelector(".admin-shell"))', returnByValue: true });
    if (ready) {
      secondTabReady = true;
      break;
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  assert.equal(secondTabReady, true, 'Second tab verifies the existing employee session before replacement');
  await until('location.pathname === "/admin/staff" && Boolean(document.querySelector(".admin-shell"))');
  await secondTabCommand('Runtime.evaluate', {
    expression: `(() => {
      localStorage.setItem('__authTestScenario', 'customer-pending');
      localStorage.setItem('${storageKey}', JSON.stringify({ accessToken: 'customer-token', accountType: 'customer' }));
    })()`,
    returnByValue: true,
  });
  await until('Boolean(document.querySelector(".auth-loading"))');
  assert.equal(await evaluate('Boolean(document.querySelector(".admin-shell"))'), false, 'The previous employee UI is hidden during identity verification');
  assert.equal(await evaluate(`import('/src/shared/auth/api.js').then(({ apiRequest }) => apiRequest('/orders')).then(() => 'sent', error => error.code)`), 'AUTH_SESSION_UNVERIFIED', 'API operations are blocked until the replacement identity is verified');
  await until('location.pathname === "/client/home" && Boolean(document.querySelector(".client-shell"))');
  const customerRequest = await evaluate(`JSON.parse(localStorage.getItem('__authTestCalls')).find(call => call.path === '/api/auth/customer/me')`);
  assert.equal(customerRequest.authorization, 'Bearer customer-token', 'The replacement identity is verified with its matching token');
  assert.equal(await evaluate('Boolean(document.querySelector(".admin-shell"))'), false, 'The old employee area is not restored after customer verification');

  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['direct unauthenticated access', 'logout', '/me failure', 'cross-tab identity replacement and API request gate'] }));
} finally {
  secondTabSocket?.close();
  if (secondTabId) await cdp('Target.closeTarget', { targetId: secondTabId });
  socket.close();
}
