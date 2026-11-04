import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const origin = process.env.FRONTEND_URL || 'http://127.0.0.1:5174';
export const outputDir = path.resolve(process.env.FRONTEND_ARTIFACTS || fileURLToPath(new URL('../../.tools/frontend-retest', import.meta.url)));
export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const targets = await fetch(`http://127.0.0.1:${process.env.FRONTEND_CDP_PORT || 9234}/json/list`).then(r => r.json());
const page = targets.find(target => target.type === 'page' && target.url.startsWith(origin)) || targets.find(target => target.type === 'page');
if (!page) throw new Error('No browser page available for frontend tests.');
export const socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
let serial = 0;
const pending = new Map();
export const errors = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.exception?.description || message.params.exceptionDetails.text);
  if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') errors.push(message.params.args.map(arg => arg.value || arg.description).join(' '));
  if (message.id && pending.has(message.id)) {
    const task = pending.get(message.id); pending.delete(message.id);
    clearTimeout(task.timeout);
    message.error ? task.reject(new Error(JSON.stringify(message.error))) : task.resolve(message.result);
  }
});
export async function cdp(method, params = {}) {
  let onLoad, timer;
  const navigation = ['Page.navigate', 'Page.reload'].includes(method) ? new Promise((resolve, reject) => {
    onLoad = ({ data }) => { if (JSON.parse(data).method === 'Page.loadEventFired') resolve(); };
    socket.addEventListener('message', onLoad);
    timer = setTimeout(() => reject(new Error(`Navigation timed out: ${method}`)), 15000);
  }) : null;
  try {
  const result = await new Promise((resolve, reject) => {
    const id = ++serial;
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 15000);
    pending.set(id, { resolve, reject, timeout });
    socket.send(JSON.stringify({ id, method, params }));
  });
  if (navigation) await navigation;
  return result;
  } finally {
    clearTimeout(timer);
    if (onLoad) socket.removeEventListener('message', onLoad);
  }
}
export async function evaluate(expression) {
  const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
  return result.result.value;
}
export async function until(expression) {
  for (let index = 0; index < 150; index++) { if (await evaluate(expression)) return; await sleep(80); }
  throw new Error(`Timed out: ${expression}; errors: ${JSON.stringify(errors)}; body: ${await evaluate('document.body.innerText.slice(0, 1200)')}`);
}
export async function screenshot(name) {
  await mkdir(outputDir, { recursive: true });
  const { data } = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile(path.join(outputDir, name), Buffer.from(data, 'base64'));
}
export async function press(key) {
  const codes = { Escape: 27, Tab: 9, Enter: 13, ' ': 32 };
  for (const type of ['keyDown', 'keyUp']) await cdp('Input.dispatchKeyEvent', { type, key, code: key, windowsVirtualKeyCode: codes[key] });
}
export const click = (text, selector = 'button') => evaluate(`(() => { const target = [...document.querySelectorAll(${JSON.stringify(selector)})].find(el => el.textContent.trim() === ${JSON.stringify(text)}); if (!target || target.disabled) throw Error('Missing/disabled: ' + ${JSON.stringify(text)}); target.click(); })()`);
export const input = (selector, value) => evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); if (!el) throw Error('Missing input: ' + ${JSON.stringify(selector)}); const proto = el.tagName === 'SELECT' ? HTMLSelectElement.prototype : el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, ${JSON.stringify(String(value))}); el.dispatchEvent(new Event(el.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true })); })()`);
export async function go(route, ready = 'document.querySelector(".cafe-heading, .home-heading, .tour-heading, .placeholder")') {
  await cdp('Page.navigate', { url: origin + route }); await sleep(160); await until(`Boolean(${ready})`);
}

export async function setupWorkflowLogin(accountType, { resetDemo = false } = {}) {
  const isCustomer = accountType === 'customer';
  const authRoute = isCustomer ? '/client/auth' : '/admin/auth';
  const destination = isCustomer ? '/client/home' : '/admin/orders';
  const loginPath = isCustomer ? '/api/auth/customer/login' : '/api/auth/login';
  const principalPath = isCustomer ? '/api/auth/customer/me' : '/api/auth/me';
  const logoutPath = isCustomer ? '/api/auth/customer/logout' : '/api/auth/logout';
  const identifier = isCustomer ? '0900000007' : 'workflow-admin';
  const password = 'Workflow@2026';
  const token = `workflow-${accountType}-token`;
  const identity = isCustomer
    ? { customerId: 7, phone: identifier, fullName: 'Workflow Customer' }
    : {
      employeeId: 99,
      username: identifier,
      role: 'Admin',
      permissions: ['order.view', 'computer.view', 'customer.view', 'product.view', 'stockreceipt.view', 'inventory.adjust', 'invoice.view', 'tournament.view', 'employee.view', 'report.view'],
    };

  const alreadyAtOrigin = await evaluate(`location.origin === ${JSON.stringify(new URL(origin).origin)}`);
  if (alreadyAtOrigin) {
    await evaluate("localStorage.removeItem('cybermanagement.auth.v1')");
  } else {
    await cdp('Page.navigate', { url: `${origin}${authRoute}` });
    await evaluate("localStorage.removeItem('cybermanagement.auth.v1')");
  }
  await cdp('Page.addScriptToEvaluateOnNewDocument', {
    source: `(() => {
      const originalFetch = window.fetch.bind(window);
      window.fetch = async (request, options = {}) => {
        const requestUrl = typeof request === 'string' ? request : request.url;
        const path = new URL(requestUrl, location.href).pathname;
        const method = (options.method || 'GET').toUpperCase();
        if (path === ${JSON.stringify(loginPath)} && method === 'POST') {
          const credentials = JSON.parse(options.body || '{}');
          const identifier = credentials.${isCustomer ? 'phone' : 'username'};
          if (identifier !== ${JSON.stringify(identifier)} || credentials.password !== ${JSON.stringify(password)}) {
            return Response.json({ message: 'Thông tin đăng nhập kiểm thử không hợp lệ.' }, { status: 401 });
          }
          return Response.json({ accessToken: ${JSON.stringify(token)} });
        }
        if (path === ${JSON.stringify(principalPath)}) return Response.json(${JSON.stringify(identity)});
        if (path === ${JSON.stringify(logoutPath)}) return new Response(null, { status: 204 });
        return originalFetch(request, options);
      };
    })()`,
  });
  await cdp('Page.navigate', { url: `${origin}${authRoute}` });
  await until('Boolean(document.querySelector(".auth-form"))');
  if (resetDemo) await clearDemoStorage();
  await input('#auth-identifier', identifier);
  await input('#auth-password', password);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until(`location.pathname === ${JSON.stringify(destination)} && Boolean(document.querySelector(${JSON.stringify(isCustomer ? '.home-heading' : '.admin-shell')}))`);
}

export async function clearDemoStorage() {
  await evaluate(`(() => {
    const authSession = localStorage.getItem('cybermanagement.auth.v1');
    localStorage.clear();
    if (authSession) localStorage.setItem('cybermanagement.auth.v1', authSession);
  })()`);
}
