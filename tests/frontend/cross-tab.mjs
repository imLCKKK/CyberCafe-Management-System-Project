import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, input, click, origin } from './cdp.mjs';

let otherSocket, targetId;
try {
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await go('/client/account'); await evaluate('localStorage.clear()'); await go('/client/account');
  ({ targetId } = await cdp('Target.createTarget', { url: origin + '/admin/customers' }));
  const targets = await fetch(`http://127.0.0.1:${process.env.FRONTEND_CDP_PORT || 9234}/json/list`).then(r => r.json());
  otherSocket = new WebSocket(targets.find(target => target.id === targetId).webSocketDebuggerUrl);
  await new Promise(resolve => otherSocket.addEventListener('open', resolve, { once: true }));
  let serial = 0;
  const pending = new Map();
  otherSocket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) { const task = pending.get(message.id); pending.delete(message.id); message.error ? task.reject(new Error(JSON.stringify(message.error))) : task.resolve(message.result); }
  });
  const remote = expression => new Promise((resolve, reject) => {
    const id = ++serial;
    const timeout = setTimeout(() => reject(Error('Second tab timed out')), 10000);
    pending.set(id, { resolve: result => { clearTimeout(timeout); if (result.exceptionDetails) reject(Error(result.exceptionDetails.text)); else resolve(result.result.value); }, reject });
    otherSocket.send(JSON.stringify({ id, method: 'Runtime.evaluate', params: { expression, returnByValue: true } }));
  });
  for (let attempt = 0; attempt < 100; attempt++) {
    if (await remote("Boolean(document.querySelector('.admin-module'))")) break;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  await remote("[...document.querySelectorAll('tbody tr:first-child button')].find(el => el.textContent === 'Sửa').click()");
  await remote("(() => { const el = document.querySelector('dialog input[name=display_name]'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, 'Đồng bộ từ admin'); el.dispatchEvent(new Event('input', { bubbles:true })); })()");
  await remote("document.querySelector('dialog form').requestSubmit()");
  await until("document.querySelector('.client-user').textContent.includes('Đồng bộ từ admin')");
  assert.equal(await evaluate("document.querySelector('input[autocomplete=name]').value"), 'Đồng bộ từ admin');
  await input('input[autocomplete=name]', 'Đồng bộ từ client'); await click('Lưu thay đổi');
  let reflected = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    if (await remote("document.querySelector('tbody').textContent.includes('Đồng bộ từ client')")) { reflected = true; break; }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  assert.ok(reflected, 'Admin receives client changes in a separate real browser tab');
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['real admin tab profile edit updates client without reload', 'real client tab edit updates admin without reload'] }));
} finally {
  otherSocket?.close();
  if (targetId) await cdp('Target.closeTarget', { targetId });
  socket.close();
}
